// Tool Name: Duct Flow Annotation Service
// Description: Places view-based annotation symbols along ducts using curve direction in the active view.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-21
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.DB.Mechanical

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace AJTools.Services.FlowDirection
{
    /// <summary>
    /// Handles placement of duct flow annotation families on ducts.
    /// </summary>
    internal static class FlowDirectionAnnotationService
    {
        private const double HorizontalZTolerance = 1e-3;
        private const double NormalizeTolerance = 1e-9;
        private const double PlacementTolerance = 1e-6;

        /// <summary>
        /// Places duct flow annotations along the provided element.
        /// Returns false if placement fails or the duct is not supported.
        /// </summary>
        internal static bool TryPlaceFlowAnnotations(
            Document doc,
            View view,
            FamilySymbol symbol,
            double spacingInternal,
            Element element,
            out int placedCount,
            out string skipReason)
        {
            placedCount = 0;
            skipReason = string.Empty;

            if (doc == null || view == null || symbol == null || element == null)
            {
                skipReason = "Missing document, view, symbol, or element data.";
                return false;
            }

            if (!(element is Duct duct))
            {
                skipReason = "Only ducts are supported.";
                return false;
            }

            LocationCurve locationCurve = duct.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null)
            {
                skipReason = "Element does not have a valid location curve.";
                return false;
            }

            if (!TryGetHorizontalDirection(curve, out XYZ dirUnit, out XYZ start))
            {
                skipReason = "Only horizontal ducts are supported.";
                return false;
            }

            double length = curve.Length;
            if (length < NormalizeTolerance)
            {
                skipReason = "Element curve length is too small.";
                return false;
            }

            if (spacingInternal <= NormalizeTolerance)
            {
                skipReason = "Spacing must be greater than zero.";
                return false;
            }

            IList<double> distances = BuildPlacementDistances(length, spacingInternal);
            if (distances.Count == 0)
            {
                skipReason = "Duct is shorter than spacing.";
                return false;
            }

            XYZ viewNormal = view.ViewDirection;
            bool hasAngle = TryComputeAngleOnViewPlane(dirUnit, viewNormal, out double angle);

            foreach (double distance in distances)
            {
                double normalized = distance / length;

                XYZ point;
                try
                {
                    point = curve.Evaluate(normalized, true);
                }
                catch
                {
                    point = start + dirUnit.Multiply(distance);
                }

                FamilyInstance instance = doc.Create.NewFamilyInstance(point, symbol, view);
                if (instance == null)
                {
                    skipReason = "Failed to create the annotation instance.";
                    return false;
                }

                if (hasAngle && Math.Abs(angle) > NormalizeTolerance)
                {
                    Line axis = Line.CreateUnbound(point, viewNormal);
                    ElementTransformUtils.RotateElement(doc, instance.Id, axis, angle);
                }

                placedCount++;
            }

            if (placedCount == 0)
            {
                skipReason = "No valid placement points were found.";
                return false;
            }

            return true;
        }

        private static IList<double> BuildPlacementDistances(double length, double spacing)
        {
            List<double> distances = new List<double>();
            if (length <= NormalizeTolerance || spacing <= NormalizeTolerance)
                return distances;

            if (length < spacing)
                return distances;

            double current = spacing;
            double limit = length - PlacementTolerance;
            while (current < limit)
            {
                distances.Add(current);
                current += spacing;
            }

            return distances;
        }

        private static bool TryGetHorizontalDirection(Curve curve, out XYZ dirUnit, out XYZ start)
        {
            dirUnit = null;
            start = null;

            if (curve == null)
                return false;

            start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            XYZ direction = end - start;
            if (direction.GetLength() < NormalizeTolerance)
                return false;

            dirUnit = direction.Normalize();
            return Math.Abs(dirUnit.Z) <= HorizontalZTolerance;
        }

        private static bool TryComputeAngleOnViewPlane(XYZ direction, XYZ viewDirection, out double angle)
        {
            angle = 0;

            if (direction == null || viewDirection == null)
                return false;

            double dot = direction.DotProduct(viewDirection);
            XYZ projected = direction - viewDirection.Multiply(dot);
            if (projected.GetLength() < NormalizeTolerance)
                return false;

            projected = projected.Normalize();

            XYZ xAxis = XYZ.BasisX;
            double dotX = xAxis.DotProduct(viewDirection);
            XYZ refAxis = xAxis - viewDirection.Multiply(dotX);
            if (refAxis.GetLength() < NormalizeTolerance)
            {
                refAxis = XYZ.BasisY;
            }
            else
            {
                refAxis = refAxis.Normalize();
            }

            XYZ cross = refAxis.CrossProduct(projected);
            double num = cross.DotProduct(viewDirection);
            double den = refAxis.DotProduct(projected);
            angle = Math.Atan2(num, den);
            return true;
        }
    }
}
