// Tool Name: Smart Connect - Route Builder
// Description: Creates Smart Connect routing geometry and fittings between two MEP elements.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-25
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models, AJTools.Utils

using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using AJTools.Models;
using AJTools.Utils;

namespace AJTools.Services.SmartConnect
{
    /// <summary>
    /// Builds Smart Connect routes between supported MEP elements.
    /// </summary>
    internal sealed class SmartConnectRouteBuilder
    {
        private const double JoinPointTolerance = 1e-4;
        private const double AngleToleranceDegrees = 2.5;
        private const double SizeComparisonTolerance = 1e-8;
        private const double QuarterTurnRadians = Math.PI * 0.5;
        private static readonly double MinSegmentLength = ConvertMillimetersToInternal(10.0);
        private static readonly double MinRunLength = ConvertMillimetersToInternal(50.0);

        private readonly Document _document;

        public SmartConnectRouteBuilder(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public bool TryBuildRoute(
            Element firstElement,
            Element secondElement,
            SmartConnectRoutingMode routingMode,
            double angleDegrees,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (firstElement == null || secondElement == null)
            {
                errorMessage = "Selected elements are invalid.";
                return false;
            }

            if (!SmartConnectSelectionFilter.TryGetSupportedCategory(firstElement, out BuiltInCategory firstCategory) ||
                !SmartConnectSelectionFilter.TryGetSupportedCategory(secondElement, out BuiltInCategory secondCategory))
            {
                errorMessage = "Smart Connect supports only Pipe, Duct, and Cable Tray.";
                return false;
            }

            if (firstCategory != secondCategory)
            {
                errorMessage = "Please select two elements from the same category.";
                return false;
            }

            if (!SmartConnectSettingsService.TryNormalizeAngle(angleDegrees, out double normalizedAngle))
            {
                errorMessage = "Selected angle is invalid. Use a practical angle between 5 and 175 degrees.";
                return false;
            }

            switch (routingMode)
            {
                case SmartConnectRoutingMode.SingleElbow:
                    if (!(firstElement is MEPCurve firstCurve) || !(secondElement is MEPCurve secondCurve))
                    {
                        errorMessage = "Single Elbow mode supports linear Pipe, Duct, and Cable Tray elements only.";
                        return false;
                    }

                    return TryBuildSingleElbowMepCurveRoute(
                        firstCurve,
                        secondCurve,
                        normalizedAngle,
                        out errorMessage);

                case SmartConnectRoutingMode.OffsetWithTwoElbows:
                    if (!SmartConnectConnectorUtils.TryGetBestOpenConnectorPair(
                        firstElement,
                        secondElement,
                        out Connector firstConnector,
                        out Connector secondConnector,
                        out string connectorError))
                    {
                        errorMessage = connectorError;
                        return false;
                    }

                    return TryBuildOffsetRoute(
                        firstElement,
                        firstConnector,
                        secondConnector,
                        normalizedAngle,
                        DegreesToRadians(normalizedAngle),
                        out errorMessage);

                default:
                    errorMessage = "Unsupported routing mode.";
                    return false;
            }
        }

        private bool TryBuildSingleElbowMepCurveRoute(
            MEPCurve firstCurve,
            MEPCurve secondCurve,
            double angleDegrees,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            bool isNinetyDegree = Math.Abs(angleDegrees - 90.0) <= AngleToleranceDegrees;

            if (angleDegrees > 90.0 + AngleToleranceDegrees)
            {
                errorMessage = "Single Elbow supports practical elbow angles up to 90 degrees.";
                return false;
            }

            if (!TryFindSingleElbowCandidate(
                firstCurve,
                secondCurve,
                angleDegrees,
                out Connector chosenFirst,
                out Connector chosenSecond,
                out XYZ oldFirstPoint,
                out XYZ oldSecondPoint,
                out XYZ elbowFirstPoint,
                out XYZ elbowSecondPoint,
                out errorMessage))
            {
                return false;
            }

            if (!TryMoveCurveEnd(firstCurve, oldFirstPoint, elbowFirstPoint, out errorMessage))
            {
                return false;
            }

            if (!TryMoveCurveEnd(secondCurve, oldSecondPoint, elbowSecondPoint, out errorMessage))
            {
                return false;
            }

            _document.Regenerate();

            Connector updatedFirst = SmartConnectConnectorUtils.FindClosestConnector(firstCurve, elbowFirstPoint, true);
            Connector updatedSecond = SmartConnectConnectorUtils.FindClosestConnector(secondCurve, elbowSecondPoint, true);
            if (updatedFirst == null || updatedSecond == null)
            {
                errorMessage = "Failed to get modified connectors after end adjustment.";
                return false;
            }

            Element middleSegment;
            try
            {
                middleSegment = CreateSegmentLike(firstCurve, elbowFirstPoint, elbowSecondPoint);
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to create the middle segment: " + ex.Message;
                return false;
            }

            _document.Regenerate();

            Connector middleFirst = SmartConnectConnectorUtils.FindClosestConnector(middleSegment, elbowFirstPoint, true);
            Connector middleSecond = SmartConnectConnectorUtils.FindClosestConnector(middleSegment, elbowSecondPoint, true);
            if (middleFirst == null || middleSecond == null)
            {
                errorMessage = "Failed to get middle segment connectors.";
                return false;
            }

            ApplyRectangularConnectorSizing(firstCurve, secondCurve, middleSegment, middleFirst, middleSecond);
            _document.Regenerate();

            if (!TryCreateMandatoryElbow(updatedFirst, middleFirst, "first", out errorMessage))
            {
                return false;
            }

            if (!TryCreateMandatoryElbow(updatedSecond, middleSecond, "second", out errorMessage))
            {
                return false;
            }

            if (isNinetyDegree)
            {
                _document.Regenerate();
                EnsureRectangularMiddleSize(firstCurve, secondCurve, middleSegment);
                Connector postFirst = SmartConnectConnectorUtils.FindClosestConnector(middleSegment, elbowFirstPoint, false);
                Connector postSecond = SmartConnectConnectorUtils.FindClosestConnector(middleSegment, elbowSecondPoint, false);
                ApplyRectangularConnectorSizing(firstCurve, secondCurve, middleSegment, postFirst, postSecond);
            }

            return true;
        }

        private bool TryFindSingleElbowCandidate(
            MEPCurve firstCurve,
            MEPCurve secondCurve,
            double angleDegrees,
            out Connector firstConnector,
            out Connector secondConnector,
            out XYZ oldFirstPoint,
            out XYZ oldSecondPoint,
            out XYZ elbowFirstPoint,
            out XYZ elbowSecondPoint,
            out string errorMessage)
        {
            firstConnector = null;
            secondConnector = null;
            oldFirstPoint = null;
            oldSecondPoint = null;
            elbowFirstPoint = null;
            elbowSecondPoint = null;
            errorMessage = string.Empty;

            var firstOpen = SmartConnectConnectorUtils.GetOpenConnectors(firstCurve);
            if (firstOpen.Count == 0)
            {
                errorMessage = "No open connector found on first selected element.";
                return false;
            }

            var secondOpen = SmartConnectConnectorUtils.GetOpenConnectors(secondCurve);
            if (secondOpen.Count == 0)
            {
                errorMessage = "No open connector found on second selected element.";
                return false;
            }

            double bestScore = double.MaxValue;
            string lastGeometryError = string.Empty;

            foreach (Connector first in firstOpen)
            {
                foreach (Connector second in secondOpen)
                {
                    if (!AreConnectorDomainsCompatible(first, second))
                    {
                        continue;
                    }

                    if (!TryComputeSingleElbowPointsForParallelCurves(
                        first,
                        second,
                        angleDegrees,
                        out XYZ candidateOldFirstPoint,
                        out XYZ candidateOldSecondPoint,
                        out XYZ candidateElbowFirstPoint,
                        out XYZ candidateElbowSecondPoint,
                        out string candidateError))
                    {
                        lastGeometryError = candidateError;
                        continue;
                    }

                    double score =
                        first.Origin.DistanceTo(second.Origin) +
                        candidateElbowFirstPoint.DistanceTo(candidateElbowSecondPoint);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        firstConnector = first;
                        secondConnector = second;
                        oldFirstPoint = candidateOldFirstPoint;
                        oldSecondPoint = candidateOldSecondPoint;
                        elbowFirstPoint = candidateElbowFirstPoint;
                        elbowSecondPoint = candidateElbowSecondPoint;
                    }
                }
            }

            if (firstConnector == null ||
                secondConnector == null ||
                oldFirstPoint == null ||
                oldSecondPoint == null ||
                elbowFirstPoint == null ||
                elbowSecondPoint == null)
            {
                errorMessage = string.IsNullOrWhiteSpace(lastGeometryError)
                    ? "No valid connector pair found for single-elbow routing."
                    : lastGeometryError;
                return false;
            }

            return true;
        }

