#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, Creating The Tag
 * File Name     : SmartTagCreator.cs
 * Purpose       : Step 4: creates the tag in Revit - the reference (this model's element, or a linked element
 *                 through its link), the transaction, the tag type and the L-shaped leader.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-10-15
 * Last Updated  : 2026-10-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Utils, the other Smart MEP Tags files
 *
 * Input         : One candidate and its chosen spot.
 * Output        : A tag in the model, or false when Revit refused it.
 *
 * Notes         :
 * - Part of the Smart MEP Tags restructure (suite 1.58.0): the code that used to sit in
 *   SmartMepTagService.cs and SmartTagPlacementEngine.cs is split one job per file, in step
 *   folders. Every routine was moved as-is - tagging results are unchanged.
 * - The numbers that tune the algorithm live in SmartTagTuning.cs.
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Split out of the old Smart MEP Tag service / placement engine.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;
using AJTools.Services.LeaderLogic;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Creates one tag in Revit.
    /// </summary>
    internal static class SmartTagCreator
    {
        /// <summary>
        /// The reference a new tag should point at. For this model's own element, the element itself.
        /// For an element inside a linked model, the element's reference carried through its link
        /// instance (CreateLinkReference) - that is what lets a host tag sit on a linked element.
        /// Null when the element (or its link) can no longer be found.
        /// </summary>
        internal static Reference BuildTagReference(Document doc, TagCandidate candidate)
        {
            if (!candidate.IsLinked)
            {
                Element hostElement = doc.GetElement(candidate.ElementId);
                return hostElement == null ? null : new Reference(hostElement);
            }

            RevitLinkInstance link = doc.GetElement(candidate.LinkInstanceId) as RevitLinkInstance;
            Document linkDoc = link?.GetLinkDocument();
            Element linkedElement = linkDoc?.GetElement(candidate.ElementId);
            if (linkedElement == null)
                return null;

            return new Reference(linkedElement).CreateLinkReference(link);
        }

        /// <summary>
        /// Gives a tag that was just created its L-shaped leader, through the shared TagLeaderService
        /// routine. False only when Revit refused the leader elbow.
        /// </summary>
        internal static bool ApplyLeaderBehavior(
            Document doc,
            IndependentTag tag,
            View view,
            LeaderLogicService leaderLogic)
        {
            if (doc == null || tag == null || view == null || leaderLogic == null)
                return true;

            // Tidy(): enable the leader, probe for the leader end (these tags were just created, so it
            // is not always readable straight away), nudge the elbow clear of the text, and retry via
            // the Free condition if Revit refuses. Exactly what the hand-written body here used to do -
            // it now lives in TagLeaderService so Stack Tags, Rearrange Tags and L-Shape Leader share
            // the same implementation instead of each keeping their own copy.
            return TagLeaderService.ApplyLShapedLeader(
                doc, tag, view, leaderLogic, TagLeaderOptions.Tidy());
        }

        /// <summary>
        /// Places a single tag inside its own transaction.
        /// After placement, registers the new tag's bounding box in the annotation list
        /// so subsequent tags avoid clashing with it.
        /// </summary>
        internal static bool PlaceSingleTag(
            Document doc,
            View view,
            TagCandidate candidate,
            XYZ tagPosition,
            List<AnnotationBox> annotations,
            AnnotationSpatialIndex spatialIndex,
            int viewScale,
            XYZ viewRight,
            XYZ viewUp,
            LeaderLogicService leaderLogic)
        {
            try
            {
                using (Transaction t = new Transaction(doc, "Smart MEP Tag"))
                {
                    t.Start();
                    try
                    {
                        // Revit 2020 API: IndependentTag.Create(Document, View.Id, Reference, bool addLeader, TagMode, TagOrientation, XYZ)
                        // A linked element gets a link reference - same call, same tag type.
                        Reference elemRef = BuildTagReference(doc, candidate);
                        if (elemRef == null)
                        {
                            t.RollBack();
                            return false;
                        }

                        bool addLeader = candidate.NeedsLeader;
                        TagOrientation orientation = TagOrientation.Horizontal;

                        if (!addLeader && candidate.Orientation == ElementOrientation.Vertical)
                        {
                            orientation = TagOrientation.Vertical;
                        }

                        IndependentTag newTag = IndependentTag.Create(
                            doc,
                            view.Id,
                            elemRef,
                            addLeader,
                            TagMode.TM_ADDBY_CATEGORY,
                            orientation,
                            tagPosition);

                        if (newTag == null)
                        {
                            t.RollBack();
                            return false;
                        }

                        // Set the tag type to our resolved family symbol.
                        try
                        {
                            newTag.ChangeTypeId(candidate.TagTypeId);
                        }
                        catch (Exception)
                        {
                            // If type change fails, the default type was used — still acceptable.
                        }

                        if (addLeader)
                        {
                            if (!ApplyLeaderBehavior(doc, newTag, view, leaderLogic))
                            {
                                t.RollBack();
                                return false;
                            }
                        }

                        t.Commit();

                        // Register the new tag's bounding box so next tags avoid it.
                        try
                        {
                            BoundingBoxXYZ newBB = newTag.get_BoundingBox(view);
                            if (newBB != null)
                            {
                                AnnotationBox placedBox = ConvertBoundingBoxToViewPlane(newBB, viewRight, viewUp);
                                if (placedBox != null)
                                {
                                    annotations.Add(placedBox);
                                    spatialIndex?.Add(placedBox);
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // If we can't read the new tag's bbox, estimate it.
                            double tagW, tagH;
                            EstimateFallbackTagSize(viewScale, out tagW, out tagH);
                            AnnotationBox estimatedBox = CreateCandidateBoxInViewPlane(
                                tagPosition, tagW, tagH, viewRight, viewUp);
                            annotations.Add(estimatedBox);
                            spatialIndex?.Add(estimatedBox);
                        }

                        return true;
                    }
                    catch (Exception)
                    {
                        if (t.HasStarted() && !t.HasEnded())
                            t.RollBack();
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
