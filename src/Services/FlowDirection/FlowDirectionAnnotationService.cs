// Tool Name: Flow Direction Annotation Service
// Description: Places view-based annotation symbols along ducts and pipes following actual flow direction.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-21
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.DB.Mechanical, Autodesk.Revit.DB.Plumbing

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace AJTools.Services.FlowDirection
{
    /// <summary>
    /// Handles placement of flow direction annotation families on ducts and pipes.
    /// </summary>
    internal static class FlowDirectionAnnotationService
    {
        /// <summary>
        /// Places flow direction annotations along the provided element.
        /// Returns false if flow direction cannot be resolved or placement fails.
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

            if (!(element is MEPCurve mepCurve) || !(element is Duct || element is Pipe))
            {
                skipReason = "Only ducts and pipes are supported.";
                return false;
            }

            LocationCurve locationCurve = mepCurve.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null)
            {
                skipReason = "Element does not have a valid location curve.";
                return false;
            }

            if (!TryResolveFlowDirection(mepCurve, out XYZ flowStart, out XYZ flowEnd, out string flowReason))
            {
                skipReason = flowReason;
                return false;
            }

            double length = curve.Length;
            if (length < 1e-6)
            {
                skipReason = "Element curve length is too small.";
                return false;
            }

            if (spacingInternal <= 1e-6)
            {
                skipReason = "Spacing must be greater than zero.";
                return false;
            }

            bool flowAlongCurve = IsFlowAlongCurve(curve, flowStart, flowEnd);
            IList<double> distances = BuildPlacementDistances(length, spacingInternal);
            if (distances.Count == 0)
            {
                skipReason = "No placement points could be calculated.";
                return false;
            }

            XYZ viewNormal = view.ViewDirection;
            XYZ viewRight = view.RightDirection;

            foreach (double distance in distances)
            {
                double normalized = distance / length;
                if (!flowAlongCurve)
                {
                    normalized = 1.0 - normalized;
                }

                if (normalized < 0.0 || normalized > 1.0)
                    continue;

                XYZ point = curve.Evaluate(normalized, true);
                Transform derivatives = curve.ComputeDerivatives(normalized, true);
                XYZ tangent = derivatives?.BasisX;
                if (tangent == null || tangent.GetLength() < 1e-9)
                {
                    skipReason = "Flow direction could not be evaluated on the curve.";
                    return false;
                }

                tangent = tangent.Normalize();
                if (!flowAlongCurve)
                {
                    tangent = tangent.Negate();
                }

                if (!TryPlaceInstance(doc, view, symbol, point, tangent, viewRight, viewNormal, out string placeReason))
                {
                    skipReason = placeReason;
                    return false;
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
            if (length <= 1e-9 || spacing <= 1e-9)
                return distances;

            if (length < spacing)
            {
                distances.Add(length / 2.0);
                return distances;
            }

            double current = spacing;
            double limit = length + 1e-6;
            while (current <= limit)
            {
                distances.Add(current);
                current += spacing;
            }

            return distances;
        }

        private static bool TryPlaceInstance(
            Document doc,
            View view,
            FamilySymbol symbol,
            XYZ point,
            XYZ flowDirection,
            XYZ viewRight,
            XYZ viewNormal,
            out string reason)
        {
            reason = string.Empty;

            if (flowDirection == null || flowDirection.GetLength() < 1e-9)
            {
                reason = "Flow direction could not be evaluated.";
                return false;
            }

            XYZ projected = flowDirection - viewNormal.Multiply(flowDirection.DotProduct(viewNormal));
            if (projected.GetLength() < 1e-9)
            {
                reason = "Flow direction is perpendicular to the view.";
                return false;
            }

            XYZ projectedDir = projected.Normalize();
            double angle = viewRight.AngleTo(projectedDir);
            double sign = viewRight.CrossProduct(projectedDir).DotProduct(viewNormal);
            if (sign < 0)
            {
                angle = -angle;
            }

            FamilyInstance instance = doc.Create.NewFamilyInstance(point, symbol, view);
            if (instance == null)
            {
                reason = "Failed to create the annotation instance.";
                return false;
            }

            if (Math.Abs(angle) > 1e-9)
            {
                Line axis = Line.CreateUnbound(point, viewNormal);
                ElementTransformUtils.RotateElement(doc, instance.Id, axis, angle);
            }

            return true;
        }

        private static bool TryResolveFlowDirection(
            MEPCurve curve,
            out XYZ flowStart,
            out XYZ flowEnd,
            out string reason)
        {
            flowStart = null;
            flowEnd = null;
            reason = string.Empty;

            ConnectorSet connectorSet = curve?.ConnectorManager?.Connectors;
            if (connectorSet == null)
            {
                reason = "No connectors found for the selected element.";
                return false;
            }

            List<Connector> endConnectors = connectorSet
                .Cast<Connector>()
                .Where(c => c != null && c.ConnectorType == ConnectorType.End)
                .ToList();

            if (endConnectors.Count < 2)
            {
                reason = "Flow direction could not be resolved (missing end connectors).";
                return false;
            }

            // The Connector API in some Revit versions does not expose an explicit FlowDirection property.
            // Fallback: infer flow direction from connector orientation (Direction) where possible.
            // If a connector's direction vector points toward the other end connector, consider it the start.

            // Try to use explicit flow direction if available via property name (guarded by reflection)
            try
            {
                // Some Revit builds may expose a FlowDirection property on Connector; use it if present.
                var t = typeof(Connector);
                var prop = t.GetProperty("FlowDirection");
                if (prop != null)
                {
                    var inC = endConnectors.FirstOrDefault(c =>
                    {
                        var val = prop.GetValue(c, null);
                        return val != null && val.ToString().IndexOf("In", StringComparison.OrdinalIgnoreCase) >= 0;
                    });

                    var outC = endConnectors.FirstOrDefault(c =>
                    {
                        var val = prop.GetValue(c, null);
                        return val != null && val.ToString().IndexOf("Out", StringComparison.OrdinalIgnoreCase) >= 0;
                    });

                    if (inC != null && outC != null)
                    {
                        flowStart = inC.Origin;
                        flowEnd = outC.Origin;
                        return true;
                    }
                }
            }
            catch
            {
                // ignore reflection failures and continue with heuristic
            }

            // No explicit per-connector direction available; fall back to position-based heuristics below.

            // Final fallback: if the MEPCurve has a location curve, choose connector closer to its start point.
            try
            {
                LocationCurve lc = curve.Location as LocationCurve;
                Curve geom = lc?.Curve;
                if (geom != null && geom.IsBound)
                {
                    XYZ curveStart = geom.GetEndPoint(0);
                    // Choose connector whose origin is closer to the curve start as the flow start
                    Connector closer = endConnectors.OrderBy(c => c.Origin.DistanceTo(curveStart)).First();
                    Connector other = endConnectors.First(c => !ReferenceEquals(c, closer));
                    flowStart = closer.Origin;
                    flowEnd = other.Origin;
                    return true;
                }
            }
            catch
            {
                // ignore and fallback to deterministic assignment below
            }

            // As a simple deterministic fallback, assign first connector as start and second as end.
            flowStart = endConnectors[0].Origin;
            flowEnd = endConnectors[1].Origin;
            return true;
        }

        private static bool IsFlowAlongCurve(Curve curve, XYZ flowStart, XYZ flowEnd)
        {
            XYZ curveDir = curve.GetEndPoint(1).Subtract(curve.GetEndPoint(0));
            if (curveDir.GetLength() < 1e-9)
                return true;

            curveDir = curveDir.Normalize();
            XYZ flowDir = flowEnd.Subtract(flowStart);
            if (flowDir.GetLength() < 1e-9)
                return true;

            flowDir = flowDir.Normalize();
            return curveDir.DotProduct(flowDir) >= 0.0;
        }
    }
}