        private static bool AreConnectorDomainsCompatible(Connector first, Connector second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            if (first.Domain == Domain.DomainUndefined || second.Domain == Domain.DomainUndefined)
            {
                return true;
            }

            return first.Domain == second.Domain;
        }

        private bool TryCreateMandatoryElbow(
            Connector firstConnector,
            Connector secondConnector,
            string sideLabel,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                FamilyInstance elbow = _document.Create.NewElbowFitting(firstConnector, secondConnector);
                if (elbow != null)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Could not place the {sideLabel} elbow: {ex.Message}";
                return false;
            }

            errorMessage = $"Could not place the {sideLabel} elbow for the selected configuration.";
            return false;
        }

        private bool TryComputeSingleElbowPointsForParallelCurves(
            Connector firstConnector,
            Connector secondConnector,
            double angleDegrees,
            out XYZ oldFirstPoint,
            out XYZ oldSecondPoint,
            out XYZ elbowFirstPoint,
            out XYZ elbowSecondPoint,
            out string errorMessage)
        {
            oldFirstPoint = null;
            oldSecondPoint = null;
            elbowFirstPoint = null;
            elbowSecondPoint = null;
            errorMessage = string.Empty;

            if (!TryGetConnectorDirectionForRouting(firstConnector, out XYZ firstDirection) ||
                !TryGetConnectorDirectionForRouting(secondConnector, out XYZ secondDirection))
            {
                errorMessage = "Could not read connector directions.";
                return false;
            }

            firstDirection = firstDirection.Normalize();
            secondDirection = secondDirection.Normalize();

            double parallelCheck = Math.Abs(firstDirection.DotProduct(secondDirection));
            if (parallelCheck < 0.999)
            {
                errorMessage = "Single Elbow mode supports only parallel open ends.";
                return false;
            }

            if (firstDirection.DotProduct(secondDirection) > -0.999)
            {
                errorMessage = "Selected open ends are not facing each other correctly.";
                return false;
            }

            XYZ firstPoint = firstConnector.Origin;
            XYZ secondPoint = secondConnector.Origin;
            XYZ offsetVector = secondPoint.Subtract(firstPoint);

            double axisOffset = offsetVector.DotProduct(firstDirection);
            XYZ parallelPart = firstDirection.Multiply(axisOffset);
            XYZ perpendicularVector = offsetVector.Subtract(parallelPart);
            double perpendicularLength = perpendicularVector.GetLength();
            if (perpendicularLength < JoinPointTolerance)
            {
                errorMessage = "Selected elements are already in line. Offset connection is not needed.";
                return false;
            }

            double qAbs;
            if (Math.Abs(angleDegrees - 90.0) <= AngleToleranceDegrees)
            {
                qAbs = 0;
            }
            else
            {
                double tangent = Math.Tan(DegreesToRadians(angleDegrees));
                if (Math.Abs(tangent) <= 1e-6)
                {
                    errorMessage = "Selected angle is not feasible for this offset condition.";
                    return false;
                }

                qAbs = perpendicularLength / Math.Abs(tangent);
            }

            double totalShiftOption1 = axisOffset - qAbs;
            double totalShiftOption2 = axisOffset + qAbs;
            double totalShift = Math.Abs(totalShiftOption1) <= Math.Abs(totalShiftOption2)
                ? totalShiftOption1
                : totalShiftOption2;

            double firstShift = totalShift * 0.5;
            double secondShift = totalShift * 0.5;

            oldFirstPoint = firstPoint;
            oldSecondPoint = secondPoint;
            elbowFirstPoint = firstPoint.Add(firstDirection.Multiply(firstShift));
            elbowSecondPoint = secondPoint.Add(secondDirection.Multiply(secondShift));

            XYZ middleVector = elbowSecondPoint.Subtract(elbowFirstPoint);
            if (middleVector.GetLength() < JoinPointTolerance)
            {
                errorMessage = "Failed to compute middle segment path.";
                return false;
            }

            return true;
        }

