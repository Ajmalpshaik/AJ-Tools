#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, Scoring A Spot
 * File Name     : SmartTagPositionScorer.cs
 * Purpose       : Step 4: the points system that rates one spot - free space, distance to other tags and the
 *                 preference for above/below. A clash disqualifies the spot.
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
 * Input         : One proposed tag box and what is already on the view.
 * Output        : A score, or -1 when the spot is refused.
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
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagCandidatePositions;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// The points system that rates one spot.
    /// </summary>
    internal static class SmartTagPositionScorer
    {
        /// <summary>
        /// Scores a candidate tag position on a 0–100 scale.
        /// Returns -1 if the position is disqualified (hard clash or leader too long).
        /// </summary>
        internal static int ScoreCandidatePosition(
            XYZ candidatePos,
            TagDirection direction,
            List<AnnotationBox> existingAnnotations,
            AnnotationSpatialIndex spatialIndex,
            double estimatedTagWidth,
            double estimatedTagHeight,
            int viewScale,
            XYZ viewRight,
            XYZ viewUp,
            AnnotationBox hostBox = null)
        {
            // Build the proposed tag box in view-plane coordinates.
            AnnotationBox proposed = CreateCandidateBoxInViewPlane(
                candidatePos, estimatedTagWidth, estimatedTagHeight, viewRight, viewUp);

            int totalScore = 0;

            // ── CRITERION 1: FREE SPACE (0–40 pts) ──
            // No clash = 40 pts. Any overlap = disqualified.
            double tolerance = SmartTagTuning.ClashTolerancePaperMm * Constants.MM_TO_FEET * viewScale;
            double nearMiss = SmartTagTuning.NearMissPaperMm * Constants.MM_TO_FEET * viewScale;
            double comfortableGap = SmartTagTuning.ComfortableGapPaperMm * Constants.MM_TO_FEET * viewScale;

            if (hostBox != null && proposed.Inflated(tolerance).Overlaps(hostBox))
            {
                return -1; // Disqualified — clashes with its own host element.
            }

            double searchMargin = Math.Max(nearMiss, comfortableGap);
            List<AnnotationBox> nearbyAnnotations = QueryNearbyAnnotations(
                spatialIndex,
                existingAnnotations,
                proposed.Inflated(searchMargin));
            bool hasClash = false;
            bool hasNearMiss = false;

            foreach (AnnotationBox existing in nearbyAnnotations)
            {
                if (proposed.Inflated(tolerance).Overlaps(existing))
                {
                    hasClash = true;
                    break;
                }
                if (proposed.Inflated(nearMiss).Overlaps(existing))
                {
                    hasNearMiss = true;
                }
            }

            if (hasClash)
                return -1; // Disqualified — hard clash.

            totalScore += hasNearMiss ? SmartTagTuning.NearMissPoints : SmartTagTuning.FreeSpacePoints;

            // ── CRITERION 2: DISTANCE FROM OTHER TAGS (0–20 pts) ──
            double minDist = double.MaxValue;
            double minimumGap = SmartTagTuning.MinimumGapPaperMm * Constants.MM_TO_FEET * viewScale;

            foreach (AnnotationBox existing in nearbyAnnotations)
            {
                double dist = proposed.DistanceTo(existing);
                if (dist < minDist)
                    minDist = dist;
            }

            if (minDist > comfortableGap)
                totalScore += SmartTagTuning.ComfortableGapPoints;
            else if (minDist >= minimumGap)
                totalScore += SmartTagTuning.MinimumGapPoints;
            else
                return -1; // Too close — disqualified.

            // ── CRITERION 3: ALIGNMENT PREFERENCE (0–20 pts) ──
            // Horizontal placement is always preferred for readability.
            if (direction == TagDirection.Top || direction == TagDirection.Bottom)
                totalScore += SmartTagTuning.AboveOrBelowPoints; // Horizontal — best readability.
            else
                totalScore += SmartTagTuning.BesidePoints; // Left/Right — acceptable but not ideal.

            // ── CRITERION 4: LEADER CONSISTENCY (0–20 pts) ──
            // Max-leader filtering is intentionally disabled. Offset value should control placement.
            // So this is a FLAT 20 on every position, not a real criterion - the score is effectively
            // out of 80 with 20 free points, and the "score >= 60" early exit in FindBestTagPosition
            // is calibrated against that. Left exactly as-is on purpose: removing the 20 would
            // silently retune every placement decision Smart MEP Tag makes. Only change it together
            // with that threshold. Both numbers are in SmartTagTuning.
            totalScore += SmartTagTuning.LeaderConsistencyPoints;

            return totalScore;
        }
    }
}
