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
using static AJTools.Services.SmartTag.SmartTagPlacementEngine;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagTypeSelector;
using static AJTools.Services.SmartTag.SmartTagViewCheck;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Decides where one tag goes - the heart of Smart MEP Tags.
    /// </summary>
    internal static class SmartTagPositionFinder
    {
        internal static readonly double ParallelGroupDistance = 200.0 * Constants.MM_TO_FEET; // 200mm

        /// <summary>
        /// Attempts to find the best tag position for a candidate.
        /// Tries the 4 cardinal positions at the configured offset,
        /// then applies parallel group and dense zone logic.
        /// Returns null if no valid position found.
        /// </summary>
        internal static XYZ FindBestTagPosition(
            TagCandidate candidate,
            List<AnnotationBox> annotations,
            AnnotationSpatialIndex spatialIndex,
            int viewScale,
            SmartTagSettingsState settingsState,
            Dictionary<BuiltInCategory, TagSizeHint> sizeHints,
            List<TagCandidate> allCandidates,
            AnnotationSpatialIndex candidateSpatialIndex,
            Dictionary<AnnotationBox, TagCandidate> candidateByBox,
            HashSet<TagCandidate> taggedGroupMembers,
            PreFlightResult preflight,
            AnnotationBox placementBoundary,
            AnnotationSpatialIndex modelObstacleIndex,
            Dictionary<ElementId, AnnotationBox> modelObstacleBoxes,
            XYZ viewRight,
            XYZ viewUp,
            out TagSkipReason skipReason)
        {
            skipReason = TagSkipReason.None;

            if (candidate == null)
            {
                skipReason = TagSkipReason.NoCleanSpaceAvailable;
                return null;
            }

            double fixedOffsetInternal = SmartTagSettingsTracker.ResolveOffsetInternal(settingsState, candidate.Category);

            double tagWidth;
            double tagHeight;
            ResolveTagSizeForCandidate(candidate, sizeHints, viewScale, out tagWidth, out tagHeight);
            bool hadBoundaryCandidate = false;

            // ── STEP 1: Try multiple candidate positions along the curve ──
            {
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
                    ? new double[] { 0.5, 0.25, 0.75 } 
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
                        ? fixedOffsetInternal * 3.0
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
                            if (score >= 60)
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
            }

            if (!hadBoundaryCandidate)
            {
                skipReason = TagSkipReason.OutsideCropRegion;
                return null;
            }

            // ── STEP 2: Parallel group handling ──
            // If this element is part of a parallel group (3+ elements within 200mm),
            // and another group member is already tagged, skip this one.
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

            if (parallelCount >= 2 && groupMemberAlreadyTagged)
            {
                skipReason = TagSkipReason.PartOfTaggedGroup;
                return null;
            }

            // ── STEP 3: Dense zone override ──
            if (candidate.IsDenseZone)
            {
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
            }

            // ── FINAL FAILURE ──
            skipReason = TagSkipReason.NoCleanSpaceAvailable;
            return null;
        }
    }
}