        private static bool TryGetConnectorDirectionForRouting(Connector connector, out XYZ direction)
        {
            direction = null;
            if (connector == null || !connector.IsValidObject)
            {
                return false;
            }

            if (TryGetMepCurveEndDirection(connector, out XYZ curveDirection))
            {
                direction = curveDirection;
                return true;
            }

            if (SmartConnectConnectorUtils.TryGetConnectorAxis(connector, out XYZ connectorAxis))
            {
                direction = connectorAxis.Normalize();
                return true;
            }

            return false;
        }

        private static bool TryGetMepCurveEndDirection(Connector connector, out XYZ direction)
        {
            direction = null;
            if (!(connector?.Owner is MEPCurve ownerCurve))
            {
                return false;
            }

            LocationCurve location = ownerCurve.Location as LocationCurve;
            Line line = location?.Curve as Line;
            if (line == null)
            {
                return false;
            }

            XYZ end0 = line.GetEndPoint(0);
            XYZ end1 = line.GetEndPoint(1);
            bool nearEnd0 = end0.DistanceTo(connector.Origin) <= end1.DistanceTo(connector.Origin);

            XYZ outward = nearEnd0 ? end0.Subtract(end1) : end1.Subtract(end0);
            if (outward.GetLength() <= 1e-9)
            {
                return false;
            }

            direction = outward.Normalize();
            return true;
        }

