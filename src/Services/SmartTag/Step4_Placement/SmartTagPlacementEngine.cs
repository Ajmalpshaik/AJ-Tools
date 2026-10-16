#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, Placement Loop
 * File Name     : SmartTagPlacementEngine.cs
 * Purpose       : Step 4: the placement loop. Builds the picture of the view once, then for each element in
 *                 priority order asks SmartTagPositionFinder where the tag goes and SmartTagCreator to place it.
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
 * Input         : Candidates in priority order.
 * Output        : Tags placed in the model; one result line per element.
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

using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// The placement loop: one element at a time, in priority order.
    /// </summary>
    internal static class SmartTagPlacementEngine
    {
        /// <summary>
        /// Places a tag on every candidate it can, in priority order (High first), and adds one result
        /// line per candidate.
        /// </summary>
        public static void ProcessAndPlaceTags(
            Document doc,
            PreFlightResult preflight,
            SmartTagSettingsState settingsState,
            List<TagCandidate> candidates,
            List<TagPlacementResult> results)
        {
            // Work out the picture of the view ONCE: what is already on it, where every element is,
            // the crop boundary, the expected tag sizes.
            SmartTagPlacementContext context =
                SmartTagPlacementContext.Build(doc, preflight, settingsState, candidates);

            // Process in priority order (candidates are already sorted HIGH → MEDIUM → LOW).
            foreach (TagCandidate candidate in candidates)
            {
                if (candidate == null)
                    continue;

                if (candidate.TagTypeId == null || candidate.TagTypeId == ElementId.InvalidElementId)
                {
                    results.Add(ResultFor(
                        candidate, false, TagSkipReason.NoTagFamilyAvailable, "Tag type ID was not resolved"));
                    continue;
                }

                // ── Where does this tag go? (SmartTagPositionFinder) ──
                TagSkipReason skipReason;
                XYZ tagPosition = SmartTagPositionFinder.FindBestTagPosition(candidate, context, out skipReason);

                if (tagPosition == null)
                {
                    results.Add(ResultFor(candidate, false, skipReason, null));
                    continue;
                }

                // ── Place the tag in its own transaction (SmartTagCreator) ──
                bool placed = SmartTagCreator.PlaceSingleTag(
                    context.Doc, context.View, candidate, tagPosition, context.Annotations, context.AnnotationIndex,
                    context.ViewScale, context.ViewRight, context.ViewUp, context.LeaderLogic);

                if (placed)
                {
                    context.TaggedCandidates.Add(candidate);
                    results.Add(ResultFor(candidate, true, TagSkipReason.None, null));
                }
                else
                {
                    // A clean spot WAS found - Revit then refused to create or finish the tag there.
                    // That is not "no clean space", so it carries no skip reason (TagSkipReason has no
                    // value for it): failed with None is how the report recognises it.
                    results.Add(ResultFor(
                        candidate, false, TagSkipReason.None, "Revit could not create the tag at the chosen spot"));
                }
            }
        }

        /// <summary>
        /// One result line for a candidate, keeping its link so the report can name a linked element.
        /// </summary>
        internal static TagPlacementResult ResultFor(
            TagCandidate candidate,
            bool success,
            TagSkipReason skipReason,
            string note)
        {
            return new TagPlacementResult
            {
                ElementId = candidate.ElementId,
                LinkInstanceId = candidate.LinkInstanceId,
                LinkName = candidate.LinkName,
                Category = candidate.Category,
                Success = success,
                SkipReason = skipReason,
                Note = note
            };
        }
    }
}
