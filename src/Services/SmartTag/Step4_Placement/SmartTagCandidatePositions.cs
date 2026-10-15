#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, Spots Around An Element
 * File Name     : SmartTagCandidatePositions.cs
 * Purpose       : Step 4: the spots tried around an element (above, below, right, left), the order they are
 *                 tried in, and the first-clear-spot search used for tags with no leader.
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
 * Input         : Element midpoint, offset, tag size, view directions.
 * Output        : Candidate tag head points.
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
using static AJTools.Services.SmartTag.SmartTagCreator;
using static AJTools.Services.SmartTag.SmartTagElementCollector;
using static AJTools.Services.SmartTag.SmartTagElementFilters;
using static AJTools.Services.SmartTag.SmartTagElementGeometry;
using static AJTools.Services.SmartTag.SmartTagPlacementEngine;
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
    /// The spots tried around an element and the order they are tried in.
    /// </summary>
    internal static class SmartTagCandidatePositions
    {
        /// <summary>
        /// The four cardinal directions for candidate tag positions.
        /// </summary>
        internal enum TagDirection { Top, Bottom, Right, Left }

        /// <summary>
        /// Generates 4 candidate tag head positions around an element midpoint.
        /// The configured offset is interpreted as host-to-text-edge distance.
        /// Candidate head positions are shifted by half tag size so the nearest
        /// text edge sits at the configured offset from the host.
        /// </summary>
        internal static Dictionary<TagDirection, XYZ> GenerateCandidatePositions(
            XYZ midpoint,
            double offsetFromHostToTextEdge,
            double estimatedTagWidth,
            double estimatedTagHeight,
            XYZ viewRight,
            XYZ viewUp,
            double hostWidth)
        {
            // Safety fallback if settings are missing/invalid.
            if (offsetFromHostToTextEdge <= Constants.ZERO_LENGTH_TOLERANCE)
                offsetFromHostToTextEdge = 100.0 * Constants.MM_TO_FEET;

            if (estimatedTagWidth <= Constants.ZERO_LENGTH_TOLERANCE)
                estimatedTagWidth = 15.0 * Constants.MM_TO_FEET;
            if (estimatedTagHeight <= Constants.ZERO_LENGTH_TOLERANCE)
                estimatedTagHeight = 5.0 * Constants.MM_TO_FEET;

            double halfW = estimatedTagWidth * 0.5;
            double halfH = estimatedTagHeight * 0.5;
            // GetDuctWidth returns -1 when a duct's size cannot be read; never let that pull the tag
            // closer than the offset (Create Tags clamps the same way).
            double hostHalfW = Math.Max(0.0, hostWidth) * 0.5;

            double topHeadDistance = hostHalfW + offsetFromHostToTextEdge + halfH;
            double bottomHeadDistance = hostHalfW + offsetFromHostToTextEdge + halfH;
            double rightHeadDistance = hostHalfW + offsetFromHostToTextEdge + halfW;
            double leftHeadDistance = hostHalfW + offsetFromHostToTextEdge + halfW;

            return new Dictionary<TagDirection, XYZ>
            {
                { TagDirection.Top,    midpoint.Add(viewUp.Multiply(topHeadDistance)) },
                { TagDirection.Bottom, midpoint.Subtract(viewUp.Multiply(bottomHeadDistance)) },
                { TagDirection.Right,  midpoint.Add(viewRight.Multiply(rightHeadDistance)) },
                { TagDirection.Left,   midpoint.Subtract(viewRight.Multiply(leftHeadDistance)) }
            };
        }

        internal static Dictionary<TagDirection, XYZ> BuildCandidatePositions(
            TagCandidate candidate,
            XYZ anchorPoint,
            double offset,
            double estimatedTagWidth,
            double estimatedTagHeight,
            XYZ viewRight,
            XYZ viewUp)
        {
            if (candidate == null || anchorPoint == null)
                return new Dictionary<TagDirection, XYZ>();

            Dictionary<TagDirection, XYZ> positions = GenerateCandidatePositions(
                anchorPoint, offset, estimatedTagWidth, estimatedTagHeight, viewRight, viewUp, candidate.HostWidth);

            return positions;
        }

        /// <summary>
        /// Returns the evaluation order for candidate positions based on element orientation.
        /// Horizontal elements check top/bottom first; vertical check left/right first;
        /// Equipment prefers top-right.
        /// </summary>
        internal static TagDirection[] GetDirectionPriority(
            ElementOrientation orientation, BuiltInCategory category)
        {
            if (category == BuiltInCategory.OST_MechanicalEquipment)
                return new[] { TagDirection.Top, TagDirection.Right, TagDirection.Bottom, TagDirection.Left };

            switch (orientation)
            {
                case ElementOrientation.Vertical:
                    return new[] { TagDirection.Left, TagDirection.Right, TagDirection.Top, TagDirection.Bottom };
                default: // Horizontal or Other
                    return new[] { TagDirection.Top, TagDirection.Bottom, TagDirection.Right, TagDirection.Left };
            }
        }

        internal static TagDirection[] GetDirectionPriority(
            TagCandidate candidate,
            PreFlightResult preflight)
        {
            if (candidate == null || preflight == null)
                return new[] { TagDirection.Top, TagDirection.Bottom, TagDirection.Right, TagDirection.Left };

            TagDirection[] basePriority = GetDirectionPriority(candidate.Orientation, candidate.Category);

            if (IsPlanView(preflight.ViewType))
            {
                if (candidate.Orientation == ElementOrientation.Horizontal)
                    return new[] { TagDirection.Top, TagDirection.Bottom };

                if (candidate.Orientation == ElementOrientation.Vertical)
                    return new[] { TagDirection.Left, TagDirection.Right };
            }

            return basePriority;
        }

        internal static bool IsPlanView(ViewType viewType)
        {
            return viewType == ViewType.FloorPlan || viewType == ViewType.CeilingPlan;
        }

        /// <summary>
        /// Where a no-leader tag is allowed to go, in the order it is tried: beside the element
        /// first, and only above or below when both sides are blocked.
        /// </summary>
        /// <remarks>
        /// Right, Left, Bottom, Top (Ajmal, 2026-08-18, after seeing it live). Sideways is what he
        /// wants normally; below-then-above is the escape when a side has no room, and below comes
        /// first to match the rest of the suite, where a run lying horizontally is tagged below it.
        ///
        /// v1.49.9 restricted this to Right and Left only, which was right about the near case and
        /// wrong about the crowded one: on a junction the tag went sideways straight onto the fitting
        /// because there was nowhere else it was permitted to go.
        ///
        /// Right and Left are in VIEW space regardless of which way the duct runs - Ajmal's explicit
        /// choice (2026-08-17) after being shown that on a duct running left-right this puts the tag at
        /// the accessory's connector ends. He wants the tags in one straight column down the sheet more
        /// than he wants them off the connectors. Both are a pure viewRight offset from the element
        /// midpoint, so the tag comes out exactly level with the accessory centre.
        /// </remarks>
        internal static readonly TagDirection[] NoLeaderDirectionOrder =
            new[] { TagDirection.Right, TagDirection.Left, TagDirection.Bottom, TagDirection.Top };

        /// <summary>
        /// Gap between a no-leader tag and the element it belongs to, as a real distance in the model.
        /// </summary>
        /// <remarks>
        /// 150mm, approved by Ajmal on 2026-08-18 after seeing both 300mm and 150mm on 32 live duct
        /// accessories. A no-leader tag reads as belonging to the thing it sits next to, so it wants
        /// to be closer than a tag on a leader - the general 300mm offset put it about 6mm off on a
        /// 1:50 sheet before the tag's own width was even added, and he called it "very far".
        ///
        /// Kept separate from SmartTagSettingsTracker's general offset ON PURPOSE: that one is shared
        /// with every leader-carrying tag, and halving it would have moved every duct tag in the
        /// suite. He scoped this to accessories.
        /// </remarks>
        internal static readonly double NoLeaderOffsetInternal = 150.0 * Constants.MM_TO_FEET;

        /// <summary>
        /// Returns the first position in <paramref name="order"/> whose tag box is clear of every
        /// other tag AND of the model geometry, or null when none of them is.
        /// </summary>
        /// <remarks>
        /// First-clear-wins, not best-scoring. That distinction is the whole point: the scorer weighs
        /// free space and would happily rate "below" higher than "beside" on an open run, which is
        /// how accessory tags ended up under their elements instead of next to them. Here the order
        /// decides, and the checks only say yes or no.
        ///
        /// The element's OWN box is left out of the model-obstacle check. The gap to its own element
        /// is set on purpose - HostWidth plus the 150 mm offset puts the tag that far from the
        /// element's edge - while the clearance used against everything else is paper-based (1.5 mm
        /// on the sheet, so 150 mm at 1:100 and 300 mm at 1:200) and would otherwise eat that gap
        /// and block every side. A position that actually lands ON the element is still refused,
        /// by a direct check against <paramref name="hostBox"/>.
        /// </remarks>
        internal static XYZ FindFirstClearNoLeaderPosition(
            Dictionary<TagDirection, XYZ> positions,
            TagDirection[] order,
            AnnotationBox placementBoundary,
            List<AnnotationBox> annotations,
            AnnotationSpatialIndex annotationIndex,
            AnnotationSpatialIndex modelIndex,
            AnnotationBox ownModelBox,
            AnnotationBox hostBox,
            double tagWidth,
            double tagHeight,
            int viewScale,
            XYZ viewRight,
            XYZ viewUp)
        {
            if (positions == null || order == null)
                return null;

            double clearanceFeet = 1.5 * Constants.MM_TO_FEET * viewScale;

            foreach (TagDirection direction in order)
            {
                XYZ pos;
                if (!positions.TryGetValue(direction, out pos) || pos == null)
                    continue;

                if (!IsWithinTagPlacementBoundary(pos, placementBoundary, viewRight, viewUp))
                    continue;

                AnnotationBox proposed = CreateCandidateBoxInViewPlane(
                    pos, tagWidth, tagHeight, viewRight, viewUp);

                if (proposed == null)
                    continue;

                if (hostBox != null && proposed.Overlaps(hostBox))
                    continue;

                if (OverlapsAnything(proposed, annotationIndex, annotations, clearanceFeet))
                    continue;

                if (OverlapsAnything(proposed, modelIndex, null, clearanceFeet, ownModelBox))
                    continue;

                return pos;
            }

            return null;
        }

        /// <summary>
        /// True when the box touches anything in the index, within the given clearance.
        /// <paramref name="ignore"/>, when given, is one box in the index that does not count.
        /// </summary>
        internal static bool OverlapsAnything(
            AnnotationBox proposed,
            AnnotationSpatialIndex index,
            List<AnnotationBox> fallbackList,
            double clearanceFeet,
            AnnotationBox ignore = null)
        {
            AnnotationBox grown = proposed.Inflated(clearanceFeet);

            if (index != null)
            {
                foreach (AnnotationBox other in index.Query(grown))
                {
                    if (other != null && !ReferenceEquals(other, ignore) && other.Overlaps(grown))
                        return true;
                }

                return false;
            }

            if (fallbackList != null)
            {
                foreach (AnnotationBox other in fallbackList)
                {
                    if (other != null && !ReferenceEquals(other, ignore) && other.Overlaps(grown))
                        return true;
                }
            }

            return false;
        }
    }
}