        private bool TryMoveCurveEnd(MEPCurve curve, XYZ oldEndPoint, XYZ newEndPoint, out string errorMessage)
        {
            errorMessage = string.Empty;

            string elementName = curve?.Category?.Name ?? "Element";
            LocationCurve location = curve?.Location as LocationCurve;
            Line line = location?.Curve as Line;
            if (location == null || line == null)
            {
                errorMessage = elementName + " location curve not found.";
                return false;
            }

            XYZ end0 = line.GetEndPoint(0);
            XYZ end1 = line.GetEndPoint(1);

            bool replaceFirst = end0.DistanceTo(oldEndPoint) <= end1.DistanceTo(oldEndPoint);
            XYZ newStart = replaceFirst ? newEndPoint : end0;
            XYZ newEnd = replaceFirst ? end1 : newEndPoint;

            if (newStart.DistanceTo(newEnd) <= MinSegmentLength)
            {
                errorMessage = elementName + " would become too short after end adjustment.";
                return false;
            }

            location.Curve = Line.CreateBound(newStart, newEnd);
            return true;
        }

        private bool TryBuildOffsetRoute(
            Element firstElement,
            Connector firstConnector,
            Connector secondConnector,
            double angleDegrees,
            double angleRadians,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!SmartConnectConnectorUtils.TryGetConnectorAxis(firstConnector, out XYZ firstAxis) ||
                !SmartConnectConnectorUtils.TryGetConnectorAxis(secondConnector, out XYZ secondAxis))
            {
                errorMessage = "Could not resolve connector directions for offset routing.";
                return false;
            }

            XYZ start = firstConnector.Origin;
            XYZ end = secondConnector.Origin;
            XYZ between = end.Subtract(start);

            if (between.GetLength() <= MinSegmentLength)
            {
                errorMessage = "Selected connectors are too close to build an offset route.";
                return false;
            }

            XYZ baseDirection = ChooseBaseDirection(firstAxis, between);
            double parallelAlignment = Math.Abs(baseDirection.DotProduct(secondAxis));
            if (parallelAlignment < Math.Cos(DegreesToRadians(20.0)))
            {
                errorMessage = "Offset + 2 Elbows works best when selected connectors are roughly parallel.";
                return false;
            }

            double parallelComponent = between.DotProduct(baseDirection);
            XYZ parallelVector = baseDirection.Multiply(parallelComponent);
            XYZ offsetVector = between.Subtract(parallelVector);
            double offsetDistance = offsetVector.GetLength();

            if (offsetDistance <= MinSegmentLength)
            {
                errorMessage = "Offset route is not required here. Try Single Elbow mode.";
                return false;
            }

            double tangent = Math.Tan(angleRadians);
            if (Math.Abs(tangent) <= 1e-6)
            {
                errorMessage = "Selected angle is not feasible for offset routing.";
                return false;
            }

            double middleParallel = offsetDistance / tangent;
            if (middleParallel <= MinSegmentLength)
            {
                middleParallel = MinSegmentLength;
            }

            double runDelta = middleParallel - parallelComponent;
            double firstRun = MinRunLength;
            double secondRun = firstRun + runDelta;
            if (secondRun < MinRunLength)
            {
                double shift = MinRunLength - secondRun;
                firstRun += shift;
                secondRun += shift;
            }

            XYZ firstBendPoint = start.Add(baseDirection.Multiply(firstRun));
            XYZ secondBendPoint = end.Add(baseDirection.Multiply(secondRun));

            if (firstBendPoint.DistanceTo(start) <= MinSegmentLength ||
                secondBendPoint.DistanceTo(end) <= MinSegmentLength ||
                firstBendPoint.DistanceTo(secondBendPoint) <= MinSegmentLength)
            {
                errorMessage = "Offset geometry is too short to create reliable segments.";
                return false;
            }

            XYZ middleDirection = secondBendPoint.Subtract(firstBendPoint).Normalize();
            double actualAngle = RadiansToDegrees(baseDirection.AngleTo(middleDirection));
            if (Math.Abs(actualAngle - angleDegrees) > AngleToleranceDegrees)
            {
                errorMessage = "Selected angle cannot be applied to this offset condition.";
                return false;
            }

