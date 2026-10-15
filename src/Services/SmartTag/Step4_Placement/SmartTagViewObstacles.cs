#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, What Is Already On The View
 * File Name     : SmartTagViewObstacles.cs
 * Purpose       : Step 4: what a new tag must avoid - tags, text notes, dimensions, and the ducts and fittings a
 *                 no-leader tag must not sit on - plus the quick-lookup grids and the crop boundary tags stay in.
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
 * Input         : The active view.
 * Output        : Flat view-plane boxes and lookup grids.
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
using static AJTools.Services.SmartTag.SmartTagPositionFinder;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagTypeSelector;
using static AJTools.Services.SmartTag.SmartTagViewCheck;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// What a new tag must avoid, and the boundary it must stay inside.
    /// </summary>
    internal static class SmartTagViewObstacles
    {
        internal const double SpatialIndexCellSizeMm = 20.0;

        /// <summary>
        /// Collects all existing annotation bounding boxes in the view.
        /// Done ONCE per run, before the placement loop — includes tags, text notes, and dimensions.
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
        internal static void AddAnnotationBox(
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
        /// Model elements the tag should not be dropped on top of - ducts, fittings, accessories and
        /// equipment as they appear in this view. <paramref name="boxByElement"/> says which box is
        /// whose, so a tag's own element can be left out of its own check.
        /// </summary>
        /// <remarks>
        /// The scorer has never seen model geometry: it weighs a position against other TAGS, text
        /// and dimensions only. That is why a no-leader tag could land squarely on a duct junction
        /// and score perfectly - as far as the engine knew, that spot was empty. Built once per run,
        /// and only when some category actually has its leader switched off, so a normal run pays
        /// nothing for it.
        /// </remarks>
        internal static AnnotationSpatialIndex BuildModelObstacleIndex(
            Document doc,
            View view,
            XYZ viewRight,
            XYZ viewUp,
            int viewScale,
            out Dictionary<ElementId, AnnotationBox> boxByElement)
        {
            var boxes = new List<AnnotationBox>();
            boxByElement = new Dictionary<ElementId, AnnotationBox>();

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
                            {
                                boxes.Add(viewBox);
                                boxByElement[element.Id] = viewBox;
                            }
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

        internal static AnnotationSpatialIndex BuildAnnotationSpatialIndex(
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

        internal static List<AnnotationBox> QueryNearbyAnnotations(
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
        internal static AnnotationSpatialIndex BuildCandidateSpatialIndex(
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
        internal static List<TagCandidate> QueryNearbyCandidatesByPosition(
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

        internal static AnnotationBox BuildPlacementBoundaryInViewPlane(
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

        internal static bool IsWithinTagPlacementBoundary(
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
    }
}
