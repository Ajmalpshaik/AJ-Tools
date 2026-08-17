// Tool Name: Smart Tag Placement Engine
// Description: Phases 3â€“6 â€” scoring, clash detection, repositioning, and tag placement.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models.SmartTag, AJTools.Utils

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;
using AJTools.Services.LeaderLogic;
using AJTools.Services.TagClash;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Handles tag position scoring, clash detection, repositioning, and placement.
    /// Processes candidates in priority order â€” HIGH first, then MEDIUM, then LOW.
    /// Each placed tag updates the annotation registry so subsequent tags avoid clashing.
    /// </summary>
    internal static class SmartTagPlacementEngine
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PHASE 3 â€” SMART TAG PLACEMENT SCORING
        // Generates 4 candidate positions around each element and scores
        // them based on free space, distance, alignment, and leader length.
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// The four cardinal directions for candidate tag positions.
        /// </summary>
        private enum TagDirection { Top, Bottom, Right, Left }
        private const double SpatialIndexCellSizeMm = 20.0;

        private sealed class TagSizeHint
        {
            public TagSizeHint(double width, double height)
            {
                Width = width;
                Height = height;
            }

            public double Width { get; private set; }
            public double Height { get; private set; }
        }

        /// <summary>
        /// Generates 4 candidate tag head positions around an element midpoint.
        /// The configured offset is interpreted as host-to-text-edge distance.
        /// Candidate head positions are shifted by half tag size so the nearest
        /// text edge sits at the configured offset from the host.
        /// </summary>
        private static Dictionary<TagDirection, XYZ> GenerateCandidatePositions(
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
            double hostHalfW = hostWidth * 0.5;

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

        /// <summary>
        /// Returns the evaluation order for candidate positions based on element orientation.
        /// Horizontal elements check top/bottom first; vertical check left/right first;
        /// Equipment prefers top-right.
        /// </summary>
        private static TagDirection[] GetDirectionPriority(
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
        /// </remarks>
        private static readonly TagDirection[] NoLeaderDirectionOrder =
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
        private static readonly double NoLeaderOffsetInternal = 150.0 * Constants.MM_TO_FEET;

        /// <summary>
        /// Model elements the tag should not be dropped on top of - ducts, fittings, accessories and
        /// equipment as they appear in this view.
        /// </summary>
        /// <remarks>
        /// The scorer has never seen model geometry: it weighs a position against other TAGS, text
        /// and dimensions only. That is why a no-leader tag could land squarely on a duct junction
        /// and score perfectly - as far as the engine knew, that spot was empty. Built once per run,
        /// and only when some category actually has its leader switched off, so a normal run pays
        /// nothing for it.
        /// </remarks>
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
        /// The element's OWN box is expected to be in the obstacle index and is not excluded - it
        /// cannot cause a false block, because every candidate position already sits a full offset
        /// clear of the host by construction (GenerateCandidatePositions adds hostHalfW).
        /// </remarks>
        private static XYZ FindFirstClearNoLeaderPosition(
            Dictionary<TagDirection, XYZ> positions,
            TagDirection[] order,
            AnnotationBox placementBoundary,
            List<AnnotationBox> annotations,
            AnnotationSpatialIndex annotationIndex,
            AnnotationSpatialIndex modelIndex,
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

                if (OverlapsAnything(proposed, annotationIndex, annotations, clearanceFeet))
                    continue;

                if (OverlapsAnything(proposed, modelIndex, null, clearanceFeet))
                    continue;

                return pos;
            }

            return null;
        }

        /// <summary>
        /// True when the box touches anything in the index, within the given clearance.
        /// </summary>
        private static bool OverlapsAnything(
            AnnotationBox proposed,
            AnnotationSpatialIndex index,
            List<AnnotationBox> fallbackList,
            double clearanceFeet)
        {
            AnnotationBox grown = proposed.Inflated(clearanceFeet);

            if (index != null)
            {
                foreach (AnnotationBox other in index.Query(grown))
                {
                    if (other != null && other.Overlaps(grown))
                        return true;
                }

                return false;
            }

            if (fallbackList != null)
            {
                foreach (AnnotationBox other in fallbackList)
                {
                    if (other != null && other.Overlaps(grown))
                        return true;
                }
            }

            return false;
        }

        private static AnnotationSpatialIndex BuildModelObstacleIndex(
            Document doc, View view, XYZ viewRight, XYZ viewUp, int viewScale)
        {
            var boxes = new List<AnnotationBox>();

            var categories = new[]
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_DuctAccessory,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_CableTrayFitting,
                BuiltInCategory.OST_MechanicalEquipment
            };

            foreach (BuiltInCategory category in categories)
            {
                try
                {
                    foreach (Element element in new FilteredElementCollector(doc, view.Id)
                        .OfCategory(category).WhereElementIsNotElementType())
                    {
                        if (element == null)
                            continue;

                        try
                        {
                            BoundingBoxXYZ box = element.get_BoundingBox(view);
                            if (box == null)
                                continue;

                            AnnotationBox viewBox = TagViewGeometry.ToViewBox(box, viewRight, viewUp);
                            if (viewBox != null)
                                boxes.Add(viewBox);
                        }
                        catch (Exception)
                        {
                            // An element that will not report a box simply is not an obstacle.
                        }
                    }
                }
                catch (Exception)
                {
                    // A category that cannot be collected in this view contributes nothing.
                }
            }

            return BuildAnnotationSpatialIndex(boxes, viewScale);
        }
        /// <remarks>
        /// RESTRICTS rather than reorders, and that distinction is the whole fix (v1.49.9). v1.49.7
        /// merely put these two first in the priority list, which decided nothing: the scoring loop
        /// keeps the HIGHEST-scoring direction, not the first one it tries, so "below" - usually the
        /// emptiest space around a duct - still won and accessory tags kept landing underneath. Order
        /// only ever mattered for an exact tie or the score >= 60 early exit. Handing the loop just
        /// these two is what actually forces the choice; the scorer then picks whichever side is
        /// clearer, which is also what makes "if one side clashes, use the other" work.
        ///
        /// Left/right in VIEW space regardless of which way the duct runs - Ajmal's explicit choice
        /// (2026-08-17) after being shown that on a duct running left-right this puts the tag at the
        /// accessory's connector ends. He wants the tags in one straight column down the sheet more
        /// than he wants them off the connectors.
        ///
        /// Both positions are a pure viewRight offset from the element midpoint, so the tag comes out
        /// exactly level with the accessory centre - the alignment asked for, already free.
        /// </remarks>

        private static bool IsPlanView(ViewType viewType)
        {
            return viewType == ViewType.FloorPlan || viewType == ViewType.CeilingPlan;
        }

        private static TagDirection[] GetDirectionPriority(
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

        private static Dictionary<TagDirection, XYZ> BuildCandidatePositions(
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

        private static XYZ GetViewRightDirection(View view)
        {
            try
            {
                if (view != null && view.RightDirection != null && view.RightDirection.GetLength() > Constants.ZERO_LENGTH_TOLERANCE)
                    return view.RightDirection.Normalize();
            }
            catch (Exception) { }

            return XYZ.BasisX;
        }

        private static XYZ GetViewUpDirection(View view)
        {
            try
            {
                if (view != null && view.UpDirection != null && view.UpDirection.GetLength() > Constants.ZERO_LENGTH_TOLERANCE)
                    return view.UpDirection.Normalize();
            }
            catch (Exception) { }

            return XYZ.BasisY;
        }

        // Thin wrapper kept so the eight call sites below read unchanged; the maths itself now lives
        // once, in TagViewGeometry.
        private static UV ProjectToViewPlane(XYZ point, XYZ viewRight, XYZ viewUp)
        {
            return TagViewGeometry.ProjectToView(point, viewRight, viewUp);
        }

        private static AnnotationBox CreateCandidateBoxInViewPlane(
            XYZ candidatePos,
            double estimatedTagWidth,
            double estimatedTagHeight,
            XYZ viewRight,
            XYZ viewUp)
        {
            UV uv = ProjectToViewPlane(candidatePos, viewRight, viewUp);
            double halfW = estimatedTagWidth * 0.5;
            double halfH = estimatedTagHeight * 0.5;
            return new AnnotationBox(
                uv.U - halfW, uv.V - halfH,
                uv.U + halfW, uv.V + halfH);
        }

        // Thin wrapper kept so the eight call sites below read unchanged; the eight-corner walk (one of
        // six copies of it that were in this project) now lives once, in TagViewGeometry.
        private static AnnotationBox ConvertBoundingBoxToViewPlane(BoundingBoxXYZ bb, XYZ viewRight, XYZ viewUp)
        {
            return TagViewGeometry.ToViewBox(bb, viewRight, viewUp);
        }

#if DEBUG
        internal static bool TryRunGeometryRegressionChecks(out string error)
        {
            error = null;
            const double tol = 1e-6;

            try
            {
                var rotated = new BoundingBoxXYZ
                {
                    Min = new XYZ(0, 0, 0),
                    Max = new XYZ(2, 1, 0),
                    Transform = Transform.CreateRotationAtPoint(XYZ.BasisZ, 0.5 * Math.PI, XYZ.Zero)
                };

                AnnotationBox rotatedBox = ConvertBoundingBoxToViewPlane(rotated, XYZ.BasisX, XYZ.BasisY);
                if (rotatedBox == null)
                {
                    error = "ConvertBoundingBoxToViewPlane returned null for rotated bbox.";
                    return false;
                }

                if (!NearlyEqual(rotatedBox.MinX, -1, tol)
                    || !NearlyEqual(rotatedBox.MaxX, 0, tol)
                    || !NearlyEqual(rotatedBox.MinY, 0, tol)
                    || !NearlyEqual(rotatedBox.MaxY, 2, tol))
                {
                    error = "Rotated bbox projection mismatch.";
                    return false;
                }

                var translated = new BoundingBoxXYZ
                {
                    Min = new XYZ(0, 0, 0),
                    Max = new XYZ(2, 1, 0),
                    Transform = Transform.CreateTranslation(new XYZ(3, -2, 0))
                };

                AnnotationBox translatedBox = ConvertBoundingBoxToViewPlane(translated, XYZ.BasisX, XYZ.BasisY);
                if (translatedBox == null)
                {
                    error = "ConvertBoundingBoxToViewPlane returned null for translated bbox.";
                    return false;
                }

                if (!NearlyEqual(translatedBox.MinX, 3, tol)
                    || !NearlyEqual(translatedBox.MaxX, 5, tol)
                    || !NearlyEqual(translatedBox.MinY, -2, tol)
                    || !NearlyEqual(translatedBox.MaxY, -1, tol))
                {
                    error = "Translated bbox projection mismatch.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool NearlyEqual(double a, double b, double tolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }
#endif

        private static AnnotationBox BuildPlacementBoundaryInViewPlane(
            PreFlightResult preflight,
            XYZ viewRight,
            XYZ viewUp)
        {
            if (preflight == null)
                return null;

            Outline boundary = preflight.AnnotationCropOutline ?? preflight.CropOutline;
            if (boundary == null)
                return null;

            XYZ min = boundary.MinimumPoint;
            XYZ max = boundary.MaximumPoint;
            XYZ[] corners = new[]
            {
                new XYZ(min.X, min.Y, min.Z),
                new XYZ(min.X, min.Y, max.Z),
                new XYZ(min.X, max.Y, min.Z),
                new XYZ(min.X, max.Y, max.Z),
                new XYZ(max.X, min.Y, min.Z),
                new XYZ(max.X, min.Y, max.Z),
                new XYZ(max.X, max.Y, min.Z),
                new XYZ(max.X, max.Y, max.Z)
            };

            double minU = double.MaxValue;
            double minV = double.MaxValue;
            double maxU = double.MinValue;
            double maxV = double.MinValue;

            foreach (XYZ corner in corners)
            {
                UV cornerUv = ProjectToViewPlane(corner, viewRight, viewUp);
                if (cornerUv.U < minU) minU = cornerUv.U;
                if (cornerUv.V < minV) minV = cornerUv.V;
                if (cornerUv.U > maxU) maxU = cornerUv.U;
                if (cornerUv.V > maxV) maxV = cornerUv.V;
            }

            if (minU > maxU || minV > maxV)
                return null;

            return new AnnotationBox(minU, minV, maxU, maxV);
        }

        private static bool IsWithinTagPlacementBoundary(
            XYZ point,
            AnnotationBox placementBoundary,
            XYZ viewRight,
            XYZ viewUp)
        {
            if (point == null || placementBoundary == null)
                return true;

            UV pointUv = ProjectToViewPlane(point, viewRight, viewUp);
            return pointUv.U >= placementBoundary.MinX
                && pointUv.U <= placementBoundary.MaxX
                && pointUv.V >= placementBoundary.MinY
                && pointUv.V <= placementBoundary.MaxY;
        }

        private static AnnotationSpatialIndex BuildAnnotationSpatialIndex(
            List<AnnotationBox> annotations,
            int viewScale)
        {
            try
            {
                int safeScale = Math.Max(1, viewScale);
                double cellSize = SpatialIndexCellSizeMm * Constants.MM_TO_FEET * safeScale;
                var index = new AnnotationSpatialIndex(cellSize);
                index.AddRange(annotations);
                return index;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static List<AnnotationBox> QueryNearbyAnnotations(
            AnnotationSpatialIndex spatialIndex,
            List<AnnotationBox> allAnnotations,
            AnnotationBox searchArea)
        {
            if (searchArea == null)
                return allAnnotations ?? new List<AnnotationBox>();

            if (spatialIndex != null)
                return spatialIndex.Query(searchArea);

            return allAnnotations ?? new List<AnnotationBox>();
        }

        /// <summary>
        /// Indexes every candidate's Midpoint (X/Y only) for the parallel-group proximity check in
        /// FindBestTagPosition, so it doesn't have to scan every other candidate for every candidate
        /// (same pattern as BuildAnnotationSpatialIndex/QueryNearbyAnnotations above). Falls back to
        /// null on any failure - callers fall back to a full scan, same as the annotation index does.
        /// </summary>
        private static AnnotationSpatialIndex BuildCandidateSpatialIndex(
            List<TagCandidate> candidates,
            out Dictionary<AnnotationBox, TagCandidate> candidateByBox)
        {
            candidateByBox = new Dictionary<AnnotationBox, TagCandidate>();
            try
            {
                var index = new AnnotationSpatialIndex(ParallelGroupDistance);
                foreach (TagCandidate candidate in candidates)
                {
                    if (candidate == null || candidate.Midpoint == null)
                        continue;

                    var box = new AnnotationBox(
                        candidate.Midpoint.X, candidate.Midpoint.Y,
                        candidate.Midpoint.X, candidate.Midpoint.Y);
                    candidateByBox[box] = candidate;
                    index.Add(box);
                }

                return index;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Candidates within ParallelGroupDistance (X/Y) of the given candidate, per the same
        /// coarse-index-then-exact-3D-check reasoning as MarkDenseZones in SmartMepTagService: X/Y
        /// distance can never exceed full 3D distance, so any pair within ParallelGroupDistance in 3D
        /// is guaranteed to also be within ParallelGroupDistance in X/Y and therefore returned here -
        /// the caller still runs the exact 3D DistanceTo check on every result. Falls back to the full
        /// candidate list (original O(n) per-candidate behavior) if the index isn't available.
        /// </summary>
        private static List<TagCandidate> QueryNearbyCandidatesByPosition(
            AnnotationSpatialIndex candidateSpatialIndex,
            Dictionary<AnnotationBox, TagCandidate> candidateByBox,
            List<TagCandidate> allCandidates,
            TagCandidate candidate)
        {
            if (candidateSpatialIndex == null || candidateByBox == null || candidate == null || candidate.Midpoint == null)
                return allCandidates;

            AnnotationBox queryRegion = new AnnotationBox(
                candidate.Midpoint.X - ParallelGroupDistance, candidate.Midpoint.Y - ParallelGroupDistance,
                candidate.Midpoint.X + ParallelGroupDistance, candidate.Midpoint.Y + ParallelGroupDistance);

            var result = new List<TagCandidate>();
            foreach (AnnotationBox hitBox in candidateSpatialIndex.Query(queryRegion))
            {
                TagCandidate other;
                if (candidateByBox.TryGetValue(hitBox, out other))
                    result.Add(other);
            }

            return result;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // SCORING ENGINE
        // Each candidate position scored 0â€“100 across 4 criteria.
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Scores a candidate tag position on a 0â€“100 scale.
        /// Returns -1 if the position is disqualified (hard clash or leader too long).
        /// </summary>
        private static int ScoreCandidatePosition(
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

            // â”€â”€ CRITERION 1: FREE SPACE (0â€“40 pts) â”€â”€
            // No clash = 40 pts. Any overlap = disqualified.
            double tolerance = 1.5 * Constants.MM_TO_FEET * viewScale; // 1.5mm at view scale
            double nearMiss = 2.0 * Constants.MM_TO_FEET * viewScale;  // 2mm at view scale
            double threshold10mm = 10.0 * Constants.MM_TO_FEET * viewScale;

            if (hostBox != null && proposed.Inflated(tolerance).Overlaps(hostBox))
            {
                return -1; // Disqualified — clashes with its own host element.
            }

            double searchMargin = Math.Max(nearMiss, threshold10mm);
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
                return -1; // Disqualified â€” hard clash.

            totalScore += hasNearMiss ? 10 : 40;

            // â”€â”€ CRITERION 2: DISTANCE FROM OTHER TAGS (0â€“20 pts) â”€â”€
            double minDist = double.MaxValue;
            double threshold5mm = 5.0 * Constants.MM_TO_FEET * viewScale;

            foreach (AnnotationBox existing in nearbyAnnotations)
            {
                double dist = proposed.DistanceTo(existing);
                if (dist < minDist)
                    minDist = dist;
            }

            if (minDist > threshold10mm)
                totalScore += 20;
            else if (minDist >= threshold5mm)
                totalScore += 10;
            else
                return -1; // Too close â€” disqualified.

            // â”€â”€ CRITERION 3: ALIGNMENT PREFERENCE (0â€“20 pts) â”€â”€
            // Horizontal placement is always preferred for readability.
            if (direction == TagDirection.Top || direction == TagDirection.Bottom)
                totalScore += 20; // Horizontal â€” best readability.
            else
                totalScore += 10; // Left/Right â€” acceptable but not ideal.

            // â”€â”€ CRITERION 4: LEADER CONSISTENCY (0â€“20 pts) â”€â”€
            // Max-leader filtering is intentionally disabled. Offset value should control placement.
            // So this is a FLAT 20 on every position, not a real criterion - the score is effectively
            // out of 80 with 20 free points, and the "score >= 60" early exit above is calibrated
            // against that. Left exactly as-is on purpose: removing the 20 would silently retune every
            // placement decision Smart MEP Tag makes. Only change it together with that threshold.
            totalScore += 20;

            return totalScore;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PHASE 4 â€” CLASH DETECTION
        // Bounding-box-based overlap detection for annotations.
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Collects all existing annotation bounding boxes in the view.
        /// Done ONCE at script start â€” includes tags, text notes, and dimensions.
        /// </summary>
        public static List<AnnotationBox> CollectExistingAnnotations(Document doc, View view)
        {
            var boxes = new List<AnnotationBox>();
            XYZ viewRight = GetViewRightDirection(view);
            XYZ viewUp = GetViewUpDirection(view);

            try
            {
                // Collect IndependentTags.
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                foreach (Element elem in tags)
                    AddAnnotationBox(elem, view, boxes, viewRight, viewUp);

                // Collect TextNotes.
                var texts = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(TextNote))
                    .WhereElementIsNotElementType();

                foreach (Element elem in texts)
                    AddAnnotationBox(elem, view, boxes, viewRight, viewUp);

                // Collect Dimensions. Category-based filter (not OfClass(typeof(Dimension))):
                // Revit 2025+ returns linear dimensions as the LinearDimension subclass, which an
                // exact-type OfClass filter misses.
                var dims = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Dimensions)
                    .WhereElementIsNotElementType();

                foreach (Element elem in dims)
                    AddAnnotationBox(elem, view, boxes, viewRight, viewUp);
            }
            catch (Exception)
            {
                // If annotation collection fails, return what we have.
            }

            return boxes;
        }

        /// <summary>
        /// Extracts the bounding box of an annotation element and adds it to the list.
        /// </summary>
        private static void AddAnnotationBox(
            Element elem,
            View view,
            List<AnnotationBox> boxes,
            XYZ viewRight,
            XYZ viewUp)
        {
            try
            {
                BoundingBoxXYZ bb = elem.get_BoundingBox(view);
                if (bb == null)
                    return;

                AnnotationBox box = ConvertBoundingBoxToViewPlane(bb, viewRight, viewUp);
                if (box != null)
                    boxes.Add(box);
            }
            catch (Exception)
            {
                // Skip elements whose bounding box can't be read.
            }
        }

        /// <summary>
        /// Fallback estimate when no sampled tag box is available.
        /// </summary>
        private static void EstimateFallbackTagSize(int viewScale, out double width, out double height)
        {
            // Typical MEP tag is roughly 15mm wide x 5mm tall at print scale.
            width = 15.0 * Constants.MM_TO_FEET * viewScale;
            height = 5.0 * Constants.MM_TO_FEET * viewScale;
        }

        private static Dictionary<BuiltInCategory, TagSizeHint> BuildTagSizeHints(
            Document doc,
            View view,
            IList<TagCandidate> candidates,
            XYZ viewRight,
            XYZ viewUp,
            int viewScale)
        {
            EstimateFallbackTagSize(viewScale, out double fallbackWidth, out double fallbackHeight);

            var neededCategories = new HashSet<BuiltInCategory>();
            if (candidates != null)
            {
                foreach (TagCandidate candidate in candidates)
                {
                    if (candidate != null)
                        neededCategories.Add(candidate.Category);
                }
            }

            var hints = new Dictionary<BuiltInCategory, TagSizeHint>();
            foreach (BuiltInCategory category in neededCategories)
                hints[category] = new TagSizeHint(fallbackWidth, fallbackHeight);

            if (doc == null || view == null || neededCategories.Count == 0)
                return hints;

            var samples = new Dictionary<BuiltInCategory, List<TagSizeHint>>();
            foreach (BuiltInCategory category in neededCategories)
                samples[category] = new List<TagSizeHint>();

            try
            {
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                foreach (Element element in tags)
                {
                    IndependentTag tag = element as IndependentTag;
                    if (tag == null)
                        continue;

                    if (!TryGetTaggedModelCategory(doc, tag, out BuiltInCategory modelCategory))
                        continue;

                    List<TagSizeHint> categorySamples;
                    if (!samples.TryGetValue(modelCategory, out categorySamples))
                        continue;

                    if (!TryGetTagTextBoxSizeInViewPlane(tag, view, viewRight, viewUp, out double width, out double height))
                        continue;

                    categorySamples.Add(new TagSizeHint(width, height));
                }
            }
            catch (Exception)
            {
                // Best effort; fallback hints remain in place.
            }

            foreach (KeyValuePair<BuiltInCategory, List<TagSizeHint>> kvp in samples)
            {
                List<TagSizeHint> list = kvp.Value;
                if (list == null || list.Count == 0)
                    continue;

                double avgWidth = list.Average(x => x.Width);
                double avgHeight = list.Average(x => x.Height);
                if (avgWidth > Constants.ZERO_LENGTH_TOLERANCE && avgHeight > Constants.ZERO_LENGTH_TOLERANCE)
                    hints[kvp.Key] = new TagSizeHint(avgWidth, avgHeight);
            }

            return hints;
        }

        private static bool TryGetTaggedModelCategory(
            Document doc,
            IndependentTag tag,
            out BuiltInCategory category)
        {
            category = BuiltInCategory.INVALID;
            if (doc == null || tag == null)
                return false;

            try
            {
                ElementId taggedId = TagCompat.GetTaggedLocalElementId(tag);
                if (taggedId == null || taggedId == ElementId.InvalidElementId)
                    return false;

                Element tagged = doc.GetElement(taggedId);
                if (tagged?.Category == null)
                    return false;

                category = (BuiltInCategory)tagged.Category.Id.IntValue();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void ResolveTagSizeForCandidate(
            TagCandidate candidate,
            Dictionary<BuiltInCategory, TagSizeHint> sizeHints,
            int viewScale,
            out double width,
            out double height)
        {
            if (candidate != null
                && sizeHints != null
                && sizeHints.TryGetValue(candidate.Category, out TagSizeHint hint)
                && hint != null
                && hint.Width > Constants.ZERO_LENGTH_TOLERANCE
                && hint.Height > Constants.ZERO_LENGTH_TOLERANCE)
            {
                width = hint.Width;
                height = hint.Height;
                return;
            }

            EstimateFallbackTagSize(viewScale, out width, out height);
        }

        private static bool TryGetTagTextBoxSizeInViewPlane(
            IndependentTag tag,
            View view,
            XYZ viewRight,
            XYZ viewUp,
            out double width,
            out double height)
        {
            width = 0;
            height = 0;
            if (tag == null || view == null)
                return false;

            // The bounding-box read, the eight-corner projection and the re-centre-on-the-head trick
            // that used to be written out here are all in TagViewGeometry.GetTagTextBox now - this was
            // the third copy of that trick in the project.
            AnnotationBox box = TagViewGeometry.GetTagTextBox(tag, view, viewRight, viewUp);
            if (box == null)
                return false;

            width = box.MaxX - box.MinX;
            height = box.MaxY - box.MinY;
            return width > Constants.ZERO_LENGTH_TOLERANCE
                && height > Constants.ZERO_LENGTH_TOLERANCE;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PHASE 5 â€” REPOSITION & FAILURE LOGIC
        // If all 4 positions fail at the configured offset, apply group/dense fallbacks.
        // Handle parallel groups and dense zones.
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static readonly double ParallelGroupDistance = 200.0 * Constants.MM_TO_FEET; // 200mm
        private const double ElbowOutsideTextMarginMm = 3.0;

        /// <summary>
        /// Attempts to find the best tag position for a candidate.
        /// Tries the 4 cardinal positions at the configured offset,
        /// then applies parallel group and dense zone logic.
        /// Returns null if no valid position found.
        /// </summary>
        private static XYZ FindBestTagPosition(
            TagCandidate candidate,
            List<AnnotationBox> annotations,
            AnnotationSpatialIndex spatialIndex,
            int viewScale,
            SmartTagSettingsState settingsState,
            Dictionary<BuiltInCategory, TagSizeHint> sizeHints,
            List<TagCandidate> allCandidates,
            AnnotationSpatialIndex candidateSpatialIndex,
            Dictionary<AnnotationBox, TagCandidate> candidateByBox,
            HashSet<ElementId> taggedGroupMembers,
            PreFlightResult preflight,
            AnnotationBox placementBoundary,
            AnnotationSpatialIndex modelObstacleIndex,
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

            // â”€â”€ STEP 1: Try multiple candidate positions along the curve â”€â”€
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
                            XYZ clearPos = FindFirstClearNoLeaderPosition(
                                positions, priority, placementBoundary, annotations,
                                spatialIndex, modelObstacleIndex,
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

            // â”€â”€ STEP 2: Parallel group handling â”€â”€
            // If this element is part of a parallel group (3+ elements within 200mm),
            // and another group member is already tagged, skip this one.
            int parallelCount = 0;
            bool groupMemberAlreadyTagged = false;

            List<TagCandidate> nearbyByPosition = QueryNearbyCandidatesByPosition(
                candidateSpatialIndex, candidateByBox, allCandidates, candidate);

            foreach (TagCandidate other in nearbyByPosition)
            {
                if (other.ElementId == candidate.ElementId)
                    continue;

                if (other.Midpoint == null || candidate.Midpoint == null)
                    continue;

                double dist = candidate.Midpoint.DistanceTo(other.Midpoint);
                if (dist <= ParallelGroupDistance)
                {
                    parallelCount++;
                    if (taggedGroupMembers.Contains(other.ElementId))
                        groupMemberAlreadyTagged = true;
                }
            }

            if (parallelCount >= 2 && groupMemberAlreadyTagged)
            {
                skipReason = TagSkipReason.PartOfTaggedGroup;
                return null;
            }

            // â”€â”€ STEP 3: Dense zone override â”€â”€
            if (candidate.IsDenseZone)
            {
                if (candidate.Priority == TagPriority.High)
                {
                    // Force tag at base offset â€” best available even if imperfect.
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

            // â”€â”€ FINAL FAILURE â”€â”€
            skipReason = TagSkipReason.NoCleanSpaceAvailable;
            return null;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PHASE 6 â€” TAG PLACEMENT EXECUTION
        // Places each tag in its own transaction using IndependentTag.Create().
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
            foreach (TagCandidate probe in candidates)
            {
                if (probe != null && !SmartTagSettingsTracker.ResolveUseLeader(settingsState, probe.Category))
                {
                    modelObstacleIndex = BuildModelObstacleIndex(doc, activeView, viewRight, viewUp, viewScale);
                    break;
                }
            }

            // Track which elements have been successfully tagged â€” used for parallel group logic.
            var taggedGroupMembers = new HashSet<ElementId>();

            // Process in priority order (candidates are already sorted HIGH â†’ MEDIUM â†’ LOW).
            foreach (TagCandidate candidate in candidates)
            {
                if (candidate == null)
                    continue;

                if (candidate.TagTypeId == null || candidate.TagTypeId == ElementId.InvalidElementId)
                {
                    results.Add(new TagPlacementResult
                    {
                        ElementId = candidate.ElementId,
                        Category = candidate.Category,
                        Success = false,
                        SkipReason = TagSkipReason.NoTagFamilyAvailable,
                        Note = "Tag type ID was not resolved"
                    });
                    continue;
                }

                // â”€â”€ Find best position (Phases 3â€“5) â”€â”€
                TagSkipReason skipReason;
                XYZ tagPosition = FindBestTagPosition(
                    candidate, annotations, annotationSpatialIndex, viewScale, settingsState, sizeHints,
                    candidates, candidateSpatialIndex, candidateByBox, taggedGroupMembers,
                    preflight, placementBoundary, modelObstacleIndex, viewRight, viewUp,
                    out skipReason);

                if (tagPosition == null)
                {
                    results.Add(new TagPlacementResult
                    {
                        ElementId = candidate.ElementId,
                        Category = candidate.Category,
                        Success = false,
                        SkipReason = skipReason
                    });
                    continue;
                }

                // â”€â”€ Place the tag in its own transaction â”€â”€
                bool placed = PlaceSingleTag(
                    doc, activeView, candidate, tagPosition, annotations, annotationSpatialIndex, viewScale, viewRight, viewUp, leaderLogic);

                if (placed)
                {
                    taggedGroupMembers.Add(candidate.ElementId);
                    results.Add(new TagPlacementResult
                    {
                        ElementId = candidate.ElementId,
                        Category = candidate.Category,
                        Success = true,
                        SkipReason = TagSkipReason.None
                    });
                }
                else
                {
                    results.Add(new TagPlacementResult
                    {
                        ElementId = candidate.ElementId,
                        Category = candidate.Category,
                        Success = false,
                        SkipReason = TagSkipReason.NoCleanSpaceAvailable,
                        Note = "Tag placement transaction failed"
                    });
                }
            }
        }

        /// <summary>
        /// Places a single tag inside its own transaction.
        /// After placement, registers the new tag's bounding box in the annotation list
        /// so subsequent tags avoid clashing with it.
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

        private static bool PlaceSingleTag(
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
                        Element targetElement = doc.GetElement(candidate.ElementId);
                        if (targetElement == null)
                        {
                            t.RollBack();
                            return false;
                        }

                        Reference elemRef = new Reference(targetElement);

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

        // Removed 2026-08-16 (about 210 lines): TryResolveLeaderEndByRollbackProbe,
        // AdjustElbowOutsideTextBoundsRight, GetScaledElbowOutsideMarginFeet, TryGetTagBoundsInView,
        // GetTagBoundingBox, IsPointInsideBounds and TrySetLeaderElbowPreserveCondition all lived
        // here. Every one of them was a private copy of something another tag tool also had its own
        // copy of - the rollback probe was byte-identical to StackTagsService's, and the bounds and
        // elbow-nudge helpers were near-verbatim in ForceTagLeaderLShapeService. They now live once,
        // in Services/LeaderLogic/TagLeaderService.cs and Services/TagClash/TagViewGeometry.cs, and
        // ApplyLeaderBehavior above calls them. Behaviour is unchanged.
    }

}