            return TryCreateThreeSegmentRoute(
                firstElement,
                firstConnector,
                secondConnector,
                firstBendPoint,
                secondBendPoint,
                out errorMessage);
        }

        private bool TryCreateThreeSegmentRoute(
            Element templateElement,
            Connector startConnector,
            Connector endConnector,
            XYZ firstBendPoint,
            XYZ secondBendPoint,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            Element firstSegment = null;
            Element middleSegment = null;
            Element thirdSegment = null;

            try
            {
                firstSegment = CreateSegmentLike(templateElement, startConnector.Origin, firstBendPoint);
                middleSegment = CreateSegmentLike(templateElement, firstBendPoint, secondBendPoint);
                thirdSegment = CreateSegmentLike(templateElement, secondBendPoint, endConnector.Origin);
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to create offset segments: " + ex.Message;
                return false;
            }

            if (firstSegment == null || middleSegment == null || thirdSegment == null)
            {
                errorMessage = "Failed to create one or more offset segments.";
                return false;
            }

            Connector firstStart = SmartConnectConnectorUtils.FindClosestConnector(firstSegment, startConnector.Origin, true);
            Connector firstEnd = SmartConnectConnectorUtils.FindClosestConnector(firstSegment, firstBendPoint, true);
            Connector middleStart = SmartConnectConnectorUtils.FindClosestConnector(middleSegment, firstBendPoint, true);
            Connector middleEnd = SmartConnectConnectorUtils.FindClosestConnector(middleSegment, secondBendPoint, true);
            Connector thirdStart = SmartConnectConnectorUtils.FindClosestConnector(thirdSegment, secondBendPoint, true);
            Connector thirdEnd = SmartConnectConnectorUtils.FindClosestConnector(thirdSegment, endConnector.Origin, true);

            if (firstStart == null ||
                firstEnd == null ||
                middleStart == null ||
                middleEnd == null ||
                thirdStart == null ||
                thirdEnd == null)
            {
                errorMessage = "Could not resolve connectors on offset route segments.";
                return false;
            }

            if (!TryConnectPair(startConnector, firstStart, false, out errorMessage))
            {
                return false;
            }

            if (!TryConnectPair(firstEnd, middleStart, true, out errorMessage))
            {
                return false;
            }

            if (!TryConnectPair(middleEnd, thirdStart, true, out errorMessage))
            {
                return false;
            }

            if (!TryConnectPair(thirdEnd, endConnector, false, out errorMessage))
            {
                return false;
            }

            return true;
        }

        private Element CreateSegmentLike(Element templateElement, XYZ start, XYZ end)
        {
            if (templateElement == null)
            {
                throw new InvalidOperationException("Template element is null.");
            }

            if (start == null || end == null || start.DistanceTo(end) <= MinSegmentLength)
            {
                throw new InvalidOperationException("Segment length is too short.");
            }

            if (templateElement is Pipe sourcePipe)
            {
                return CreatePipeSegment(sourcePipe, start, end);
            }

            if (templateElement is Duct sourceDuct)
            {
                return CreateDuctSegment(sourceDuct, start, end);
            }

            if (templateElement is CableTray sourceCableTray)
            {
                return CreateCableTraySegment(sourceCableTray, start, end);
            }

            throw new InvalidOperationException("Unsupported template element type.");
        }

        private Pipe CreatePipeSegment(Pipe source, XYZ start, XYZ end)
        {
            ElementId systemTypeId = ResolvePipeSystemTypeId(source);
            ElementId pipeTypeId = source.GetTypeId();
            ElementId levelId = ResolveLevelId(source);

            if (IsInvalidId(systemTypeId) || IsInvalidId(pipeTypeId) || IsInvalidId(levelId))
            {
                throw new InvalidOperationException("Pipe type/system/level could not be resolved.");
            }

            Pipe pipe = Pipe.Create(_document, systemTypeId, pipeTypeId, levelId, start, end);
            CopyDoubleParameter(source, pipe, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            return pipe;
        }

        private Duct CreateDuctSegment(Duct source, XYZ start, XYZ end)
        {
            ElementId systemTypeId = ResolveDuctSystemTypeId(source);
            ElementId ductTypeId = source.GetTypeId();
            ElementId levelId = ResolveLevelId(source);

            if (IsInvalidId(systemTypeId) || IsInvalidId(ductTypeId) || IsInvalidId(levelId))
            {
                throw new InvalidOperationException("Duct type/system/level could not be resolved.");
            }

            Duct duct = Duct.Create(_document, systemTypeId, ductTypeId, levelId, start, end);
            CopyDoubleParameter(source, duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
            CopyDoubleParameter(source, duct, BuiltInParameter.PROFILE_ANGLE);
            CopyDoubleParameter(source, duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
            CopyDoubleParameter(source, duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
            ForceRectangularDimensionsMatch(
                source,
                duct,
                BuiltInParameter.RBS_CURVE_WIDTH_PARAM,
                BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
            return duct;
        }

        private CableTray CreateCableTraySegment(CableTray source, XYZ start, XYZ end)
        {
            ElementId typeId = source.GetTypeId();
            ElementId levelId = ResolveLevelId(source);

            if (IsInvalidId(typeId) || IsInvalidId(levelId))
            {
                throw new InvalidOperationException("Cable tray type/level could not be resolved.");
            }

            CableTray cableTray = CableTray.Create(_document, typeId, start, end, levelId);
            CopyDoubleParameter(source, cableTray, BuiltInParameter.PROFILE_ANGLE);
            CopyDoubleParameter(source, cableTray, BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
            CopyDoubleParameter(source, cableTray, BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
            ForceRectangularDimensionsMatch(
                source,
                cableTray,
                BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM,
                BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
            return cableTray;
        }

        private void EnsureRectangularMiddleSize(MEPCurve firstSourceCurve, MEPCurve secondSourceCurve, Element middleSegment)
        {
            if (middleSegment is Duct middleDuct)
            {
                if (firstSourceCurve is Duct firstSourceDuct)
                {
                    ForceRectangularDimensionsMatch(
                        firstSourceDuct,
                        middleDuct,
                        BuiltInParameter.RBS_CURVE_WIDTH_PARAM,
                        BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                }

                if (secondSourceCurve is Duct secondSourceDuct)
                {
                    ForceRectangularDimensionsMatch(
                        secondSourceDuct,
                        middleDuct,
                        BuiltInParameter.RBS_CURVE_WIDTH_PARAM,
                        BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                }

                return;
            }

            if (middleSegment is CableTray middleTray)
            {
                if (firstSourceCurve is CableTray firstSourceTray)
                {
                    ForceRectangularDimensionsMatch(
                        firstSourceTray,
                        middleTray,
                        BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM,
                        BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                }

                if (secondSourceCurve is CableTray secondSourceTray)
                {
                    ForceRectangularDimensionsMatch(
                        secondSourceTray,
                        middleTray,
                        BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM,
                        BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                }
            }
        }

        private void ApplyRectangularConnectorSizing(
            MEPCurve firstSourceCurve,
            MEPCurve secondSourceCurve,
            Element middleSegment,
            Connector firstMiddleConnector,
            Connector secondMiddleConnector)
        {
            if (!TryResolvePreferredRectangularSize(firstSourceCurve, secondSourceCurve, middleSegment, out double width, out double height))
            {
                return;
            }

            TrySetRectangularConnectorSize(firstMiddleConnector, width, height);
            TrySetRectangularConnectorSize(secondMiddleConnector, width, height);
            EnsureRectangularMiddleSize(firstSourceCurve, secondSourceCurve, middleSegment);
        }

        private static bool TryResolvePreferredRectangularSize(
            MEPCurve firstSourceCurve,
            MEPCurve secondSourceCurve,
            Element middleSegment,
            out double width,
            out double height)
        {
            width = 0;
            height = 0;

            if (middleSegment is Duct)
            {
                return TryReadRectangularSize(
                           firstSourceCurve,
                           BuiltInParameter.RBS_CURVE_WIDTH_PARAM,
                           BuiltInParameter.RBS_CURVE_HEIGHT_PARAM,
                           out width,
                           out height) ||
                       TryReadRectangularSize(
                           secondSourceCurve,
                           BuiltInParameter.RBS_CURVE_WIDTH_PARAM,
                           BuiltInParameter.RBS_CURVE_HEIGHT_PARAM,
                           out width,
                           out height);
            }

            if (middleSegment is CableTray)
            {
                return TryReadRectangularSize(
                           firstSourceCurve,
                           BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM,
                           BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM,
                           out width,
                           out height) ||
                       TryReadRectangularSize(
                           secondSourceCurve,
                           BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM,
                           BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM,
                           out width,
                           out height);
            }

            return false;
        }

        private static bool TryReadRectangularSize(
            Element element,
            BuiltInParameter widthParam,
            BuiltInParameter heightParam,
            out double width,
            out double height)
        {
            width = 0;
            height = 0;
            return TryGetDoubleParameter(element, widthParam, out width) &&
                   TryGetDoubleParameter(element, heightParam, out height);
        }

        private static bool TrySetRectangularConnectorSize(Connector connector, double width, double height)
        {
            if (connector == null || !connector.IsValidObject || connector.Shape != ConnectorProfileType.Rectangular)
            {
                return false;
            }

            try
            {
                connector.Width = width;
                connector.Height = height;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ForceRectangularDimensionsMatch(
            Element source,
            Element target,
            BuiltInParameter widthParameterId,
            BuiltInParameter heightParameterId)
        {
            if (!TryGetDoubleParameter(source, widthParameterId, out double sourceWidth) ||
                !TryGetDoubleParameter(source, heightParameterId, out double sourceHeight))
            {
                return;
            }

            bool hasSourceProfileAngle = TryGetDoubleParameter(source, BuiltInParameter.PROFILE_ANGLE, out double sourceProfileAngle);
            if (hasSourceProfileAngle)
            {
                TrySetDoubleParameter(target, BuiltInParameter.PROFILE_ANGLE, sourceProfileAngle);
            }

            if (TryApplyRectangularDimensions(target, widthParameterId, heightParameterId, sourceWidth, sourceHeight))
            {
                return;
            }

            if (!hasSourceProfileAngle || AreAlmostEqual(sourceWidth, sourceHeight))
            {
                return;
            }

            if (TrySetDoubleParameter(target, BuiltInParameter.PROFILE_ANGLE, sourceProfileAngle + QuarterTurnRadians) &&
                TryApplyRectangularDimensions(target, widthParameterId, heightParameterId, sourceWidth, sourceHeight))
            {
                return;
            }

            if (TrySetDoubleParameter(target, BuiltInParameter.PROFILE_ANGLE, sourceProfileAngle - QuarterTurnRadians))
            {
                if (TryApplyRectangularDimensions(target, widthParameterId, heightParameterId, sourceWidth, sourceHeight))
                {
                    return;
                }
            }

            if (!AreAlmostEqual(sourceWidth, sourceHeight))
            {
                if (TryRotateRectangularProfileAndMatch(
                    target,
                    widthParameterId,
                    heightParameterId,
                    sourceWidth,
                    sourceHeight,
                    QuarterTurnRadians))
                {
                    return;
                }

                TryRotateRectangularProfileAndMatch(
                    target,
                    widthParameterId,
                    heightParameterId,
                    sourceWidth,
                    sourceHeight,
                    -QuarterTurnRadians);
            }
        }

        private bool TryApplyRectangularDimensions(
            Element target,
            BuiltInParameter widthParameterId,
            BuiltInParameter heightParameterId,
            double expectedWidth,
            double expectedHeight)
        {
            if (!TrySetDoubleParameter(target, widthParameterId, expectedWidth) ||
                !TrySetDoubleParameter(target, heightParameterId, expectedHeight))
            {
                return false;
            }

            try
            {
                _document.Regenerate();
            }
            catch
            {
                return false;
            }

            if (!TryGetDoubleParameter(target, widthParameterId, out double actualWidth) ||
                !TryGetDoubleParameter(target, heightParameterId, out double actualHeight))
            {
                return false;
            }

            return AreAlmostEqual(actualWidth, expectedWidth) &&
                   AreAlmostEqual(actualHeight, expectedHeight);
        }

        private bool TryRotateRectangularProfileAndMatch(
            Element target,
            BuiltInParameter widthParameterId,
            BuiltInParameter heightParameterId,
            double expectedWidth,
            double expectedHeight,
            double rotationRadians)
        {
            MEPCurve curve = target as MEPCurve;
            LocationCurve location = curve?.Location as LocationCurve;
            Line centerLine = location?.Curve as Line;
            if (centerLine == null)
            {
                return false;
            }

            Line rotationAxis = Line.CreateBound(centerLine.GetEndPoint(0), centerLine.GetEndPoint(1));
            try
            {
                ElementTransformUtils.RotateElement(_document, target.Id, rotationAxis, rotationRadians);
                _document.Regenerate();
            }
            catch
            {
                return false;
            }

            return TryApplyRectangularDimensions(
                target,
                widthParameterId,
                heightParameterId,
                expectedWidth,
                expectedHeight);
        }

        private ElementId ResolvePipeSystemTypeId(Pipe source)
        {
            if (source?.MEPSystem != null)
            {
                ElementId fromSystem = source.MEPSystem.GetTypeId();
                if (!IsInvalidId(fromSystem))
                {
                    return fromSystem;
                }
            }

            ElementId fromParameter = GetElementIdParameter(source, BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
            if (!IsInvalidId(fromParameter))
            {
                return fromParameter;
            }

            return GetFirstTypeId<PipingSystemType>();
        }

        private ElementId ResolveDuctSystemTypeId(Duct source)
        {
            if (source?.MEPSystem != null)
            {
                ElementId fromSystem = source.MEPSystem.GetTypeId();
                if (!IsInvalidId(fromSystem))
                {
                    return fromSystem;
                }
            }

            ElementId fromParameter = GetElementIdParameter(source, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
            if (!IsInvalidId(fromParameter))
            {
                return fromParameter;
            }

            return GetFirstTypeId<MechanicalSystemType>();
        }

        private ElementId GetFirstTypeId<TType>() where TType : Element
        {
            Element first = new FilteredElementCollector(_document)
                .OfClass(typeof(TType))
                .FirstElement();

            return first?.Id ?? ElementId.InvalidElementId;
        }

        private bool TryConnectPair(
            Connector firstConnector,
            Connector secondConnector,
            bool preferElbow,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (firstConnector == null || secondConnector == null)
            {
                errorMessage = "Connector pairing failed during connection.";
                return false;
            }

            if (SmartConnectConnectorUtils.AreConnected(firstConnector, secondConnector))
            {
                return true;
            }

            if (preferElbow && TryCreateElbow(firstConnector, secondConnector))
            {
                return true;
            }

            if (TryConnectDirect(firstConnector, secondConnector))
            {
                return true;
            }

            if (TryCreateUnion(firstConnector, secondConnector))
            {
                return true;
            }

            if (TryCreateTransition(firstConnector, secondConnector))
            {
                return true;
            }

            if (!preferElbow && TryCreateElbow(firstConnector, secondConnector))
            {
                return true;
            }

            errorMessage = "Unable to connect route segments with fittings for the selected configuration.";
            return false;
        }

        private bool TryCreateElbow(Connector firstConnector, Connector secondConnector)
        {
            try
            {
                FamilyInstance fitting = _document.Create.NewElbowFitting(firstConnector, secondConnector);
                return fitting != null;
            }
            catch
            {
                return false;
            }
        }

        private bool TryCreateUnion(Connector firstConnector, Connector secondConnector)
        {
            try
            {
                FamilyInstance fitting = _document.Create.NewUnionFitting(firstConnector, secondConnector);
                return fitting != null;
            }
            catch
            {
                return false;
            }
        }

        private bool TryCreateTransition(Connector firstConnector, Connector secondConnector)
        {
            try
            {
                FamilyInstance fitting = _document.Create.NewTransitionFitting(firstConnector, secondConnector);
                return fitting != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConnectDirect(Connector firstConnector, Connector secondConnector)
        {
            try
            {
                firstConnector.ConnectTo(secondConnector);
                return SmartConnectConnectorUtils.AreConnected(firstConnector, secondConnector);
            }
            catch
            {
                return false;
            }
        }

        private static XYZ ChooseBaseDirection(XYZ firstAxis, XYZ between)
        {
            XYZ normalizedBetween = between.Normalize();
            double forward = firstAxis.DotProduct(normalizedBetween);
            double backward = firstAxis.Multiply(-1.0).DotProduct(normalizedBetween);
            return forward >= backward ? firstAxis : firstAxis.Multiply(-1.0);
        }

        private static ElementId ResolveLevelId(MEPCurve curve)
        {
            if (curve == null)
            {
                return ElementId.InvalidElementId;
            }

            if (curve.ReferenceLevel != null && curve.ReferenceLevel.Id != ElementId.InvalidElementId)
            {
                return curve.ReferenceLevel.Id;
            }

            if (curve.LevelId != ElementId.InvalidElementId)
            {
                return curve.LevelId;
            }

            Parameter startLevel = curve.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            if (startLevel != null && startLevel.StorageType == StorageType.ElementId)
            {
                ElementId value = startLevel.AsElementId();
                if (!IsInvalidId(value))
                {
                    return value;
                }
            }

            return ElementId.InvalidElementId;
        }

        private static ElementId GetElementIdParameter(Element element, BuiltInParameter parameterId)
        {
            Parameter parameter = element?.get_Parameter(parameterId);
            if (parameter == null || parameter.StorageType != StorageType.ElementId)
            {
                return ElementId.InvalidElementId;
            }

            ElementId value = parameter.AsElementId();
            return value ?? ElementId.InvalidElementId;
        }

        private static void CopyDoubleParameter(Element source, Element target, BuiltInParameter parameterId)
        {
            Parameter sourceParameter = source?.get_Parameter(parameterId);
            Parameter targetParameter = target?.get_Parameter(parameterId);

            if (sourceParameter == null ||
                targetParameter == null ||
                sourceParameter.StorageType != StorageType.Double ||
                targetParameter.StorageType != StorageType.Double ||
                targetParameter.IsReadOnly)
            {
                return;
            }

            targetParameter.Set(sourceParameter.AsDouble());
        }

        private static bool TryGetDoubleParameter(Element element, BuiltInParameter parameterId, out double value)
        {
            value = 0;
            Parameter parameter = element?.get_Parameter(parameterId);
            if (parameter == null || parameter.StorageType != StorageType.Double || !parameter.HasValue)
            {
                return false;
            }

            value = parameter.AsDouble();
            return true;
        }

        private static bool TrySetDoubleParameter(Element element, BuiltInParameter parameterId, double value)
        {
            Parameter parameter = element?.get_Parameter(parameterId);
            if (parameter == null || parameter.StorageType != StorageType.Double || parameter.IsReadOnly)
            {
                return false;
            }

            try
            {
                return parameter.Set(value);
            }
            catch
            {
                return false;
            }
        }

        private static bool AreAlmostEqual(double first, double second)
        {
            return Math.Abs(first - second) <= SizeComparisonTolerance;
        }

        private static bool IsInvalidId(ElementId value)
        {
            return value == null || value == ElementId.InvalidElementId || ElementIdHelper.GetIntegerValue(value) <= 0;
        }

        private static double ConvertMillimetersToInternal(double value)
        {
#if REVIT2024_OR_GREATER
            return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
#else
            return UnitUtils.ConvertToInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS);
#endif
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }

    }
}
