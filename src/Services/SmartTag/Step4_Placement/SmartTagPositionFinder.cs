#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, Where The Tag Goes
 * File Name     : SmartTagPositionFinder.cs
 * Purpose       : Step 4, THE ALGORITHM that decides where one tag goes: the spots around the element are tried
 *                 with a leader first, then without; then the tagged-group rule; then the crowded-area rule.
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
 * Input         : One candidate and the picture of the view.
 * Output        : The tag head point, or null with the reason.
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
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagCandidatePositions;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Decides where one tag goes - the heart of Smart MEP Tags.
    /// </summary>
    internal static class SmartTagPositionFinder
    {
        internal static readonly double ParallelGroupDistance = SmartTagTuning.ParallelGroupDistanceMm * Constants.MM_TO_FEET;

        /// <summary>
        /// Where one tag goes - THE placement algorithm. Three stages, tried in order:
        ///   1. The spots around the element: above/below/right/left (the order depends on the element
        ///      and the view), at a few points along a run, with a leader first and then without. The
        ///      best-scoring spot wins; a spot scoring SmartTagTuning.GoodEnoughScore is taken at once.
        ///   2. Parallel runs: if nothing fitted and another run in the same tight group already has
        ///      a tag, this one is skipped - one tag per group reads cleaner than a pile of them.
        ///   3. Crowded area: if nothing fitted, a High priority element is tagged at the least-bad
        ///      spot anyway; Medium and Low ones are skipped.
        /// Returns the tag head point, or null with the reason the element was not tagged.
        /// </summary>
        /// <remarks>
        /// Sets candidate.NeedsLeader to say whether the chosen spot wants a leader. The numbers that tune
        /// every stage are in SmartTagTuning.
        /// </remarks>
        internal static XYZ FindBestTagPosition(
            TagCandidate candidate,
            SmartTagPlacementContext context,
            out TagSkipReason skipReason)
        {
            skipReason = TagSkipReason.None;

            if (candidate == null)
            {
                skipReason = TagSkipReason.NoCleanSpaceAvailable;
                return null;
            }

            double fixedOffsetInternal = SmartTagSettingsTracker.ResolveOffsetInternal(context.Settings, candidate.Category);

            double tagWidth;
            double tagHeight;
            ResolveTagSizeForCandidate(candidate, context.SizeHints, context.ViewScale, out tagWidth, out tagHeight);

            // ── STAGE 1: the spots around the element ──
            bool hadBoundaryCandidate;
            XYZ aroundTheElement = TryAroundTheElement(
                candidate, context, fixedOffsetInternal, tagWidth, tagHeight, out hadBoundaryCandidate);
            if (aroundTheElement != null)
                return aroundTheElement;

            if (!hadBoundaryCandidate)
            {
                skipReason = TagSkipReason.OutsideCropRegion;
                return null;
            }

            // ── STAGE 2: parallel runs - one tag per group ──
            if (IsCoveredByTaggedGroup(candidate, context))
            {
                skipReason = TagSkipReason.PartOfTaggedGroup;
                return null;
            }

            // ── STAGE 3: crowded area ──
            if (candidate.IsDenseZone)
            {
                XYZ crowdedSpot = TryCrowdedAreaSpot(
                    candidate, context, fixedOffsetInternal, tagWidth, tagHeight, out skipReason);
                if (crowdedSpot != null || skipReason != TagSkipReason.None)
                    return crowdedSpot;
            }

            // ── Nothing fitted ──
            skipReason = TagSkipReason.NoCleanSpaceAvailable;
            return null;
        }

        /// <summary>
        /// Stage 1: tries the spots around the element and returns the best one, or null when none
        /// scored. <paramref name="hadBoundaryCandidate"/> is false when every spot fell outside the
        /// crop, which the caller reports as "outside crop region".
        /// </summary>
        private static XYZ TryAroundTheElement(
            TagCandidate candidate,
            SmartTagPlacementContext context,
            double fixedOffsetInternal,
            double tagWidth,
            double tagHeight,
            out bool hadBoundaryCandidate)
        {
            // Same names the code below has always used.
            SmartTagSettingsState settingsState = context.Settings;
            PreFlightResult preflight = context.Preflight;
            List<AnnotationBox> annotations = context.Annotations;
            AnnotationSpatialIndex spatialIndex = context.AnnotationIndex;
            AnnotationBox placementBoundary = context.PlacementBoundary;
            AnnotationSpatialIndex modelObstacleIndex = context.ModelObstacleIndex;
            Dictionary<ElementId, AnnotationBox> modelObstacleBoxes = context.ModelObstacleBoxes;
            int viewScale = context.ViewScale;
            XYZ viewRight = context.ViewRight;
            XYZ viewUp = context.ViewUp;

            hadBoundaryCandidate = false;


            AnnotationBox hostBox = null;
            if (candidate.BoundingBox != null)
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                XYZ[] corners = new XYZ[]
                {
                    new XYZ(candidate.BoundingBox.Min.X, candidate.BoundingBox.Min.Y, candidate.BoundingBox.Min.Z),
                    new XYZ(candidate.BoundingBox.Max.X, candidate.BoundingBox.Min.Y, candidate.BoundingBox.Min.Z),
                    new XYZ(candidate.BoundingBox.Min.X, candidate.BoundingBox.Max.Y, candidate.BoundingBox.Min.Z),
                    new XYZ(candidate.BoundingBox.Max.X, candidate.BoundingBox.Max.Y, candidate.BoundingBox.Min.Z),
                    new XYZ(candidate.BoundingBox.Min.X, candidate.BoundingBox.Min.Y, candidate.BoundingBox.Max.Z),
                    new XYZ(candidate.BoundingBox.Max.X, candidate.BoundingBox.Min.Y, candidate.BoundingBox.Max.Z),
                    new XYZ(candidate.BoundingBox.Min.X, candidate.BoundingBox.Max.Y, candidate.BoundingBox.Max.Z),
                    new XYZ(candidate.BoundingBox.Max.X, candidate.BoundingBox.Max.Y, candidate.BoundingBox.Max.Z)
                };
                foreach (XYZ pt in corners)
                {
                    double x = pt.DotProduct(viewRight);
                    double y = pt.DotProduct(viewUp);
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
                hostBox = new AnnotationBox(minX, minY, maxX, maxY);
            }

            int bestScore = -1;
            XYZ bestPos = null;

            // Test sliding positions if curve exists, otherwise just test midpoint.
            double[] testParams = (candidate.ElementCurve != null) 
                ? SmartTagTuning.SlidePointsAlongRun
                : new double[] { 0.5 };

            // Normally two passes: try WITH a leader first, and only fall back to a no-leader
            // position if nothing with a leader scores at all (see the early return below).
            // When the category has its Leader tick off, the leader pass is skipped entirely, so
            // the tag lands on the close-in no-leader offset - beside the element, which is the
            // point of the setting. Added v1.49.7 for accessories.
            bool leaderDisabledForCategory =
                !SmartTagSettingsTracker.ResolveUseLeader(settingsState, candidate.Category);

            bool[] leaderPasses = leaderDisabledForCategory
                ? new bool[] { false }
                : new bool[] { true, false };

            foreach (bool tryLeader in leaderPasses)
            {
                // A tag the user asked to have NO leader sits closer than the general offset
                // allows. Tuned live with Ajmal on 32 duct accessories (2026-08-18): 300mm read
                // as too far - about 6mm of gap on a 1:50 sheet before the tag's own width is
                // added - and 150mm was the value he approved.
                //
                // Only the DELIBERATE leader-off case gets it. The no-leader FALLBACK pass, used
                // when a leader position could not be found at all, keeps the general offset, so
                // nothing about duct or pipe tagging changes.
                double baseOff = tryLeader
                    ? fixedOffsetInternal * SmartTagTuning.LeaderOffsetMultiplier
                    : (leaderDisabledForCategory ? NoLeaderOffsetInternal : fixedOffsetInternal);

                foreach (double tParam in testParams)
                {
                    XYZ anchorPoint;
                    if (candidate.ElementCurve != null)
                    {
                        try { anchorPoint = candidate.ElementCurve.Evaluate(tParam, true); }
                        catch { anchorPoint = candidate.Midpoint; }
                    }
                    else
                    {
                        anchorPoint = candidate.Midpoint;
                    }

                    Dictionary<TagDirection, XYZ> positions = BuildCandidatePositions(
                        candidate, anchorPoint, baseOff, tagWidth, tagHeight, viewRight, viewUp);

                    // Score in direction priority order. A tag the user asked to have NO leader
                    // goes sideways first - "just place near the accessory sideways" - so the
                    // order becomes Right, Left, then the usual two. Everything after that is
                    // unchanged: the scorer still rates every direction for clashes and takes the
                    // best, so a clash on the right is answered by the left automatically, and if
                    // both sides are blocked it carries on into Top/Bottom exactly as before.
                    // Gated on the SETTING, not on tryLeader, so the existing no-leader FALLBACK
                    // pass (used when no leader position could be found) keeps its old order.
                    TagDirection[] priority = leaderDisabledForCategory
                        ? NoLeaderDirectionOrder
                        : GetDirectionPriority(candidate, preflight);

                    // A no-leader tag takes the FIRST direction that is genuinely clear - clear of
                    // other tags AND clear of the ducts and fittings, which the scorer below cannot
                    // see. Order is beside-first, then below, then above, so it only leaves the
                    // side of the element when there is really no room there.
                    if (leaderDisabledForCategory && modelObstacleIndex != null)
                    {
                        AnnotationBox ownModelBox = null;
                        // The obstacle boxes are this model's elements only - looking a linked
                        // element's id up there would find an unrelated host element.
                        if (modelObstacleBoxes != null && candidate.ElementId != null && !candidate.IsLinked)
                            modelObstacleBoxes.TryGetValue(candidate.ElementId, out ownModelBox);

                        XYZ clearPos = FindFirstClearNoLeaderPosition(
                            positions, priority, placementBoundary, annotations,
                            spatialIndex, modelObstacleIndex, ownModelBox, hostBox,
                            tagWidth, tagHeight, viewScale, viewRight, viewUp);

                        if (clearPos != null)
                        {
                            candidate.NeedsLeader = false;
                            return clearPos;
                        }

                        // Nothing was completely clear - fall through to the scorer, which still
                        // picks the least-bad of the four rather than refusing to tag.
                    }

                    foreach (TagDirection dir in priority)
                    {
                        XYZ pos;
                        if (!positions.TryGetValue(dir, out pos))
                            continue;

                        if (!IsWithinTagPlacementBoundary(pos, placementBoundary, viewRight, viewUp))
                            continue;

                        hadBoundaryCandidate = true;

                        int score = ScoreCandidatePosition(
                            pos, dir,
                            annotations, spatialIndex, tagWidth, tagHeight, viewScale, viewRight, viewUp, hostBox);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPos = pos;
                            candidate.NeedsLeader = tryLeader;
                        }

                        // Early exit if we find a perfect, non-clashing spot
                        if (score >= SmartTagTuning.GoodEnoughScore)
                        {
                            candidate.NeedsLeader = tryLeader;
                            return bestPos;
                        }
                    }
                }

                // If we found a valid position WITH a leader (score >= 0), don't bother doing the no-leader pass.
                if (tryLeader && bestScore >= 0 && bestPos != null)
                {
                    candidate.NeedsLeader = true;
                    return bestPos;
                }
            }

            if (bestScore >= 0 && bestPos != null)
                return bestPos;

            return null;
        }

        /// <summary>
        /// Stage 2: true when this element sits in a tight parallel group (SmartTagTuning) and another
        /// member of the group already has a tag.
        /// </summary>
        private static bool IsCoveredByTaggedGroup(TagCandidate candidate, SmartTagPlacementContext context)
        {
            List<TagCandidate> allCandidates = context.Candidates;
            AnnotationSpatialIndex candidateSpatialIndex = context.CandidateIndex;
            Dictionary<AnnotationBox, TagCandidate> candidateByBox = context.CandidateByBox;
            HashSet<TagCandidate> taggedGroupMembers = context.TaggedCandidates;

            // A parallel group is this run plus at least ParallelGroupMinNeighbours others within
            // ParallelGroupDistanceMm (both in SmartTagTuning).
            int parallelCount = 0;
            bool groupMemberAlreadyTagged = false;

            List<TagCandidate> nearbyByPosition = QueryNearbyCandidatesByPosition(
                candidateSpatialIndex, candidateByBox, allCandidates, candidate);

            foreach (TagCandidate other in nearbyByPosition)
            {
                // The candidate itself, not "same ElementId": a linked element can share its id with
                // one of this model's elements.
                if (ReferenceEquals(other, candidate))
                    continue;

                if (other.Midpoint == null || candidate.Midpoint == null)
                    continue;

                double dist = candidate.Midpoint.DistanceTo(other.Midpoint);
                if (dist <= ParallelGroupDistance)
                {
                    parallelCount++;
                    if (taggedGroupMembers.Contains(other))
                        groupMemberAlreadyTagged = true;
                }
            }

            return parallelCount >= SmartTagTuning.ParallelGroupMinNeighbours && groupMemberAlreadyTagged;
        }

        /// <summary>
        /// Stage 3, crowded area: a High priority element gets the spot with the least overlap even if
        /// it is not clear; Medium and Low ones are skipped. Returns null with skipReason left at None
        /// when nothing was decided here, so the caller reports "no clean space".
        /// </summary>
        private static XYZ TryCrowdedAreaSpot(
            TagCandidate candidate,
            SmartTagPlacementContext context,
            double fixedOffsetInternal,
            double tagWidth,
            double tagHeight,
            out TagSkipReason skipReason)
        {
            PreFlightResult preflight = context.Preflight;
            List<AnnotationBox> annotations = context.Annotations;
            AnnotationSpatialIndex spatialIndex = context.AnnotationIndex;
            AnnotationBox placementBoundary = context.PlacementBoundary;
            XYZ viewRight = context.ViewRight;
            XYZ viewUp = context.ViewUp;

            skipReason = TagSkipReason.None;


            if (candidate.Priority == TagPriority.High)
            {
                // Force tag at base offset — best available even if imperfect.
                Dictionary<TagDirection, XYZ> positions = BuildCandidatePositions(
                    candidate, candidate.Midpoint, fixedOffsetInternal, tagWidth, tagHeight, viewRight, viewUp);
                TagDirection[] priority = GetDirectionPriority(candidate, preflight);

                // Pick the direction with the least overlap (even if not perfect).
                XYZ leastBadPos = null;
                double leastOverlap = double.MaxValue;
                bool hasBoundaryPosition = false;

                foreach (TagDirection dir in priority)
                {
                    XYZ pos;
                    if (!positions.TryGetValue(dir, out pos))
                        continue;

                    if (!IsWithinTagPlacementBoundary(pos, placementBoundary, viewRight, viewUp))
                        continue;

                    hasBoundaryPosition = true;

                    AnnotationBox proposed = CreateCandidateBoxInViewPlane(
                        pos, tagWidth, tagHeight, viewRight, viewUp);

                    List<AnnotationBox> nearbyAnnotations = QueryNearbyAnnotations(
                        spatialIndex,
                        annotations,
                        proposed);

                    double totalOverlap = 0;
                    foreach (AnnotationBox existing in nearbyAnnotations)
                        totalOverlap += proposed.OverlapArea(existing);

                    if (totalOverlap < leastOverlap)
                    {
                        leastOverlap = totalOverlap;
                        leastBadPos = pos;
                    }
                }

                if (leastBadPos != null)
                    return leastBadPos;

                if (!hasBoundaryPosition)
                {
                    skipReason = TagSkipReason.OutsideCropRegion;
                    return null;
                }
            }
            else
            {
                // MEDIUM and LOW priority in dense zones: skip.
                skipReason = TagSkipReason.DenseZoneSkipped;
                return null;
            }

            return null;
        }
    }
}
