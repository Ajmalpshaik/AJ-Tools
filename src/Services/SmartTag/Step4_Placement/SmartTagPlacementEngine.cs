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

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using AJTools.Models.SmartTag;
using AJTools.Services.CreateTags;
using AJTools.Services.LeaderLogic;
using AJTools.Services.TagClash;
using AJTools.Services.ViewCrop;
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagCandidatePositions;
using static AJTools.Services.SmartTag.SmartTagCreator;
using static AJTools.Services.SmartTag.SmartTagElementCollector;
using static AJTools.Services.SmartTag.SmartTagElementFilters;
using static AJTools.Services.SmartTag.SmartTagElementGeometry;
using static AJTools.Services.SmartTag.SmartTagPositionFinder;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagTypeSelector;
using static AJTools.Services.SmartTag.SmartTagViewCheck;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// The placement loop: one element at a time, in priority order.
    /// </summary>
    internal static class SmartTagPlacementEngine
    {
        /// <summary>
        /// Processes all candidates through the scoring/clash/reposition pipeline
        /// and places tags. Returns results for each element.
        /// </summary>
        public static void ProcessAndPlaceTags(
            Document doc,
            PreFlightResult preflight,
            SmartTagSettingsState settingsState,
            List<TagCandidate> candidates,
            List<TagPlacementResult> results)
        {
            View activeView = preflight.ActiveView;
            int viewScale = preflight.ViewScale;
            XYZ viewRight = GetViewRightDirection(activeView);
            XYZ viewUp = GetViewUpDirection(activeView);
            LeaderLogicService leaderLogic = new LeaderLogicService(activeView);

            // Collect all existing annotations ONCE before the placement loop.
            List<AnnotationBox> annotations = CollectExistingAnnotations(doc, activeView);
            AnnotationSpatialIndex annotationSpatialIndex = BuildAnnotationSpatialIndex(annotations, viewScale);
            Dictionary<BuiltInCategory, TagSizeHint> sizeHints =
                BuildTagSizeHints(doc, activeView, candidates, viewRight, viewUp, viewScale);
            AnnotationBox placementBoundary =
                BuildPlacementBoundaryInViewPlane(preflight, viewRight, viewUp);

            // Index every candidate's own position ONCE too, for the parallel-group check inside
            // FindBestTagPosition - same reasoning as the annotation index above.
            Dictionary<AnnotationBox, TagCandidate> candidateByBox;
            AnnotationSpatialIndex candidateSpatialIndex = BuildCandidateSpatialIndex(candidates, out candidateByBox);

            // Only built when a category actually has its leader switched off - it is the no-leader
            // placement that needs to know where the ducts are, and a normal run should not pay for it.
            AnnotationSpatialIndex modelObstacleIndex = null;
            Dictionary<ElementId, AnnotationBox> modelObstacleBoxes = null;
            foreach (TagCandidate probe in candidates)
            {
                if (probe != null && !SmartTagSettingsTracker.ResolveUseLeader(settingsState, probe.Category))
                {
                    modelObstacleIndex = BuildModelObstacleIndex(
                        doc, activeView, viewRight, viewUp, viewScale, out modelObstacleBoxes);
                    break;
                }
            }

            // Track which elements have been successfully tagged — used for parallel group logic.
            // Held as the candidates themselves: an ElementId alone cannot tell a linked element from
            // one of this model's own.
            var taggedGroupMembers = new HashSet<TagCandidate>();

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

                // ── Find best position (Phases 3–5) ──
                TagSkipReason skipReason;
                XYZ tagPosition = FindBestTagPosition(
                    candidate, annotations, annotationSpatialIndex, viewScale, settingsState, sizeHints,
                    candidates, candidateSpatialIndex, candidateByBox, taggedGroupMembers,
                    preflight, placementBoundary, modelObstacleIndex, modelObstacleBoxes, viewRight, viewUp,
                    out skipReason);

                if (tagPosition == null)
                {
                    results.Add(ResultFor(candidate, false, skipReason, null));
                    continue;
                }

                // ── Place the tag in its own transaction ──
                bool placed = PlaceSingleTag(
                    doc, activeView, candidate, tagPosition, annotations, annotationSpatialIndex, viewScale, viewRight, viewUp, leaderLogic);

                if (placed)
                {
                    taggedGroupMembers.Add(candidate);
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
