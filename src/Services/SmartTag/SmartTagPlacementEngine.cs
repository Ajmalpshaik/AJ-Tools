// Tool Name: Smart Tag Placement Engine
// Description: Phases 3â€“6 â€” scoring, clash detection, repositioning, and tag placement.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models.SmartTag, AJTools.Utils

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;
using AJTools.Services.LeaderLogic;
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
            XYZ viewUp)
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

            double topHeadDistance = offsetFromHostToTextEdge + halfH;
            double bottomHeadDistance = offsetFromHostToTextEdge + halfH;
            double rightHeadDistance = offsetFromHostToTextEdge + halfW;
            double leftHeadDistance = offsetFromHostToTextEdge + halfW;

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
            PreFlightResult preflight,
            double offset,
            double estimatedTagWidth,
            double estimatedTagHeight,
            XYZ viewRight,
            XYZ viewUp)
        {
            if (candidate == null || candidate.Midpoint == null)
                return new Dictionary<TagDirection, XYZ>();

            Dictionary<TagDirection, XYZ> positions = GenerateCandidatePositions(
                candidate.Midpoint, offset, estimatedTagWidth, estimatedTagHeight, viewRight, viewUp);

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

        private static UV ProjectToViewPlane(XYZ point, XYZ viewRight, XYZ viewUp)
        {
            if (point == null)
                return new UV(0, 0);

            double u = point.DotProduct(viewRight);
            double v = point.DotProduct(viewUp);
            return new UV(u, v);
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

        private static AnnotationBox ConvertBoundingBoxToViewPlane(BoundingBoxXYZ bb, XYZ viewRight, XYZ viewUp)
        {
            if (bb == null)
                return null;

            XYZ min = bb.Min;
            XYZ max = bb.Max;
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
                UV uv = ProjectToViewPlane(corner, viewRight, viewUp);
                if (uv.U < minU) minU = uv.U;
                if (uv.V < minV) minV = uv.V;
                if (uv.U > maxU) maxU = uv.U;
                if (uv.V > maxV) maxV = uv.V;
            }

            return new AnnotationBox(minU, minV, maxU, maxV);
        }

        private static bool IsWithinTagPlacementBoundary(
            XYZ point,
            PreFlightResult preflight,
            XYZ viewRight,
            XYZ viewUp)
        {
            if (point == null || preflight == null)
                return true;

            Outline boundary = preflight.AnnotationCropOutline ?? preflight.CropOutline;
            if (boundary == null)
                return true;

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

            UV pointUv = ProjectToViewPlane(point, viewRight, viewUp);
            return pointUv.U >= minU && pointUv.U <= maxU
                && pointUv.V >= minV && pointUv.V <= maxV;
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
            double estimatedTagWidth,
            double estimatedTagHeight,
            int viewScale,
            XYZ viewRight,
            XYZ viewUp)
        {
            // Build the proposed tag box in view-plane coordinates.
            AnnotationBox proposed = CreateCandidateBoxInViewPlane(
                candidatePos, estimatedTagWidth, estimatedTagHeight, viewRight, viewUp);

            int totalScore = 0;

            // â”€â”€ CRITERION 1: FREE SPACE (0â€“40 pts) â”€â”€
            // No clash = 40 pts. Any overlap = disqualified.
            double tolerance = 1.5 * Constants.MM_TO_FEET * viewScale; // 1.5mm at view scale
            double nearMiss = 2.0 * Constants.MM_TO_FEET * viewScale;  // 2mm at view scale
            bool hasClash = false;
            bool hasNearMiss = false;

            foreach (AnnotationBox existing in existingAnnotations)
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
            double threshold10mm = 10.0 * Constants.MM_TO_FEET * viewScale;
            double threshold5mm = 5.0 * Constants.MM_TO_FEET * viewScale;

            foreach (AnnotationBox existing in existingAnnotations)
            {
                double dist = proposed.DistanceTo(existing);
                if (dist < minDist)
                    minDist = dist;
            }

            if (existingAnnotations.Count == 0 || minDist > threshold10mm)
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

                // Collect Dimensions.
                var dims = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Dimension))
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
                ElementId taggedId = tag.TaggedLocalElementId;
                if (taggedId == null || taggedId == ElementId.InvalidElementId)
                    return false;

                Element tagged = doc.GetElement(taggedId);
                if (tagged?.Category == null)
                    return false;

                category = (BuiltInCategory)tagged.Category.Id.IntegerValue;
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

            BoundingBoxXYZ bb = null;
            try
            {
                bb = tag.get_BoundingBox(view);
            }
            catch (Exception)
            {
                return false;
            }

            AnnotationBox box = ConvertBoundingBoxToViewPlane(bb, viewRight, viewUp);
            if (box == null)
                return false;

            double minX = box.MinX;
            double maxX = box.MaxX;
            double minY = box.MinY;
            double maxY = box.MaxY;

            try
            {
                UV headUv = ProjectToViewPlane(tag.TagHeadPosition, viewRight, viewUp);
                bool headInside = headUv != null
                    && headUv.U > minX && headUv.U < maxX
                    && headUv.V > minY && headUv.V < maxY;

                if (headInside)
                {
                    double left = headUv.U - minX;
                    double right = maxX - headUv.U;
                    double down = headUv.V - minY;
                    double up = maxY - headUv.V;

                    double halfWidth = Math.Min(left, right);
                    double halfHeight = Math.Min(down, up);

                    if (halfWidth > Constants.ZERO_LENGTH_TOLERANCE)
                    {
                        minX = headUv.U - halfWidth;
                        maxX = headUv.U + halfWidth;
                    }

                    if (halfHeight > Constants.ZERO_LENGTH_TOLERANCE)
                    {
                        minY = headUv.V - halfHeight;
                        maxY = headUv.V + halfHeight;
                    }
                }
            }
            catch (Exception)
            {
            }

            width = maxX - minX;
            height = maxY - minY;
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
        // Stagger offset reserved for future parallel group stagger logic.
        // private static readonly double StaggerOffset = 10.0 * Constants.MM_TO_FEET;

        /// <summary>
        /// Attempts to find the best tag position for a candidate.
        /// Tries the 4 cardinal positions at the configured offset,
        /// then applies parallel group and dense zone logic.
        /// Returns null if no valid position found.
        /// </summary>
        private static XYZ FindBestTagPosition(
            TagCandidate candidate,
            List<AnnotationBox> annotations,
            int viewScale,
            SmartTagSettingsState settingsState,
            Dictionary<BuiltInCategory, TagSizeHint> sizeHints,
            List<TagCandidate> allCandidates,
            HashSet<ElementId> taggedGroupMembers,
            PreFlightResult preflight,
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

            // â”€â”€ STEP 1: Try only the configured offset â”€â”€
            {
                double baseOff = fixedOffsetInternal;
                Dictionary<TagDirection, XYZ> positions = BuildCandidatePositions(
                    candidate, preflight, baseOff, tagWidth, tagHeight, viewRight, viewUp);

                // Score in direction priority order.
                TagDirection[] priority = GetDirectionPriority(candidate, preflight);
                int bestScore = -1;
                XYZ bestPos = null;

                foreach (TagDirection dir in priority)
                {
                    XYZ pos;
                    if (!positions.TryGetValue(dir, out pos))
                        continue;

                    if (!IsWithinTagPlacementBoundary(pos, preflight, viewRight, viewUp))
                        continue;

                    hadBoundaryCandidate = true;

                    int score = ScoreCandidatePosition(
                        pos, dir,
                        annotations, tagWidth, tagHeight, viewScale, viewRight, viewUp);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = pos;
                    }
                }

                if (bestScore > 0 && bestPos != null)
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

            foreach (TagCandidate other in allCandidates)
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
                        candidate, preflight, fixedOffsetInternal, tagWidth, tagHeight, viewRight, viewUp);
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

                        if (!IsWithinTagPlacementBoundary(pos, preflight, viewRight, viewUp))
                            continue;

                        hasBoundaryPosition = true;

                        AnnotationBox proposed = CreateCandidateBoxInViewPlane(
                            pos, tagWidth, tagHeight, viewRight, viewUp);

                        double totalOverlap = 0;
                        foreach (AnnotationBox existing in annotations)
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
            Dictionary<BuiltInCategory, TagSizeHint> sizeHints =
                BuildTagSizeHints(doc, activeView, candidates, viewRight, viewUp, viewScale);

            // Track which elements have been successfully tagged â€” used for parallel group logic.
            var taggedGroupMembers = new HashSet<ElementId>();
            int successCount = 0;

            // Process in priority order (candidates are already sorted HIGH â†’ MEDIUM â†’ LOW).
            foreach (TagCandidate candidate in candidates)
            {
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
                    candidate, annotations, viewScale, settingsState, sizeHints,
                    candidates, taggedGroupMembers,
                    preflight, viewRight, viewUp,
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
                    doc, activeView, candidate, tagPosition, annotations, viewScale, viewRight, viewUp, leaderLogic);

                if (placed)
                {
                    successCount++;
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
        private static bool ApplyLeaderBehavior(
            Document doc,
            IndependentTag tag,
            View view,
            LeaderLogicService leaderLogic)
        {
            if (doc == null || tag == null || view == null || leaderLogic == null)
                return true;

            try
            {
                tag.HasLeader = true;
            }
            catch (Exception)
            {
                // Best effort only.
            }

            bool hasLeader;
            try
            {
                hasLeader = tag.HasLeader;
            }
            catch (Exception)
            {
                hasLeader = false;
            }

            if (!hasLeader)
                return true;

            XYZ head;
            try
            {
                head = tag.TagHeadPosition;
            }
            catch (Exception)
            {
                return true;
            }

            if (head == null)
                return true;

            XYZ l1 = TryGetLeaderEnd(tag);
            if (l1 == null)
                l1 = TryResolveLeaderEndByRollbackProbe(doc, tag);

            if (l1 == null)
                return true;

            XYZ elbow = leaderLogic.ComputeElbow(head, l1);
            if (elbow == null)
                return true;

            elbow = AdjustElbowOutsideTextBoundsRight(tag, view, leaderLogic, elbow);
            return TrySetLeaderElbowPreserveCondition(tag, elbow);
        }

        private static bool PlaceSingleTag(
            Document doc,
            View view,
            TagCandidate candidate,
            XYZ tagPosition,
            List<AnnotationBox> annotations,
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
                        Reference elemRef = new Reference(doc.GetElement(candidate.ElementId));

                        IndependentTag newTag = IndependentTag.Create(
                            doc,
                            view.Id,
                            elemRef,
                            true, // addLeader â€” always enabled per spec
                            TagMode.TM_ADDBY_CATEGORY,
                            TagOrientation.Horizontal, // Always horizontal per spec
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
                            // If type change fails, the default type was used â€” still acceptable.
                        }

                        if (!ApplyLeaderBehavior(doc, newTag, view, leaderLogic))
                        {
                            t.RollBack();
                            return false;
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
                                    annotations.Add(placedBox);
                            }
                        }
                        catch (Exception)
                        {
                            // If we can't read the new tag's bbox, estimate it.
                            double tagW, tagH;
                            EstimateFallbackTagSize(viewScale, out tagW, out tagH);
                            annotations.Add(CreateCandidateBoxInViewPlane(
                                tagPosition, tagW, tagH, viewRight, viewUp));
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

        private static XYZ TryResolveLeaderEndByRollbackProbe(Document doc, IndependentTag tag)
        {
            if (doc == null || tag == null || !tag.IsValidObject)
                return null;

            try
            {
                using (SubTransaction st = new SubTransaction(doc))
                {
                    st.Start();
                    XYZ probed = null;
                    try
                    {
                        if (TrySetLeaderEndCondition(tag, LeaderEndCondition.Free))
                            probed = TryGetLeaderEnd(tag);
                    }
                    catch (Exception)
                    {
                    }

                    st.RollBack();
                    return probed;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static XYZ TryGetLeaderEnd(IndependentTag tag)
        {
            if (tag == null)
                return null;

            try
            {
                if (!tag.HasLeader)
                    return null;
            }
            catch (Exception)
            {
                return null;
            }

            try
            {
                XYZ direct = tag.LeaderEnd;
                if (direct != null)
                    return direct;
            }
            catch (Exception)
            {
            }

            XYZ byTaggedReference = TryGetLeaderEndFromTaggedReference(tag);
            if (byTaggedReference != null)
                return byTaggedReference;

            XYZ byTaggedReferences = TryGetLeaderEndFromTaggedReferences(tag);
            if (byTaggedReferences != null)
                return byTaggedReferences;

            return TryGetXYZProperty(tag, "LeaderEnd");
        }

        private static XYZ TryGetLeaderEndFromTaggedReference(IndependentTag tag)
        {
            if (tag == null)
                return null;

            try
            {
                Reference taggedReference = tag.GetTaggedReference();
                if (taggedReference == null)
                    return null;

                return InvokeGetLeaderEnd(tag, taggedReference);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static XYZ TryGetLeaderEndFromTaggedReferences(IndependentTag tag)
        {
            if (tag == null)
                return null;

            try
            {
                MethodInfo method = tag.GetType().GetMethod("GetTaggedReferences", BindingFlags.Instance | BindingFlags.Public);
                if (method == null)
                    return null;

                object refsRaw = method.Invoke(tag, null);
                IEnumerable refs = refsRaw as IEnumerable;
                if (refs == null)
                    return null;

                foreach (object item in refs)
                {
                    Reference reference = item as Reference;
                    if (reference == null)
                        continue;

                    XYZ end = InvokeGetLeaderEnd(tag, reference);
                    if (end != null)
                        return end;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static XYZ InvokeGetLeaderEnd(IndependentTag tag, Reference reference)
        {
            if (tag == null || reference == null)
                return null;

            try
            {
                MethodInfo[] methods = tag.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
                foreach (MethodInfo method in methods)
                {
                    if (!string.Equals(method.Name, "GetLeaderEnd", StringComparison.Ordinal))
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Reference))
                        continue;

                    object result = method.Invoke(tag, new object[] { reference });
                    XYZ xyz = result as XYZ;
                    if (xyz != null)
                        return xyz;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static XYZ TryGetXYZProperty(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            try
            {
                PropertyInfo prop = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (prop == null)
                    return null;

                object raw = prop.GetValue(instance, null);
                return raw as XYZ;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static XYZ AdjustElbowOutsideTextBoundsRight(
            IndependentTag tag,
            View activeView,
            LeaderLogicService leaderLogic,
            XYZ elbow)
        {
            if (tag == null || leaderLogic == null || elbow == null)
                return elbow;

            if (!TryGetTagBoundsInView(tag, activeView, leaderLogic, out double minX, out double maxX, out double minY, out double maxY))
                return elbow;

            UV elbowUv = leaderLogic.ProjectToView(elbow);
            if (!IsPointInsideBounds(elbowUv, minX, maxX, minY, maxY))
                return elbow;

            double rightMarginFeet = GetScaledElbowOutsideMarginFeet(activeView);
            double targetX = maxX + rightMarginFeet;
            double deltaX = targetX - elbowUv.U;
            return leaderLogic.OffsetInView(elbow, deltaX, 0);
        }

        private static double GetScaledElbowOutsideMarginFeet(View activeView)
        {
            int scale = 1;
            try
            {
                if (activeView != null && activeView.Scale > 0)
                    scale = activeView.Scale;
            }
            catch (Exception)
            {
            }

            return ElbowOutsideTextMarginMm * Constants.MM_TO_FEET * scale;
        }

        private static bool TryGetTagBoundsInView(
            IndependentTag tag,
            View activeView,
            LeaderLogicService leaderLogic,
            out double minX,
            out double maxX,
            out double minY,
            out double maxY)
        {
            minX = 0;
            maxX = 0;
            minY = 0;
            maxY = 0;

            BoundingBoxXYZ bb = GetTagBoundingBox(tag, activeView);
            if (bb == null || bb.Min == null || bb.Max == null)
                return false;

            XYZ min = bb.Min;
            XYZ max = bb.Max;
            Transform transform = bb.Transform ?? Transform.Identity;
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

            double localMinX = double.MaxValue;
            double localMinY = double.MaxValue;
            double localMaxX = double.MinValue;
            double localMaxY = double.MinValue;

            foreach (XYZ corner in corners)
            {
                XYZ worldCorner = transform.OfPoint(corner);
                UV uv = leaderLogic.ProjectToView(worldCorner);
                if (uv.U < localMinX) localMinX = uv.U;
                if (uv.U > localMaxX) localMaxX = uv.U;
                if (uv.V < localMinY) localMinY = uv.V;
                if (uv.V > localMaxY) localMaxY = uv.V;
            }

            try
            {
                UV headUv = leaderLogic.ProjectToView(tag.TagHeadPosition);
                bool headInside = headUv != null
                    && headUv.U > localMinX && headUv.U < localMaxX
                    && headUv.V > localMinY && headUv.V < localMaxY;

                if (headInside)
                {
                    double left = headUv.U - localMinX;
                    double right = localMaxX - headUv.U;
                    double down = headUv.V - localMinY;
                    double up = localMaxY - headUv.V;

                    double halfWidth = Math.Min(left, right);
                    double halfHeight = Math.Min(down, up);

                    if (halfWidth > Constants.ZERO_LENGTH_TOLERANCE)
                    {
                        localMinX = headUv.U - halfWidth;
                        localMaxX = headUv.U + halfWidth;
                    }

                    if (halfHeight > Constants.ZERO_LENGTH_TOLERANCE)
                    {
                        localMinY = headUv.V - halfHeight;
                        localMaxY = headUv.V + halfHeight;
                    }
                }
            }
            catch (Exception)
            {
            }

            if (localMinX > localMaxX || localMinY > localMaxY)
                return false;

            minX = localMinX;
            maxX = localMaxX;
            minY = localMinY;
            maxY = localMaxY;
            return true;
        }

        private static BoundingBoxXYZ GetTagBoundingBox(IndependentTag tag, View activeView)
        {
            if (tag == null)
                return null;

            try
            {
                if (activeView != null)
                {
                    BoundingBoxXYZ viewBox = tag.get_BoundingBox(activeView);
                    if (viewBox != null)
                        return viewBox;
                }
            }
            catch (Exception)
            {
            }

            try
            {
                return tag.get_BoundingBox(null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsPointInsideBounds(UV point, double minX, double maxX, double minY, double maxY)
        {
            if (point == null)
                return false;

            return point.U >= minX && point.U <= maxX
                && point.V >= minY && point.V <= maxY;
        }

        private static bool TrySetLeaderElbowPreserveCondition(IndependentTag tag, XYZ elbow)
        {
            if (TrySetLeaderElbow(tag, elbow))
                return true;

            bool hadInitialCondition = TryGetLeaderEndCondition(tag, out LeaderEndCondition initialCondition);
            if (!TrySetLeaderEndCondition(tag, LeaderEndCondition.Free))
                return false;

            if (!TrySetLeaderElbow(tag, elbow))
                return false;

            if (hadInitialCondition && !TrySetLeaderEndConditionValue(tag, initialCondition))
                return false;

            return true;
        }

        private static bool TrySetLeaderElbow(IndependentTag tag, XYZ elbow)
        {
            if (tag == null || elbow == null)
                return false;

            try
            {
                tag.LeaderElbow = elbow;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryGetLeaderEndCondition(IndependentTag tag, out LeaderEndCondition condition)
        {
            condition = LeaderEndCondition.Attached;
            if (tag == null)
                return false;

            try
            {
                condition = tag.LeaderEndCondition;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TrySetLeaderEndConditionValue(IndependentTag tag, LeaderEndCondition condition)
        {
            return TrySetLeaderEndCondition(tag, condition);
        }

        private static bool TrySetLeaderEndCondition(IndependentTag tag, LeaderEndCondition condition)
        {
            if (tag == null)
                return false;

            try
            {
                if (tag.LeaderEndCondition == condition)
                    return true;
            }
            catch (Exception)
            {
            }

            try
            {
                if (tag.CanLeaderEndConditionBeAssigned(condition))
                {
                    tag.LeaderEndCondition = condition;
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ANNOTATION BOX â€” Lightweight 2D bounding box for clash checks.
    // Used instead of BoundingBoxXYZ for faster annotation-to-annotation
    // overlap testing (no Z dimension needed for plan/section annotation).
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// A lightweight axis-aligned 2D rectangle for annotation clash detection.
    /// Supports inflation, overlap testing, and distance measurement.
    /// </summary>
    internal class AnnotationBox
    {
        public double MinX { get; private set; }
        public double MinY { get; private set; }
        public double MaxX { get; private set; }
        public double MaxY { get; private set; }

        public AnnotationBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        /// <summary>
        /// Returns a new box inflated by the given margin on all sides.
        /// </summary>
        public AnnotationBox Inflated(double margin)
        {
            return new AnnotationBox(
                MinX - margin, MinY - margin,
                MaxX + margin, MaxY + margin);
        }

        /// <summary>
        /// Tests whether this box overlaps another box.
        /// </summary>
        public bool Overlaps(AnnotationBox other)
        {
            if (MaxX <= other.MinX || MinX >= other.MaxX)
                return false;
            if (MaxY <= other.MinY || MinY >= other.MaxY)
                return false;
            return true;
        }

        /// <summary>
        /// Returns the minimum edge-to-edge distance between two boxes.
        /// Returns 0 if they overlap.
        /// </summary>
        public double DistanceTo(AnnotationBox other)
        {
            double dx = 0;
            if (MaxX < other.MinX)
                dx = other.MinX - MaxX;
            else if (MinX > other.MaxX)
                dx = MinX - other.MaxX;

            double dy = 0;
            if (MaxY < other.MinY)
                dy = other.MinY - MaxY;
            else if (MinY > other.MaxY)
                dy = MinY - other.MaxY;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Returns the overlapping area between two boxes (0 if no overlap).
        /// Used by the dense-zone force-placement to pick the least-bad position.
        /// </summary>
        public double OverlapArea(AnnotationBox other)
        {
            double overlapX = Math.Max(0, Math.Min(MaxX, other.MaxX) - Math.Max(MinX, other.MinX));
            double overlapY = Math.Max(0, Math.Min(MaxY, other.MaxY) - Math.Max(MinY, other.MinY));
            return overlapX * overlapY;
        }
    }
}


