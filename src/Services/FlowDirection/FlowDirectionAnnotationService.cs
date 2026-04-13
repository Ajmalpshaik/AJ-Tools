// Tool Name: Duct Flow Annotation Service
// Description: Places view-based annotation symbols along ducts using connector-aware flow direction in the active view.
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

            if (!TryGetHorizontalCurveDirection(curve, out XYZ curveDirUnit, out XYZ curveStart, out XYZ curveEnd))
            {
                skipReason = "Only horizontal ducts are supported.";
                return false;
            }

            XYZ flowDirUnit = curveDirUnit;
            if (TryGetConnectorFlowDirection(duct, curveStart, curveEnd, curveDirUnit, out XYZ connectorFlowDir))
            {
                flowDirUnit = connectorFlowDir;
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

            XYZ viewRight = view.RightDirection;
            XYZ viewUp = view.UpDirection;
            XYZ viewNormal = view.ViewDirection;
            bool hasAngle = TryComputeAngleOnViewPlane(flowDirUnit, viewRight, viewUp, out double angle);

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
                    point = curveStart + curveDirUnit.Multiply(distance);
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

        private static bool TryGetHorizontalCurveDirection(
            Curve curve,
            out XYZ dirUnit,
            out XYZ start,
            out XYZ end)
        {
            dirUnit = null;
            start = null;
            end = null;

            if (curve == null)
                return false;

            start = curve.GetEndPoint(0);
            end = curve.GetEndPoint(1);
            XYZ direction = end - start;
            if (direction.GetLength() < NormalizeTolerance)
                return false;

            dirUnit = direction.Normalize();
            return Math.Abs(dirUnit.Z) <= HorizontalZTolerance;
        }

        private static bool TryGetConnectorFlowDirection(
            Duct duct,
            XYZ curveStart,
            XYZ curveEnd,
            XYZ curveDirection,
            out XYZ flowDirection)
        {
            flowDirection = null;
            if (duct == null || curveStart == null || curveEnd == null || curveDirection == null)
                return false;

            ConnectorManager manager = duct.ConnectorManager;
            if (manager == null)
                return false;

            if (!TryGetEndConnectorsAtCurveEnds(manager, curveStart, curveEnd, out Connector startConnector, out Connector endConnector))
                return false;

            bool hasStartFlow = TryGetDirectionalFlow(startConnector, out FlowDirectionType startFlow);
            bool hasEndFlow = TryGetDirectionalFlow(endConnector, out FlowDirectionType endFlow);

            if (hasStartFlow && hasEndFlow)
            {
                if (startFlow == FlowDirectionType.In && endFlow == FlowDirectionType.Out)
                {
                    flowDirection = curveDirection;
                    return true;
                }

                if (startFlow == FlowDirectionType.Out && endFlow == FlowDirectionType.In)
                {
                    flowDirection = curveDirection.Multiply(-1.0);
                    return true;
                }
            }

            if (hasStartFlow)
            {
                flowDirection = startFlow == FlowDirectionType.In
                    ? curveDirection
                    : curveDirection.Multiply(-1.0);
                return true;
            }

            if (hasEndFlow)
            {
                flowDirection = endFlow == FlowDirectionType.Out
                    ? curveDirection
                    : curveDirection.Multiply(-1.0);
                return true;
            }

            return false;
        }

        private static bool TryGetEndConnectorsAtCurveEnds(
            ConnectorManager manager,
            XYZ curveStart,
            XYZ curveEnd,
            out Connector startConnector,
            out Connector endConnector)
        {
            startConnector = null;
            endConnector = null;

            if (manager == null || curveStart == null || curveEnd == null)
                return false;

            var connectors = new List<Connector>();
            foreach (Connector connector in manager.Connectors)
            {
                if (connector == null || !connector.IsValidObject)
                    continue;

                if (connector.ConnectorType != ConnectorType.End)
                    continue;

                connectors.Add(connector);
            }

            if (connectors.Count < 2)
                return false;

            double bestStartDist = double.MaxValue;
            foreach (Connector connector in connectors)
            {
                double dist = connector.Origin.DistanceTo(curveStart);
                if (dist < bestStartDist)
                {
                    bestStartDist = dist;
                    startConnector = connector;
                }
            }

            double bestEndDist = double.MaxValue;
            foreach (Connector connector in connectors)
            {
                if (connector == startConnector)
                    continue;

                double dist = connector.Origin.DistanceTo(curveEnd);
                if (dist < bestEndDist)
                {
                    bestEndDist = dist;
                    endConnector = connector;
                }
            }

            if (startConnector == null || endConnector == null)
                return false;

            return true;
        }

        private static bool TryGetDirectionalFlow(Connector connector, out FlowDirectionType flowDirection)
        {
            flowDirection = FlowDirectionType.Bidirectional;
            if (connector == null || !connector.IsValidObject)
                return false;

            FlowDirectionType direction;
            try
            {
                direction = connector.Direction;
            }
            catch
            {
                return false;
            }

            if (direction != FlowDirectionType.In && direction != FlowDirectionType.Out)
                return false;

            flowDirection = direction;
            return true;
        }

        private static bool TryComputeAngleOnViewPlane(
            XYZ direction,
            XYZ viewRight,
            XYZ viewUp,
            out double angle)
        {
            angle = 0;

            if (direction == null || viewRight == null || viewUp == null)
                return false;

            if (viewRight.GetLength() < NormalizeTolerance || viewUp.GetLength() < NormalizeTolerance)
                return false;

            XYZ right = viewRight.Normalize();
            XYZ up = viewUp.Normalize();
            double u = direction.DotProduct(right);
            double v = direction.DotProduct(up);

            if (Math.Abs(u) < NormalizeTolerance && Math.Abs(v) < NormalizeTolerance)
                return false;

            angle = Math.Atan2(v, u);
            return true;
        }
    }
}
