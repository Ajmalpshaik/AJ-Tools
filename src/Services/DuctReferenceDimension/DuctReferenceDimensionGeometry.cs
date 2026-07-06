// Tool Name: Duct Reference Dimension Geometry
// Description: Geometry and reference extraction helpers for duct reference dimensions.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-05-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.DuctReferenceDimension
{
    internal static class DuctReferenceDimensionGeometry
    {
        internal const double AxisBandTolerance = 150.0 * Constants.MM_TO_FEET;
        internal const double CoordinateMergeTolerance = 1.0 * Constants.MM_TO_FEET;

        private const double DefaultSearchHalfLength = 250.0;
        private const double MinimumSearchHalfLength = 25.0;
        private const double FaceNormalAlignmentTolerance = 0.02;
        private const double NormalizeTolerance = 1e-9;

        internal static bool TryCreateAxisFromDuct(
            View view,
            Element duct,
            out DuctDimensionAxis axis,
            out string reason)
        {
            axis = null;
            reason = string.Empty;

            if (view == null)
            {
                reason = "No active view.";
                return false;
            }

            if (duct == null)
            {
                reason = "No duct was selected.";
                return false;
            }

            LocationCurve locationCurve = duct.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null)
            {
                reason = "Selected duct does not have a valid location curve.";
                return false;
            }

            if (!TryGetCurveDirection(curve, out XYZ ductDirection))
            {
                reason = "Selected duct direction could not be resolved.";
                return false;
            }

            XYZ viewNormal = view.ViewDirection;
            if (viewNormal == null || viewNormal.GetLength() <= NormalizeTolerance)
                viewNormal = XYZ.BasisZ;

            viewNormal = viewNormal.Normalize();
            XYZ ductDirectionOnView = ProjectVectorToPlane(ductDirection, viewNormal);
            if (ductDirectionOnView == null || ductDirectionOnView.GetLength() <= NormalizeTolerance)
            {
                reason = "Selected duct does not have a usable plan direction.";
                return false;
            }

            ductDirectionOnView = ductDirectionOnView.Normalize();
            XYZ dimensionDirection = viewNormal.CrossProduct(ductDirectionOnView);
            if (dimensionDirection.GetLength() <= NormalizeTolerance)
            {
                reason = "Could not build a perpendicular dimension axis for the selected duct.";
                return false;
            }

            dimensionDirection = dimensionDirection.Normalize();
            XYZ ductMidPoint = GetCurveMidPoint(curve);
            XYZ axisOrigin = ProjectPointToViewPlane(view, ductMidPoint, viewNormal);

            axis = new DuctDimensionAxis
            {
                Origin = axisOrigin,
                DimensionDirection = dimensionDirection,
                DuctDirection = ductDirectionOnView,
                ViewNormal = viewNormal,
                OriginDimensionCoord = axisOrigin.DotProduct(dimensionDirection),
                OriginDuctCoord = axisOrigin.DotProduct(ductDirectionOnView),
                AxisBandTolerance = AxisBandTolerance
            };

            axis.SearchHalfLength = ResolveSearchHalfLength(view, axis);
            return true;
        }

        internal static bool MayIntersectAxisBand(Element element, View view, DuctDimensionAxis axis)
        {
            if (element == null || axis == null)
                return false;

            BoundingBoxXYZ box = null;
            try
            {
                box = element.get_BoundingBox(view) ?? element.get_BoundingBox(null);
            }
            catch
            {
                box = null;
            }

            if (box == null)
                return true;

            IList<XYZ> corners = GetBoundingBoxCorners(box);
            if (corners.Count == 0)
                return true;

            double minDuct = corners.Min(p => p.DotProduct(axis.DuctDirection));
            double maxDuct = corners.Max(p => p.DotProduct(axis.DuctDirection));
            if (axis.OriginDuctCoord < minDuct - axis.AxisBandTolerance ||
                axis.OriginDuctCoord > maxDuct + axis.AxisBandTolerance)
            {
                return false;
            }

            double minDim = corners.Min(p => p.DotProduct(axis.DimensionDirection));
            double maxDim = corners.Max(p => p.DotProduct(axis.DimensionDirection));
            double searchMin = axis.OriginDimensionCoord - axis.SearchHalfLength;
            double searchMax = axis.OriginDimensionCoord + axis.SearchHalfLength;

            return maxDim >= searchMin && minDim <= searchMax;
        }

        internal static IList<DuctReferenceCandidate> CollectFaceReferenceCandidates(
            Document doc,
            View view,
            Element element,
            DuctDimensionAxis axis,
            DuctReferenceTargetType targetType,
            ElementId selectedDuctId)
        {
            List<DuctReferenceCandidate> candidates = new List<DuctReferenceCandidate>();
            if (doc == null || view == null || element == null || axis == null)
                return candidates;

            GeometryElement geometry = GetGeometryWithReferences(element, view);
            if (geometry == null)
                return candidates;

            CollectFaceReferenceCandidates(
                geometry,
                Transform.Identity,
                element,
                doc,
                axis,
                targetType,
                selectedDuctId,
                candidates);

            return candidates;
        }

        internal static bool TryCreateDimensionLine(View view, DuctDimensionPlan plan, out string reason)
        {
            reason = string.Empty;
            if (view == null || plan?.Axis == null || plan.References == null || plan.References.Count < 2)
            {
                reason = "Not enough references were found to create a dimension.";
                return false;
            }

            double minCoord = plan.References.Min(r => r.SortCoord);
            double maxCoord = plan.References.Max(r => r.SortCoord);
            double scale = Math.Max(1.0, view.Scale);
            double padding = Math.Max(1.0, 6.0 * Constants.MM_TO_FEET * scale);

            XYZ start = plan.Axis.Origin +
                        plan.Axis.DimensionDirection * (minCoord - plan.Axis.OriginDimensionCoord - padding);
            XYZ end = plan.Axis.Origin +
                      plan.Axis.DimensionDirection * (maxCoord - plan.Axis.OriginDimensionCoord + padding);

            if (start.DistanceTo(end) <= Constants.MIN_DISTANCE_TOLERANCE)
            {
                reason = "Dimension line length is too small.";
                return false;
            }

            plan.DimensionLine = Line.CreateBound(start, end);
            return true;
        }

        internal static bool AreParallel(XYZ a, XYZ b, double tolerance)
        {
            if (a == null || b == null)
                return false;

            if (a.GetLength() <= NormalizeTolerance || b.GetLength() <= NormalizeTolerance)
                return false;

            double alignment = Math.Abs(a.Normalize().DotProduct(b.Normalize()));
            return alignment >= 1.0 - tolerance;
        }

        internal static bool TryGetLineIntervalAlongAxis(
            Curve curve,
            DuctDimensionAxis axis,
            out double minCoord,
            out double maxCoord,
            out double ductCoord,
            out XYZ direction)
        {
            minCoord = 0.0;
            maxCoord = 0.0;
            ductCoord = 0.0;
            direction = null;

            Line line = curve as Line;
            if (line == null || axis == null)
                return false;

            try
            {
                if (!line.IsBound)
                {
                    direction = ProjectVectorToPlane(line.Direction, axis.ViewNormal);
                    if (direction == null || direction.GetLength() <= NormalizeTolerance)
                        return false;

                    direction = direction.Normalize();
                    XYZ origin = ProjectPointToViewPlane(null, line.Origin, axis.ViewNormal);
                    ductCoord = origin.DotProduct(axis.DuctDirection);
                    minCoord = axis.OriginDimensionCoord - axis.SearchHalfLength;
                    maxCoord = axis.OriginDimensionCoord + axis.SearchHalfLength;
                    return true;
                }

                XYZ start = line.GetEndPoint(0);
                XYZ end = line.GetEndPoint(1);
                XYZ vector = end - start;
                if (vector.GetLength() <= NormalizeTolerance)
                    return false;

                direction = vector.Normalize();
                double c0 = start.DotProduct(axis.DimensionDirection);
                double c1 = end.DotProduct(axis.DimensionDirection);
                minCoord = Math.Min(c0, c1);
                maxCoord = Math.Max(c0, c1);
                ductCoord = ((start + end) * 0.5).DotProduct(axis.DuctDirection);
                return true;
            }
            catch
            {
                minCoord = 0.0;
                maxCoord = 0.0;
                ductCoord = 0.0;
                direction = null;
                return false;
            }
        }

        internal static bool IntervalsOverlap(
            double minA,
            double maxA,
            double minB,
            double maxB,
            double tolerance)
        {
            return maxA >= minB - tolerance && maxB >= minA - tolerance;
        }

        internal static string GetReferenceStableKey(Document doc, Reference reference)
        {
            if (reference == null)
                return string.Empty;

            try
            {
                return reference.ConvertToStableRepresentation(doc);
            }
            catch
            {
                ElementId elementId = reference.ElementId;
                return elementId == null ? string.Empty : elementId.IntegerValue.ToString();
            }
        }

        private static GeometryElement GetGeometryWithReferences(Element element, View view)
        {
            GeometryElement geometry = null;

            try
            {
                Options viewOptions = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = false,
                    View = view
                };
                geometry = element.get_Geometry(viewOptions);
            }
            catch
            {
                geometry = null;
            }

            if (geometry != null)
                return geometry;

            try
            {
                Options modelOptions = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = false
                };
                return element.get_Geometry(modelOptions);
            }
            catch
            {
                return null;
            }
        }

        private static void CollectFaceReferenceCandidates(
            GeometryElement geometry,
            Transform transform,
            Element element,
            Document doc,
            DuctDimensionAxis axis,
            DuctReferenceTargetType targetType,
            ElementId selectedDuctId,
            IList<DuctReferenceCandidate> candidates)
        {
            if (geometry == null || element == null || candidates == null)
                return;

            foreach (GeometryObject obj in geometry)
            {
                Solid solid = obj as Solid;
                if (solid != null)
                {
                    CollectSolidFaceCandidates(solid, transform, element, doc, axis, targetType, selectedDuctId, candidates);
                    continue;
                }

                GeometryInstance instance = obj as GeometryInstance;
                if (instance == null)
                    continue;

                Transform nextTransform = transform;
                try
                {
                    if (instance.Transform != null)
                        nextTransform = transform.Multiply(instance.Transform);
                }
                catch
                {
                    nextTransform = transform;
                }

                GeometryElement symbolGeometry = null;
                try
                {
                    symbolGeometry = instance.GetSymbolGeometry();
                }
                catch
                {
                    symbolGeometry = null;
                }

                if (symbolGeometry != null)
                {
                    CollectFaceReferenceCandidates(
                        symbolGeometry,
                        nextTransform,
                        element,
                        doc,
                        axis,
                        targetType,
                        selectedDuctId,
                        candidates);
                    continue;
                }

                GeometryElement instanceGeometry = null;
                try
                {
                    instanceGeometry = instance.GetInstanceGeometry();
                }
                catch
                {
                    instanceGeometry = null;
                }

                if (instanceGeometry != null)
                {
                    CollectFaceReferenceCandidates(
                        instanceGeometry,
                        transform,
                        element,
                        doc,
                        axis,
                        targetType,
                        selectedDuctId,
                        candidates);
                }
            }
        }

        private static void CollectSolidFaceCandidates(
            Solid solid,
            Transform transform,
            Element element,
            Document doc,
            DuctDimensionAxis axis,
            DuctReferenceTargetType targetType,
            ElementId selectedDuctId,
            IList<DuctReferenceCandidate> candidates)
        {
            if (solid?.Faces == null || solid.Faces.Size == 0)
                return;

            foreach (Face face in solid.Faces)
            {
                PlanarFace planarFace = face as PlanarFace;
                if (planarFace == null || planarFace.Reference == null)
                    continue;

                if (!TryCreateCandidateFromFace(
                    planarFace,
                    transform,
                    element,
                    doc,
                    axis,
                    targetType,
                    selectedDuctId,
                    out DuctReferenceCandidate candidate))
                {
                    continue;
                }

                candidates.Add(candidate);
            }
        }

        private static bool TryCreateCandidateFromFace(
            PlanarFace face,
            Transform transform,
            Element element,
            Document doc,
            DuctDimensionAxis axis,
            DuctReferenceTargetType targetType,
            ElementId selectedDuctId,
            out DuctReferenceCandidate candidate)
        {
            candidate = null;

            XYZ faceNormal = TransformVector(transform, face.FaceNormal);
            XYZ normalOnView = ProjectVectorToPlane(faceNormal, axis.ViewNormal);
            if (normalOnView == null || normalOnView.GetLength() <= NormalizeTolerance)
                return false;

            normalOnView = normalOnView.Normalize();
            if (!AreParallel(normalOnView, axis.DimensionDirection, FaceNormalAlignmentTolerance))
                return false;

            IList<XYZ> points = GetPlanarFacePoints(face, transform);
            if (points.Count == 0)
                return false;

            double minDuctCoord = points.Min(p => p.DotProduct(axis.DuctDirection));
            double maxDuctCoord = points.Max(p => p.DotProduct(axis.DuctDirection));
            if (axis.OriginDuctCoord < minDuctCoord - axis.AxisBandTolerance ||
                axis.OriginDuctCoord > maxDuctCoord + axis.AxisBandTolerance)
            {
                return false;
            }

            XYZ faceOrigin = TransformPoint(transform, face.Origin);
            double sortCoord = faceOrigin.DotProduct(axis.DimensionDirection);
            if (Math.Abs(sortCoord - axis.OriginDimensionCoord) > axis.SearchHalfLength)
                return false;

            double axisOffset = 0.0;
            if (axis.OriginDuctCoord < minDuctCoord)
                axisOffset = minDuctCoord - axis.OriginDuctCoord;
            else if (axis.OriginDuctCoord > maxDuctCoord)
                axisOffset = axis.OriginDuctCoord - maxDuctCoord;

            string stableKey = GetReferenceStableKey(doc, face.Reference);
            if (string.IsNullOrWhiteSpace(stableKey))
                return false;

            bool isSelectedDuct = selectedDuctId != null && element.Id.IntegerValue == selectedDuctId.IntegerValue;
            candidate = new DuctReferenceCandidate
            {
                ElementId = element.Id,
                Reference = face.Reference,
                SortCoord = sortCoord,
                AxisOffset = axisOffset,
                TargetType = targetType,
                IsDuct = targetType == DuctReferenceTargetType.Duct,
                IsSelectedDuct = isSelectedDuct,
                StableKey = stableKey
            };

            return true;
        }

        private static IList<XYZ> GetPlanarFacePoints(PlanarFace face, Transform transform)
        {
            List<XYZ> points = new List<XYZ>();
            try
            {
                foreach (EdgeArray loop in face.EdgeLoops)
                {
                    foreach (Edge edge in loop)
                    {
                        IList<XYZ> tessellated = edge.Tessellate();
                        if (tessellated == null)
                            continue;

                        foreach (XYZ point in tessellated)
                        {
                            points.Add(TransformPoint(transform, point));
                        }
                    }
                }
            }
            catch
            {
                points.Clear();
            }

            if (points.Count == 0)
                points.Add(TransformPoint(transform, face.Origin));

            return points;
        }

        private static bool TryGetCurveDirection(Curve curve, out XYZ direction)
        {
            direction = null;
            if (curve == null)
                return false;

            try
            {
                Transform derivatives = curve.ComputeDerivatives(0.5, true);
                XYZ tangent = derivatives?.BasisX;
                if (tangent != null && tangent.GetLength() > NormalizeTolerance)
                {
                    direction = tangent.Normalize();
                    return true;
                }
            }
            catch
            {
                // Fall back to endpoints below.
            }

            if (!curve.IsBound)
                return false;

            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            XYZ vector = end - start;
            if (vector.GetLength() <= NormalizeTolerance)
                return false;

            direction = vector.Normalize();
            return true;
        }

        private static XYZ GetCurveMidPoint(Curve curve)
        {
            try
            {
                return curve.Evaluate(0.5, true);
            }
            catch
            {
                if (curve != null && curve.IsBound)
                    return (curve.GetEndPoint(0) + curve.GetEndPoint(1)) * 0.5;
            }

            return XYZ.Zero;
        }

        private static XYZ ProjectVectorToPlane(XYZ vector, XYZ normal)
        {
            if (vector == null || normal == null)
                return null;

            return vector - normal * vector.DotProduct(normal);
        }

        private static XYZ ProjectPointToViewPlane(View view, XYZ point, XYZ viewNormal)
        {
            XYZ origin = view?.Origin ?? XYZ.Zero;
            XYZ normal = viewNormal ?? XYZ.BasisZ;
            double distance = (point - origin).DotProduct(normal);
            return point - normal * distance;
        }

        private static double ResolveSearchHalfLength(View view, DuctDimensionAxis axis)
        {
            // An inactive crop box still returns stale bounds that would wrongly shrink the search
            // range, so only trust CropBox when the crop is actually active.
            if (view == null || !view.CropBoxActive || view.CropBox == null || axis == null)
                return DefaultSearchHalfLength;

            try
            {
                IList<XYZ> corners = GetBoundingBoxCorners(view.CropBox);
                if (corners.Count == 0)
                    return DefaultSearchHalfLength;

                double minCoord = corners.Min(p => p.DotProduct(axis.DimensionDirection));
                double maxCoord = corners.Max(p => p.DotProduct(axis.DimensionDirection));
                double halfLength = Math.Max(
                    Math.Abs(maxCoord - axis.OriginDimensionCoord),
                    Math.Abs(axis.OriginDimensionCoord - minCoord));

                return Math.Max(MinimumSearchHalfLength, halfLength + 10.0);
            }
            catch
            {
                return DefaultSearchHalfLength;
            }
        }

        private static IList<XYZ> GetBoundingBoxCorners(BoundingBoxXYZ box)
        {
            List<XYZ> corners = new List<XYZ>();
            if (box == null)
                return corners;

            Transform transform = box.Transform ?? Transform.Identity;
            double[] xs = { box.Min.X, box.Max.X };
            double[] ys = { box.Min.Y, box.Max.Y };
            double[] zs = { box.Min.Z, box.Max.Z };

            foreach (double x in xs)
            {
                foreach (double y in ys)
                {
                    foreach (double z in zs)
                    {
                        corners.Add(transform.OfPoint(new XYZ(x, y, z)));
                    }
                }
            }

            return corners;
        }

        private static XYZ TransformPoint(Transform transform, XYZ point)
        {
            if (point == null)
                return XYZ.Zero;

            try
            {
                return transform == null ? point : transform.OfPoint(point);
            }
            catch
            {
                return point;
            }
        }

        private static XYZ TransformVector(Transform transform, XYZ vector)
        {
            if (vector == null)
                return XYZ.Zero;

            try
            {
                return transform == null ? vector : transform.OfVector(vector);
            }
            catch
            {
                return vector;
            }
        }
    }
}
