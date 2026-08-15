// Tool Name: Connect MEP Elements (Smart Connect) - Route Builder
// Description: Plans and creates Connect MEP Elements routing geometry and fittings between two MEP elements.
// Author: Ajmal P.S.
// Version: 2.0.0
// Last Updated: 2026-08-15
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models, AJTools.Utils
//
// How this works
// --------------
// One planner works out where each open end has to finish up (the "plan point") and how far it
// travels along its own outward direction to get there. Two execution strategies then reach those
// points: trim the existing run's end, or leave the run alone and insert a new piece up to the point.
// Single Elbow mode trims where it is allowed to; Offset + 2 Elbows never touches the picked
// elements, which is what makes equipment, air terminals and flex runs connectable at all.
//
// Every attempt runs inside its own sub-transaction, so a failed angle or mode rolls back cleanly
// and the next one starts from untouched geometry.

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using AJTools.Models;
using AJTools.Utils;

namespace AJTools.Services.SmartConnect
{
    /// <summary>
    /// Builds Connect MEP Elements routes between supported MEP elements.
    /// </summary>
    internal sealed class SmartConnectRouteBuilder
    {
        private const double JoinPointTolerance = 1e-4;
        private const double ParallelDotTolerance = 0.999;
        private const double SizeComparisonTolerance = 1e-8;
        private const double QuarterTurnRadians = Math.PI * 0.5;
        private const double MinPlanAngleDegrees = 5.0;
        private const double MaxPlanAngleDegrees = 175.0;

        private static readonly double MinSegmentLength = RevitCompat.MmToInternal(10.0);
        private static readonly double MinRunLength = RevitCompat.MmToInternal(50.0);

        private static readonly BuiltInCategory[] ClashCategories =
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_CableTray,
            BuiltInCategory.OST_Conduit
        };

        private readonly Document _document;

        public SmartConnectRouteBuilder(Document document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            _document = document;
        }

        /// <summary>
        /// Connects two elements. Must be called inside an already open transaction.
        /// </summary>
        public SmartConnectRouteResult BuildRoute(Element firstElement, Element secondElement, SmartConnectSettings settings)
        {
            SmartConnectSettings effective = settings ?? new SmartConnectSettings();

            if (firstElement == null || secondElement == null || !firstElement.IsValidObject || !secondElement.IsValidObject)
            {
                return SmartConnectRouteResult.Failed("Selected elements are invalid.");
            }

            string compatibilityError;
            if (!SmartConnectSelectionFilter.AreCompatible(firstElement, secondElement, out compatibilityError))
            {
                return SmartConnectRouteResult.Failed(compatibilityError);
            }

            IList<SmartConnectRoutingMode> modes = BuildModeOrder(effective.RoutingMode);
            IList<double> angles = SmartConnectSettingsService.BuildAngleAttemptOrder(effective);

            string lastError = "No workable route was found for these two elements.";

            foreach (SmartConnectRoutingMode mode in modes)
            {
                foreach (double angle in angles)
                {
                    SmartConnectRouteResult attempt = RunAttemptInSubTransaction(
                        firstElement,
                        secondElement,
                        effective,
                        mode,
                        angle);

                    if (attempt.Success)
                    {
                        // Only an offset crank is actually built to the requested angle. A straight
                        // bridge, a corner and a skew take their angle from the shape of the two
                        // runs, so reporting them as "built at X instead of your Y" is nonsense -
                        // it fired on every clean in-line connection.
                        attempt.AngleWasSubstituted =
                            !attempt.AngleFixedByGeometry &&
                            !SmartConnectSettingsService.AreAnglesEqual(attempt.AngleUsedDegrees, effective.SelectedAngleDegrees);
                        return attempt;
                    }

                    if (!string.IsNullOrWhiteSpace(attempt.ErrorMessage))
                    {
                        lastError = attempt.ErrorMessage;
                    }
                }
            }

            return SmartConnectRouteResult.Failed(lastError);
        }

        private SmartConnectRouteResult RunAttemptInSubTransaction(
            Element firstElement,
            Element secondElement,
            SmartConnectSettings settings,
            SmartConnectRoutingMode mode,
            double angleDegrees)
        {
            using (SubTransaction attemptTransaction = new SubTransaction(_document))
            {
                try
                {
                    attemptTransaction.Start();

                    SmartConnectRouteResult result = TryAttempt(firstElement, secondElement, settings, mode, angleDegrees);
                    if (result.Success)
                    {
                        attemptTransaction.Commit();
                        return result;
                    }

                    attemptTransaction.RollBack();
                    return result;
                }
                catch (Exception ex)
                {
                    if (attemptTransaction.HasStarted() && !attemptTransaction.HasEnded())
                    {
                        attemptTransaction.RollBack();
                    }

                    return SmartConnectRouteResult.Failed("Route attempt failed: " + ex.Message);
                }
            }
        }

        // ------------------------------------------------------------------
        // One attempt: plan, then execute
        // ------------------------------------------------------------------

        private SmartConnectRouteResult TryAttempt(
            Element firstElement,
            Element secondElement,
            SmartConnectSettings settings,
            SmartConnectRoutingMode mode,
            double angleDegrees)
        {
            bool forceInsert = mode == SmartConnectRoutingMode.OffsetWithTwoElbows ||
                               settings.MoveMode == SmartConnectMoveMode.None;

            bool mayMoveFirst = !forceInsert &&
                                CanTrimEnd(firstElement) &&
                                (settings.MoveMode == SmartConnectMoveMode.Both ||
                                 settings.MoveMode == SmartConnectMoveMode.FirstOnly);

            bool mayMoveSecond = !forceInsert &&
                                 CanTrimEnd(secondElement) &&
                                 (settings.MoveMode == SmartConnectMoveMode.Both ||
                                  settings.MoveMode == SmartConnectMoveMode.SecondOnly);

            SmartConnectRoutePlan plan;
            string planError;
            if (!TryPlanRoute(
                firstElement,
                secondElement,
                settings,
                angleDegrees,
                mayMoveFirst,
                mayMoveSecond,
                out plan,
                out planError))
            {
                return SmartConnectRouteResult.Failed(planError);
            }

            var result = new SmartConnectRouteResult
            {
                ModeUsed = mode,
                PlanUsed = plan.Kind,
                AngleUsedDegrees = plan.ResultingAngleDegrees,
                AngleFixedByGeometry = plan.AngleFixedByGeometry
            };

            string executeError;
            if (!TryExecutePlan(firstElement, secondElement, plan, settings, mayMoveFirst, mayMoveSecond, result, out executeError))
            {
                return SmartConnectRouteResult.Failed(executeError);
            }

            // A straight bridge has no bend to report; only a corner or skew is worth mentioning,
            // because there the geometry overrides the angle the user chose.
            if (plan.AngleFixedByGeometry && plan.Kind != SmartConnectPlanKind.Inline)
            {
                result.Warnings.Add(string.Format(
                    "The geometry fixed the bend at {0:0.##}° - the chosen angle does not apply to a {1}.",
                    plan.ResultingAngleDegrees,
                    result.DescribePlan()));
            }

            result.Success = true;
            return result;
        }

        private static IList<SmartConnectRoutingMode> BuildModeOrder(SmartConnectRoutingMode requested)
        {
            switch (requested)
            {
                case SmartConnectRoutingMode.SingleElbow:
                    return new List<SmartConnectRoutingMode> { SmartConnectRoutingMode.SingleElbow };
                case SmartConnectRoutingMode.OffsetWithTwoElbows:
                    return new List<SmartConnectRoutingMode> { SmartConnectRoutingMode.OffsetWithTwoElbows };
                default:
                    return new List<SmartConnectRoutingMode>
                    {
                        SmartConnectRoutingMode.SingleElbow,
                        SmartConnectRoutingMode.OffsetWithTwoElbows
                    };
            }
        }

        // ------------------------------------------------------------------
        // Planner
        // ------------------------------------------------------------------

        private bool TryPlanRoute(
            Element firstElement,
            Element secondElement,
            SmartConnectSettings settings,
            double angleDegrees,
            bool mayMoveFirst,
            bool mayMoveSecond,
            out SmartConnectRoutePlan plan,
            out string errorMessage)
        {
            plan = null;
            errorMessage = string.Empty;

            IList<Connector> firstOpen = SmartConnectConnectorUtils.GetOpenConnectors(firstElement);
            if (firstOpen.Count == 0)
            {
                errorMessage = "No free open end found on the first selected element.";
                return false;
            }

            IList<Connector> secondOpen = SmartConnectConnectorUtils.GetOpenConnectors(secondElement);
            if (secondOpen.Count == 0)
            {
                errorMessage = "No free open end found on the second selected element.";
                return false;
            }

            SmartConnectRoutePlan best = null;
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

                    SmartConnectRoutePlan candidate;
                    string candidateError;
                    if (!TryPlanConnectorPair(
                        first,
                        second,
                        settings,
                        angleDegrees,
                        mayMoveFirst,
                        mayMoveSecond,
                        out candidate,
                        out candidateError))
                    {
                        lastGeometryError = candidateError;
                        continue;
                    }

                    // Prefer the shortest overall intervention: how far the ends travel plus
                    // how long the bridging piece ends up.
                    double score = Math.Abs(candidate.FirstShift) +
                                   Math.Abs(candidate.SecondShift) +
                                   candidate.FirstPlanPoint.DistanceTo(candidate.SecondPlanPoint);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
            }

            if (best == null)
            {
                errorMessage = string.IsNullOrWhiteSpace(lastGeometryError)
                    ? "No workable pair of open ends was found for these two elements."
                    : lastGeometryError;
                return false;
            }

            plan = best;
            return true;
        }

        private bool TryPlanConnectorPair(
            Connector firstConnector,
            Connector secondConnector,
            SmartConnectSettings settings,
            double angleDegrees,
            bool mayMoveFirst,
            bool mayMoveSecond,
            out SmartConnectRoutePlan plan,
            out string errorMessage)
        {
            plan = null;
            errorMessage = string.Empty;

            XYZ firstDirection;
            XYZ secondDirection;
            if (!TryGetOutwardDirection(firstConnector, out firstDirection) ||
                !TryGetOutwardDirection(secondConnector, out secondDirection))
            {
                errorMessage = "Could not read the direction of one of the open ends.";
                return false;
            }

            XYZ firstPoint = firstConnector.Origin;
            XYZ secondPoint = secondConnector.Origin;
            double directionDot = firstDirection.DotProduct(secondDirection);

            if (directionDot > ParallelDotTolerance)
            {
                errorMessage = "Both open ends point the same way - a U-shaped route is not supported.";
                return false;
            }

            if (directionDot < -ParallelDotTolerance)
            {
                return TryPlanParallelPair(
                    firstConnector,
                    secondConnector,
                    firstDirection,
                    secondDirection,
                    firstPoint,
                    secondPoint,
                    angleDegrees,
                    mayMoveFirst,
                    mayMoveSecond,
                    out plan,
                    out errorMessage);
            }

            if (!settings.AllowNonParallelEnds)
            {
                errorMessage = "These open ends are not parallel. In the settings, click \"Show advanced settings\" and tick \"Runs that are not parallel to each other\".";
                return false;
            }

            return TryPlanNonParallelPair(
                firstConnector,
                secondConnector,
                firstDirection,
                secondDirection,
                firstPoint,
                secondPoint,
                mayMoveFirst,
                mayMoveSecond,
                out plan,
                out errorMessage);
        }

        /// <summary>
        /// Ends that face each other head on: either in line (straight bridge) or offset sideways
        /// (the classic crank, where the requested angle drives how far the ends travel).
        /// </summary>
        private bool TryPlanParallelPair(
            Connector firstConnector,
            Connector secondConnector,
            XYZ firstDirection,
            XYZ secondDirection,
            XYZ firstPoint,
            XYZ secondPoint,
            double angleDegrees,
            bool mayMoveFirst,
            bool mayMoveSecond,
            out SmartConnectRoutePlan plan,
            out string errorMessage)
        {
            plan = null;
            errorMessage = string.Empty;

            XYZ between = secondPoint.Subtract(firstPoint);
            double axisOffset = between.DotProduct(firstDirection);
            XYZ perpendicularVector = between.Subtract(firstDirection.Multiply(axisOffset));
            double perpendicularLength = perpendicularVector.GetLength();

            if (perpendicularLength < JoinPointTolerance)
            {
                // Dead in line with each other - a straight bridging piece is all that is needed.
                if (axisOffset <= MinSegmentLength)
                {
                    errorMessage = "The two ends are in line but already touching - there is no gap to bridge.";
                    return false;
                }

                plan = new SmartConnectRoutePlan
                {
                    Kind = SmartConnectPlanKind.Inline,
                    FirstConnector = firstConnector,
                    SecondConnector = secondConnector,
                    FirstDirection = firstDirection,
                    SecondDirection = secondDirection,
                    FirstPlanPoint = firstPoint,
                    SecondPlanPoint = secondPoint,
                    FirstShift = 0,
                    SecondShift = 0,
                    NeedsMiddleSegment = true,
                    ResultingAngleDegrees = 180.0,
                    AngleFixedByGeometry = true
                };

                return true;
            }

            double requiredAxialGap;
            if (SmartConnectSettingsService.AreAnglesEqual(angleDegrees, 90.0))
            {
                requiredAxialGap = 0;
            }
            else
            {
                double tangent = Math.Tan(DegreesToRadians(angleDegrees));
                if (Math.Abs(tangent) <= 1e-6)
                {
                    errorMessage = "That angle cannot be built for this offset.";
                    return false;
                }

                requiredAxialGap = perpendicularLength / Math.Abs(tangent);
            }

            // The bridging piece leaves the first run along its own outward direction, so the axial
            // gap it has to cover must stay POSITIVE: the bend angle satisfies
            //     tan(angle) = perpendicularLength / (axisOffset - totalShift)
            // and only axisOffset - requiredAxialGap keeps that term positive. Adding the gap
            // instead lands on the supplement (180 - angle), which folds the bridge back over the
            // run it just left. An earlier version chose between the two by smallest travel, which
            // silently picked the fold-back for every pair whose open ends had already passed each
            // other - the ordinary overlapping crank. Angles are capped at 90 degrees, so there is
            // no case where the other option is the right one.
            double totalShift = axisOffset - requiredAxialGap;

            double firstShift;
            double secondShift;
            if (!TryDistributeShift(totalShift, mayMoveFirst, mayMoveSecond, out firstShift, out secondShift, out errorMessage))
            {
                return false;
            }

            plan = new SmartConnectRoutePlan
            {
                Kind = SmartConnectPlanKind.ParallelOffset,
                FirstConnector = firstConnector,
                SecondConnector = secondConnector,
                FirstDirection = firstDirection,
                SecondDirection = secondDirection,
                FirstPlanPoint = firstPoint.Add(firstDirection.Multiply(firstShift)),
                SecondPlanPoint = secondPoint.Add(secondDirection.Multiply(secondShift)),
                FirstShift = firstShift,
                SecondShift = secondShift,
                NeedsMiddleSegment = true,
                ResultingAngleDegrees = angleDegrees
            };

            if (plan.FirstPlanPoint.DistanceTo(plan.SecondPlanPoint) < MinSegmentLength)
            {
                plan = null;
                errorMessage = string.Format(
                    "These two runs are only {0:0.#} mm apart sideways - too little to crank between them. Move one run so they are either dead in line or at least {1:0} mm apart.",
                    RevitCompat.InternalToMm(perpendicularLength),
                    RevitCompat.InternalToMm(MinSegmentLength));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Ends at an angle to each other: if the two run axes cross, both ends reach the crossing
        /// point and share one elbow; if they pass by each other, they are bridged on their common
        /// perpendicular. Either way the geometry, not the settings, decides the bend angle.
        /// </summary>
        private bool TryPlanNonParallelPair(
            Connector firstConnector,
            Connector secondConnector,
            XYZ firstDirection,
            XYZ secondDirection,
            XYZ firstPoint,
            XYZ secondPoint,
            bool mayMoveFirst,
            bool mayMoveSecond,
            out SmartConnectRoutePlan plan,
            out string errorMessage)
        {
            plan = null;
            errorMessage = string.Empty;

            double firstTravel;
            double secondTravel;
            if (!TryGetClosestApproach(
                firstPoint,
                firstDirection,
                secondPoint,
                secondDirection,
                out firstTravel,
                out secondTravel))
            {
                errorMessage = "Could not work out where these two runs meet.";
                return false;
            }

            XYZ firstPlanPoint = firstPoint.Add(firstDirection.Multiply(firstTravel));
            XYZ secondPlanPoint = secondPoint.Add(secondDirection.Multiply(secondTravel));
            double gap = firstPlanPoint.DistanceTo(secondPlanPoint);

            if (!mayMoveFirst && firstTravel < -JoinPointTolerance)
            {
                errorMessage = "The first element would have to be shortened, but it is set not to move.";
                return false;
            }

            if (!mayMoveSecond && secondTravel < -JoinPointTolerance)
            {
                errorMessage = "The second element would have to be shortened, but it is set not to move.";
                return false;
            }

            double bendAngle = RadiansToDegrees(firstDirection.AngleTo(secondDirection.Negate()));

            // This branch is entered as little as 2.6 degrees away from dead parallel, and the
            // closest-approach solve is badly conditioned there: dividing by (1 - dot^2) sends the
            // travels off to enormous distances, so two near-parallel runs would be dragged hundreds
            // of metres to "meet". Screen the deflection for BOTH the corner and the skew case.
            if (bendAngle < MinPlanAngleDegrees || bendAngle > MaxPlanAngleDegrees)
            {
                errorMessage = string.Format(
                    "These two runs are only {0:0.##}° apart from being parallel, which is too shallow to bridge.",
                    bendAngle < MinPlanAngleDegrees ? bendAngle : 180.0 - bendAngle);
                return false;
            }

            // Even inside that range, refuse a solve that wants to travel far further than the two
            // ends are actually apart - that is the signature of an ill-conditioned result.
            double endSeparation = firstPoint.DistanceTo(secondPoint);
            double travelLimit = Math.Max(4.0 * endSeparation, MinRunLength * 10.0);
            if (Math.Abs(firstTravel) > travelLimit || Math.Abs(secondTravel) > travelLimit)
            {
                errorMessage = "These two runs would have to be stretched an unreasonable distance to meet.";
                return false;
            }

            if (gap <= JoinPointTolerance)
            {
                plan = new SmartConnectRoutePlan
                {
                    Kind = SmartConnectPlanKind.Corner,
                    FirstConnector = firstConnector,
                    SecondConnector = secondConnector,
                    FirstDirection = firstDirection,
                    SecondDirection = secondDirection,
                    FirstPlanPoint = firstPlanPoint,
                    SecondPlanPoint = firstPlanPoint,
                    FirstShift = firstTravel,
                    SecondShift = secondTravel,
                    NeedsMiddleSegment = false,
                    ResultingAngleDegrees = bendAngle,
                    AngleFixedByGeometry = true
                };

                return true;
            }

            if (gap < MinSegmentLength)
            {
                errorMessage = "These two runs pass too close to each other to fit a bridging piece between them.";
                return false;
            }

            plan = new SmartConnectRoutePlan
            {
                Kind = SmartConnectPlanKind.Skew,
                FirstConnector = firstConnector,
                SecondConnector = secondConnector,
                FirstDirection = firstDirection,
                SecondDirection = secondDirection,
                FirstPlanPoint = firstPlanPoint,
                SecondPlanPoint = secondPlanPoint,
                FirstShift = firstTravel,
                SecondShift = secondTravel,
                NeedsMiddleSegment = true,
                ResultingAngleDegrees = 90.0,
                AngleFixedByGeometry = true
            };

            return true;
        }

        private static bool TryDistributeShift(
            double totalShift,
            bool mayMoveFirst,
            bool mayMoveSecond,
            out double firstShift,
            out double secondShift,
            out string errorMessage)
        {
            firstShift = 0;
            secondShift = 0;
            errorMessage = string.Empty;

            if (Math.Abs(totalShift) < JoinPointTolerance)
            {
                return true;
            }

            // Where an end is not allowed to be trimmed, the travel has to be positive so a new
            // piece can be inserted instead. A negative travel there is simply not buildable.
            bool needsShortening = totalShift < 0;

            if (mayMoveFirst && mayMoveSecond)
            {
                firstShift = totalShift * 0.5;
                secondShift = totalShift * 0.5;
                return true;
            }

            if (mayMoveFirst)
            {
                firstShift = totalShift;
                return true;
            }

            if (mayMoveSecond)
            {
                secondShift = totalShift;
                return true;
            }

            // Neither end may be trimmed: split the travel and insert a new piece on each side.
            if (needsShortening)
            {
                errorMessage = "These ends would have to be shortened to meet, but neither element is allowed to move.";
                return false;
            }

            if (totalShift < 2.0 * MinRunLength)
            {
                errorMessage = "There is not enough room to insert new pieces without shortening the picked elements.";
                return false;
            }

            firstShift = totalShift * 0.5;
            secondShift = totalShift * 0.5;
            return true;
        }

        // ------------------------------------------------------------------
        // Executor
        // ------------------------------------------------------------------

        private bool TryExecutePlan(
            Element firstElement,
            Element secondElement,
            SmartConnectRoutePlan plan,
            SmartConnectSettings settings,
            bool mayMoveFirst,
            bool mayMoveSecond,
            SmartConnectRouteResult result,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            SegmentTemplate template;
            if (!TryResolveSegmentTemplate(firstElement, secondElement, out template, out errorMessage))
            {
                return false;
            }

            var createdIds = new List<ElementId>();

            ConnectorSnapshot firstSnapshot = ConnectorSnapshot.Capture(plan.FirstConnector);
            ConnectorSnapshot secondSnapshot = ConnectorSnapshot.Capture(plan.SecondConnector);

            ElementId firstTerminalId;
            if (!TryReachPlanPoint(
                firstElement,
                firstSnapshot,
                plan.FirstPlanPoint,
                plan.FirstShift,
                mayMoveFirst,
                template,
                "first",
                createdIds,
                out firstTerminalId,
                out errorMessage))
            {
                return false;
            }

            ElementId secondTerminalId;
            if (!TryReachPlanPoint(
                secondElement,
                secondSnapshot,
                plan.SecondPlanPoint,
                plan.SecondShift,
                mayMoveSecond,
                template,
                "second",
                createdIds,
                out secondTerminalId,
                out errorMessage))
            {
                return false;
            }

            _document.Regenerate();

            if (!TryBridgePlanPoints(
                plan,
                template,
                firstSnapshot,
                secondSnapshot,
                firstTerminalId,
                secondTerminalId,
                settings,
                createdIds,
                out errorMessage))
            {
                return false;
            }

            _document.Regenerate();

            ApplyRunQuality(firstElement, secondElement, createdIds, settings, result);

            if (settings.WarnOnClash)
            {
                CheckForClashes(createdIds, firstElement, secondElement, result);
            }

            return true;
        }

        /// <summary>
        /// Gets one end of the route to its plan point, either by trimming the picked element or by
        /// inserting a new piece, and reports which element now carries the connector to bridge from.
        /// </summary>
        private bool TryReachPlanPoint(
            Element element,
            ConnectorSnapshot snapshot,
            XYZ planPoint,
            double shift,
            bool mayMove,
            SegmentTemplate template,
            string sideLabel,
            List<ElementId> createdIds,
            out ElementId terminalElementId,
            out string errorMessage)
        {
            terminalElementId = element.Id;
            errorMessage = string.Empty;

            if (Math.Abs(shift) < JoinPointTolerance)
            {
                return true;
            }

            if (mayMove)
            {
                MEPCurve curve = element as MEPCurve;
                if (curve != null && TryMoveCurveEnd(curve, snapshot.Origin, planPoint, out errorMessage))
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = "Could not move the " + sideLabel + " element's end.";
                }

                return false;
            }

            if (shift < MinSegmentLength)
            {
                errorMessage = "The " + sideLabel + " element would have to be shortened, but it is set not to move.";
                return false;
            }

            Element inserted;
            try
            {
                inserted = CreateSegment(template, snapshot, snapshot.Origin, planPoint);
            }
            catch (Exception ex)
            {
                errorMessage = "Could not create the piece leading out of the " + sideLabel + " element: " + ex.Message;
                return false;
            }

            if (inserted == null)
            {
                errorMessage = "Could not create the piece leading out of the " + sideLabel + " element.";
                return false;
            }

            createdIds.Add(inserted.Id);
            _document.Regenerate();

            Connector ownerConnector = FindConnectorAt(element, snapshot.Origin, false);
            Connector insertedConnector = FindConnectorAt(inserted, snapshot.Origin, true);
            if (ownerConnector == null || insertedConnector == null)
            {
                errorMessage = "Could not line up the new piece with the " + sideLabel + " element.";
                return false;
            }

            if (!TryConnectPair(ownerConnector, insertedConnector, false, out errorMessage))
            {
                return false;
            }

            terminalElementId = inserted.Id;
            return true;
        }

        private bool TryBridgePlanPoints(
            SmartConnectRoutePlan plan,
            SegmentTemplate template,
            ConnectorSnapshot firstSnapshot,
            ConnectorSnapshot secondSnapshot,
            ElementId firstTerminalId,
            ElementId secondTerminalId,
            SmartConnectSettings settings,
            List<ElementId> createdIds,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            Element firstTerminal = _document.GetElement(firstTerminalId);
            Element secondTerminal = _document.GetElement(secondTerminalId);
            if (firstTerminal == null || secondTerminal == null)
            {
                errorMessage = "Lost track of the route's end pieces.";
                return false;
            }

            double bridgeLength = plan.FirstPlanPoint.DistanceTo(plan.SecondPlanPoint);
            bool preferElbow = plan.Kind != SmartConnectPlanKind.Inline;

            if (!plan.NeedsMiddleSegment || bridgeLength < MinSegmentLength)
            {
                Connector firstEnd = FindConnectorAt(firstTerminal, plan.FirstPlanPoint, true);
                Connector secondEnd = FindConnectorAt(secondTerminal, plan.SecondPlanPoint, true);
                if (firstEnd == null || secondEnd == null)
                {
                    errorMessage = "Could not find the open ends to join at the corner.";
                    return false;
                }

                return TryConnectPair(firstEnd, secondEnd, preferElbow, out errorMessage);
            }

            bool needsTransition = settings.AutoTransitionOnSizeMismatch &&
                                   !AreEndSizesEqual(firstSnapshot, secondSnapshot) &&
                                   bridgeLength >= 4.0 * MinSegmentLength;

            var middleIds = new List<ElementId>();
            if (needsTransition)
            {
                XYZ midPoint = MidPoint(plan.FirstPlanPoint, plan.SecondPlanPoint);

                Element middleFirst;
                Element middleSecond;
                try
                {
                    middleFirst = CreateSegment(template, firstSnapshot, plan.FirstPlanPoint, midPoint);
                    middleSecond = CreateSegment(template, secondSnapshot, midPoint, plan.SecondPlanPoint);
                }
                catch (Exception ex)
                {
                    errorMessage = "Could not create the bridging pieces: " + ex.Message;
                    return false;
                }

                if (middleFirst == null || middleSecond == null)
                {
                    errorMessage = "Could not create the bridging pieces.";
                    return false;
                }

                createdIds.Add(middleFirst.Id);
                createdIds.Add(middleSecond.Id);
                middleIds.Add(middleFirst.Id);
                middleIds.Add(middleSecond.Id);

                _document.Regenerate();

                Connector transitionFirst = FindConnectorAt(middleFirst, midPoint, true);
                Connector transitionSecond = FindConnectorAt(middleSecond, midPoint, true);
                if (transitionFirst == null || transitionSecond == null)
                {
                    errorMessage = "Could not line up the size change in the middle of the route.";
                    return false;
                }

                if (!TryCreateTransition(transitionFirst, transitionSecond) &&
                    !TryConnectDirect(transitionFirst, transitionSecond))
                {
                    errorMessage = "The two elements are different sizes and no transition fitting could be placed between them.";
                    return false;
                }
            }
            else
            {
                Element middle;
                try
                {
                    middle = CreateSegment(template, firstSnapshot, plan.FirstPlanPoint, plan.SecondPlanPoint);
                }
                catch (Exception ex)
                {
                    errorMessage = "Could not create the bridging piece: " + ex.Message;
                    return false;
                }

                if (middle == null)
                {
                    errorMessage = "Could not create the bridging piece.";
                    return false;
                }

                createdIds.Add(middle.Id);
                middleIds.Add(middle.Id);
            }

            _document.Regenerate();

            Element middleAtFirst = _document.GetElement(middleIds.First());
            Element middleAtSecond = _document.GetElement(middleIds.Last());

            Connector firstOuter = FindConnectorAt(firstTerminal, plan.FirstPlanPoint, true);
            Connector middleFirstEnd = FindConnectorAt(middleAtFirst, plan.FirstPlanPoint, true);
            if (firstOuter == null || middleFirstEnd == null)
            {
                errorMessage = "Could not join the bridging piece to the first element.";
                return false;
            }

            ApplyRectangularConnectorSize(middleFirstEnd, firstSnapshot);

            if (!TryConnectPair(firstOuter, middleFirstEnd, preferElbow, out errorMessage))
            {
                return false;
            }

            _document.Regenerate();

            Element secondTerminalRefreshed = _document.GetElement(secondTerminalId);
            middleAtSecond = _document.GetElement(middleIds.Last());

            Connector secondOuter = FindConnectorAt(secondTerminalRefreshed, plan.SecondPlanPoint, true);
            Connector middleSecondEnd = FindConnectorAt(middleAtSecond, plan.SecondPlanPoint, true);
            if (secondOuter == null || middleSecondEnd == null)
            {
                errorMessage = "Could not join the bridging piece to the second element.";
                return false;
            }

            ApplyRectangularConnectorSize(middleSecondEnd, secondSnapshot);

            return TryConnectPair(secondOuter, middleSecondEnd, preferElbow, out errorMessage);
        }

        // ------------------------------------------------------------------
        // Quality: insulation, lining, instance parameters, clash warning
        // ------------------------------------------------------------------

        private void ApplyRunQuality(
            Element firstElement,
            Element secondElement,
            List<ElementId> createdIds,
            SmartConnectSettings settings,
            SmartConnectRouteResult result)
        {
            if (createdIds.Count == 0)
            {
                return;
            }

            Element source = firstElement is MEPCurve ? firstElement : secondElement;
            if (!(source is MEPCurve))
            {
                return;
            }

            if (settings.CopyInstanceParameters)
            {
                foreach (ElementId createdId in createdIds)
                {
                    Element created = _document.GetElement(createdId);
                    if (created == null)
                    {
                        continue;
                    }

                    CopyStringParameter(source, created, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    CopyStringParameter(source, created, BuiltInParameter.ALL_MODEL_MARK);
                    CopyElementIdParameter(source, created, BuiltInParameter.ELEM_PARTITION_PARAM);
                }
            }

            if (!settings.CopyInsulationAndLining)
            {
                return;
            }

            int copied = 0;
            int failed = 0;

            foreach (ElementId createdId in createdIds)
            {
                Element created = _document.GetElement(createdId);
                if (!(created is MEPCurve))
                {
                    continue;
                }

                if (TryCopyInsulationAndLining(source, created))
                {
                    copied++;
                }
                else
                {
                    failed++;
                }
            }

            if (failed > 0 && copied == 0)
            {
                result.Warnings.Add("Insulation or lining could not be copied onto the new pieces.");
            }
        }

        private bool TryCopyInsulationAndLining(Element source, Element target)
        {
            bool anythingAttempted = false;

            try
            {
                ICollection<ElementId> insulationIds = InsulationLiningBase.GetInsulationIds(_document, source.Id);
                foreach (ElementId insulationId in insulationIds)
                {
                    InsulationLiningBase insulation = _document.GetElement(insulationId) as InsulationLiningBase;
                    if (insulation == null)
                    {
                        continue;
                    }

                    anythingAttempted = true;
                    if (target is Duct)
                    {
                        DuctInsulation.Create(_document, target.Id, insulation.GetTypeId(), insulation.Thickness);
                    }
                    else if (target is Pipe)
                    {
                        PipeInsulation.Create(_document, target.Id, insulation.GetTypeId(), insulation.Thickness);
                    }
                }

                ICollection<ElementId> liningIds = InsulationLiningBase.GetLiningIds(_document, source.Id);
                foreach (ElementId liningId in liningIds)
                {
                    InsulationLiningBase lining = _document.GetElement(liningId) as InsulationLiningBase;
                    if (lining == null || !(target is Duct))
                    {
                        continue;
                    }

                    anythingAttempted = true;
                    DuctLining.Create(_document, target.Id, lining.GetTypeId(), lining.Thickness);
                }
            }
            catch
            {
                return !anythingAttempted;
            }

            return true;
        }

        private void CheckForClashes(
            List<ElementId> createdIds,
            Element firstElement,
            Element secondElement,
            SmartConnectRouteResult result)
        {
            var ignoreIds = new HashSet<ElementId>(createdIds);
            ignoreIds.Add(firstElement.Id);
            ignoreIds.Add(secondElement.Id);

            var clashingNames = new HashSet<string>();

            foreach (ElementId createdId in createdIds)
            {
                Element created = _document.GetElement(createdId);
                if (created == null || !(created is MEPCurve))
                {
                    continue;
                }

                try
                {
                    var categoryFilter = new ElementMulticategoryFilter(ClashCategories);
                    ICollection<Element> hits = new FilteredElementCollector(_document)
                        .WhereElementIsNotElementType()
                        .WherePasses(categoryFilter)
                        .WherePasses(new ElementIntersectsElementFilter(created))
                        .ToElements();

                    foreach (Element hit in hits)
                    {
                        if (hit == null || ignoreIds.Contains(hit.Id))
                        {
                            continue;
                        }

                        Category category = hit.Category;
                        clashingNames.Add(category == null ? "Element" : category.Name);
                    }
                }
                catch
                {
                    // A clash check must never sink an otherwise good connection.
                }
            }

            if (clashingNames.Count > 0)
            {
                result.Warnings.Add("The new route overlaps: " + string.Join(", ", clashingNames.OrderBy(name => name).ToArray()) + ".");
            }
        }

        // ------------------------------------------------------------------
        // Segment creation
        // ------------------------------------------------------------------

        private sealed class SegmentTemplate
        {
            public MEPCurve SourceCurve { get; set; }

            public ElementId TypeId { get; set; }

            public ElementId SystemTypeId { get; set; }

            public ElementId LevelId { get; set; }

            public SegmentKind Kind { get; set; }
        }

        private enum SegmentKind
        {
            Pipe,
            Duct,
            CableTray,
            Conduit
        }

        /// <summary>
        /// Snapshot of a connector taken before any geometry changes, because Connector objects go
        /// stale the moment the document regenerates.
        /// </summary>
        private sealed class ConnectorSnapshot
        {
            public XYZ Origin { get; set; }

            public ConnectorProfileType Shape { get; set; }

            public double Radius { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }

            public static ConnectorSnapshot Capture(Connector connector)
            {
                var snapshot = new ConnectorSnapshot
                {
                    Origin = connector.Origin,
                    Shape = connector.Shape
                };

                try
                {
                    if (connector.Shape == ConnectorProfileType.Round)
                    {
                        snapshot.Radius = connector.Radius;
                    }
                    else if (connector.Shape == ConnectorProfileType.Rectangular ||
                             connector.Shape == ConnectorProfileType.Oval)
                    {
                        snapshot.Width = connector.Width;
                        snapshot.Height = connector.Height;
                    }
                }
                catch
                {
                    // Some connectors refuse to report a size; the segment then keeps the type default.
                }

                return snapshot;
            }
        }

        private bool TryResolveSegmentTemplate(Element firstElement, Element secondElement, out SegmentTemplate template, out string errorMessage)
        {
            template = null;
            errorMessage = string.Empty;

            SegmentTemplate fromFirst = TryBuildTemplate(firstElement);
            SegmentTemplate fromSecond = TryBuildTemplate(secondElement);

            template = fromFirst ?? fromSecond;
            if (template == null)
            {
                errorMessage = "Pick at least one pipe, duct, cable tray or conduit - two equipment items on their own cannot be joined.";
                return false;
            }

            return true;
        }

        private SegmentTemplate TryBuildTemplate(Element element)
        {
            MEPCurve curve = element as MEPCurve;
            if (curve == null)
            {
                return null;
            }

            ElementId levelId = ResolveLevelId(curve);

            Pipe pipe = curve as Pipe;
            if (pipe != null)
            {
                return BuildTemplate(SegmentKind.Pipe, curve, pipe.GetTypeId(), ResolvePipeSystemTypeId(pipe), levelId);
            }

            Duct duct = curve as Duct;
            if (duct != null)
            {
                return BuildTemplate(SegmentKind.Duct, curve, duct.GetTypeId(), ResolveDuctSystemTypeId(duct), levelId);
            }

            CableTray cableTray = curve as CableTray;
            if (cableTray != null)
            {
                return BuildTemplate(SegmentKind.CableTray, curve, cableTray.GetTypeId(), ElementId.InvalidElementId, levelId);
            }

            Conduit conduit = curve as Conduit;
            if (conduit != null)
            {
                return BuildTemplate(SegmentKind.Conduit, curve, conduit.GetTypeId(), ElementId.InvalidElementId, levelId);
            }

            // Flex runs cannot be created by this tool, so borrow a rigid type of the same discipline.
            FlexDuct flexDuct = curve as FlexDuct;
            if (flexDuct != null)
            {
                return BuildTemplate(
                    SegmentKind.Duct,
                    curve,
                    GetFirstTypeId<DuctType>(),
                    GetElementIdParameter(curve, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM),
                    levelId);
            }

            FlexPipe flexPipe = curve as FlexPipe;
            if (flexPipe != null)
            {
                return BuildTemplate(
                    SegmentKind.Pipe,
                    curve,
                    GetFirstTypeId<PipeType>(),
                    GetElementIdParameter(curve, BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM),
                    levelId);
            }

            return null;
        }

        private SegmentTemplate BuildTemplate(
            SegmentKind kind,
            MEPCurve sourceCurve,
            ElementId typeId,
            ElementId systemTypeId,
            ElementId levelId)
        {
            if (IsInvalidId(typeId) || IsInvalidId(levelId))
            {
                return null;
            }

            if ((kind == SegmentKind.Pipe || kind == SegmentKind.Duct) && IsInvalidId(systemTypeId))
            {
                systemTypeId = kind == SegmentKind.Pipe
                    ? GetFirstTypeId<PipingSystemType>()
                    : GetFirstTypeId<MechanicalSystemType>();

                if (IsInvalidId(systemTypeId))
                {
                    return null;
                }
            }

            return new SegmentTemplate
            {
                Kind = kind,
                SourceCurve = sourceCurve,
                TypeId = typeId,
                SystemTypeId = systemTypeId,
                LevelId = levelId
            };
        }

        private Element CreateSegment(SegmentTemplate template, ConnectorSnapshot sizeSource, XYZ start, XYZ end)
        {
            if (template == null)
            {
                throw new InvalidOperationException("No segment template is available.");
            }

            if (start == null || end == null || start.DistanceTo(end) <= MinSegmentLength)
            {
                throw new InvalidOperationException("Segment length is too short.");
            }

            switch (template.Kind)
            {
                case SegmentKind.Pipe:
                    {
                        Pipe pipe = Pipe.Create(_document, template.SystemTypeId, template.TypeId, template.LevelId, start, end);
                        ApplySegmentSize(pipe, template, sizeSource);
                        return pipe;
                    }

                case SegmentKind.Duct:
                    {
                        Duct duct = Duct.Create(_document, template.SystemTypeId, template.TypeId, template.LevelId, start, end);
                        ApplySegmentSize(duct, template, sizeSource);
                        return duct;
                    }

                case SegmentKind.CableTray:
                    {
                        CableTray cableTray = CableTray.Create(_document, template.TypeId, start, end, template.LevelId);
                        ApplySegmentSize(cableTray, template, sizeSource);
                        return cableTray;
                    }

                case SegmentKind.Conduit:
                    {
                        Conduit conduit = Conduit.Create(_document, template.TypeId, start, end, template.LevelId);
                        ApplySegmentSize(conduit, template, sizeSource);
                        return conduit;
                    }

                default:
                    throw new InvalidOperationException("Unsupported segment kind.");
            }
        }

        private void ApplySegmentSize(Element segment, SegmentTemplate template, ConnectorSnapshot sizeSource)
        {
            if (segment == null)
            {
                return;
            }

            BuiltInParameter widthParameter;
            BuiltInParameter heightParameter;
            BuiltInParameter diameterParameter;
            ResolveSizeParameters(template.Kind, out widthParameter, out heightParameter, out diameterParameter);

            if (sizeSource != null && sizeSource.Shape == ConnectorProfileType.Round && sizeSource.Radius > 0)
            {
                TrySetDoubleParameter(segment, diameterParameter, sizeSource.Radius * 2.0);
            }
            else if (sizeSource != null && sizeSource.Width > 0 && sizeSource.Height > 0)
            {
                TrySetDoubleParameter(segment, widthParameter, sizeSource.Width);
                TrySetDoubleParameter(segment, heightParameter, sizeSource.Height);
            }
            else if (template.SourceCurve != null)
            {
                CopyDoubleParameter(template.SourceCurve, segment, diameterParameter);
                CopyDoubleParameter(template.SourceCurve, segment, widthParameter);
                CopyDoubleParameter(template.SourceCurve, segment, heightParameter);
            }

            if (template.SourceCurve != null)
            {
                CopyDoubleParameter(template.SourceCurve, segment, BuiltInParameter.PROFILE_ANGLE);
            }

            if (widthParameter == BuiltInParameter.INVALID || heightParameter == BuiltInParameter.INVALID)
            {
                return;
            }

            double targetWidth = sizeSource != null && sizeSource.Width > 0 ? sizeSource.Width : 0;
            double targetHeight = sizeSource != null && sizeSource.Height > 0 ? sizeSource.Height : 0;
            if (targetWidth > 0 && targetHeight > 0)
            {
                ForceRectangularDimensions(segment, widthParameter, heightParameter, targetWidth, targetHeight);
            }
        }

        private static void ResolveSizeParameters(
            SegmentKind kind,
            out BuiltInParameter widthParameter,
            out BuiltInParameter heightParameter,
            out BuiltInParameter diameterParameter)
        {
            switch (kind)
            {
                case SegmentKind.Pipe:
                    widthParameter = BuiltInParameter.INVALID;
                    heightParameter = BuiltInParameter.INVALID;
                    diameterParameter = BuiltInParameter.RBS_PIPE_DIAMETER_PARAM;
                    return;

                case SegmentKind.Duct:
                    widthParameter = BuiltInParameter.RBS_CURVE_WIDTH_PARAM;
                    heightParameter = BuiltInParameter.RBS_CURVE_HEIGHT_PARAM;
                    diameterParameter = BuiltInParameter.RBS_CURVE_DIAMETER_PARAM;
                    return;

                case SegmentKind.CableTray:
                    widthParameter = BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM;
                    heightParameter = BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM;
                    diameterParameter = BuiltInParameter.INVALID;
                    return;

                default:
                    widthParameter = BuiltInParameter.INVALID;
                    heightParameter = BuiltInParameter.INVALID;
                    diameterParameter = BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM;
                    return;
            }
        }

        // ------------------------------------------------------------------
        // Geometry helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Travel along each ray to the point of closest approach between them. For rays that cross,
        /// both travels land on the crossing point.
        /// </summary>
        private static bool TryGetClosestApproach(
            XYZ firstPoint,
            XYZ firstDirection,
            XYZ secondPoint,
            XYZ secondDirection,
            out double firstTravel,
            out double secondTravel)
        {
            firstTravel = 0;
            secondTravel = 0;

            XYZ between = secondPoint.Subtract(firstPoint);
            double dotDirections = firstDirection.DotProduct(secondDirection);
            double denominator = 1.0 - (dotDirections * dotDirections);

            if (Math.Abs(denominator) < 1e-9)
            {
                return false;
            }

            double firstProjection = between.DotProduct(firstDirection);
            double secondProjection = between.DotProduct(secondDirection);

            firstTravel = (firstProjection - (dotDirections * secondProjection)) / denominator;
            secondTravel = ((dotDirections * firstProjection) - secondProjection) / denominator;

            return !double.IsNaN(firstTravel) && !double.IsInfinity(firstTravel) &&
                   !double.IsNaN(secondTravel) && !double.IsInfinity(secondTravel);
        }

        private static bool TryGetOutwardDirection(Connector connector, out XYZ direction)
        {
            direction = null;
            if (connector == null || !connector.IsValidObject)
            {
                return false;
            }

            XYZ curveDirection;
            if (TryGetMepCurveEndDirection(connector, out curveDirection))
            {
                direction = curveDirection;
                return true;
            }

            XYZ connectorAxis;
            if (SmartConnectConnectorUtils.TryGetConnectorAxis(connector, out connectorAxis))
            {
                direction = connectorAxis.Normalize();
                return true;
            }

            return false;
        }

        private static bool TryGetMepCurveEndDirection(Connector connector, out XYZ direction)
        {
            direction = null;

            MEPCurve ownerCurve = connector == null ? null : connector.Owner as MEPCurve;
            if (ownerCurve == null)
            {
                return false;
            }

            LocationCurve location = ownerCurve.Location as LocationCurve;
            Line line = location == null ? null : location.Curve as Line;
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

        /// <summary>
        /// True when this element's end can be trimmed: only straight, line-based runs qualify.
        /// Flex runs and equipment keep their geometry and get a new piece inserted instead.
        /// </summary>
        private static bool CanTrimEnd(Element element)
        {
            MEPCurve curve = element as MEPCurve;
            if (curve == null)
            {
                return false;
            }

            if (curve is FlexDuct || curve is FlexPipe)
            {
                return false;
            }

            LocationCurve location = curve.Location as LocationCurve;
            return location != null && location.Curve is Line;
        }

        private bool TryMoveCurveEnd(MEPCurve curve, XYZ oldEndPoint, XYZ newEndPoint, out string errorMessage)
        {
            errorMessage = string.Empty;

            string elementName = curve == null || curve.Category == null ? "Element" : curve.Category.Name;
            LocationCurve location = curve == null ? null : curve.Location as LocationCurve;
            Line line = location == null ? null : location.Curve as Line;
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
                errorMessage = elementName + " would become too short after the end was adjusted.";
                return false;
            }

            try
            {
                location.Curve = Line.CreateBound(newStart, newEnd);
            }
            catch (Exception ex)
            {
                errorMessage = "Could not adjust " + elementName + ": " + ex.Message;
                return false;
            }

            return true;
        }

        private static XYZ MidPoint(XYZ first, XYZ second)
        {
            return new XYZ(
                (first.X + second.X) * 0.5,
                (first.Y + second.Y) * 0.5,
                (first.Z + second.Z) * 0.5);
        }

        private static bool AreEndSizesEqual(ConnectorSnapshot first, ConnectorSnapshot second)
        {
            if (first == null || second == null)
            {
                return true;
            }

            if (first.Shape != second.Shape)
            {
                return false;
            }

            if (first.Shape == ConnectorProfileType.Round)
            {
                return Math.Abs(first.Radius - second.Radius) <= SizeComparisonTolerance;
            }

            return Math.Abs(first.Width - second.Width) <= SizeComparisonTolerance &&
                   Math.Abs(first.Height - second.Height) <= SizeComparisonTolerance;
        }

        private Connector FindConnectorAt(Element element, XYZ point, bool requireOpen)
        {
            if (element == null || !element.IsValidObject)
            {
                return null;
            }

            Connector connector = SmartConnectConnectorUtils.FindClosestConnector(element, point, requireOpen);
            if (connector != null)
            {
                return connector;
            }

            return requireOpen ? SmartConnectConnectorUtils.FindClosestConnector(element, point, false) : null;
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

        // ------------------------------------------------------------------
        // Fitting creation
        // ------------------------------------------------------------------

        private bool TryConnectPair(
            Connector firstConnector,
            Connector secondConnector,
            bool preferElbow,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (firstConnector == null || secondConnector == null)
            {
                errorMessage = "Connector pairing failed while joining the route.";
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

            errorMessage = "Could not place a fitting to join the route - check the routing preferences of the pipe or duct type.";
            return false;
        }

        private bool TryCreateElbow(Connector firstConnector, Connector secondConnector)
        {
            try
            {
                return _document.Create.NewElbowFitting(firstConnector, secondConnector) != null;
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
                return _document.Create.NewUnionFitting(firstConnector, secondConnector) != null;
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
                return _document.Create.NewTransitionFitting(firstConnector, secondConnector) != null;
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

        // ------------------------------------------------------------------
        // Rectangular sizing (kept from v1: proven against real duct and tray families)
        // ------------------------------------------------------------------

        private static void ApplyRectangularConnectorSize(Connector connector, ConnectorSnapshot snapshot)
        {
            if (connector == null ||
                !connector.IsValidObject ||
                snapshot == null ||
                connector.Shape != ConnectorProfileType.Rectangular ||
                snapshot.Width <= 0 ||
                snapshot.Height <= 0)
            {
                return;
            }

            try
            {
                connector.Width = snapshot.Width;
                connector.Height = snapshot.Height;
            }
            catch
            {
                // Connector sizing is a refinement, never a reason to fail the connection.
            }
        }

        /// <summary>
        /// Rectangular ducts and trays sometimes refuse a width/height pair until the profile is
        /// rotated a quarter turn, so try the plain set first and rotate only if it does not stick.
        /// </summary>
        private void ForceRectangularDimensions(
            Element target,
            BuiltInParameter widthParameterId,
            BuiltInParameter heightParameterId,
            double expectedWidth,
            double expectedHeight)
        {
            if (TryApplyRectangularDimensions(target, widthParameterId, heightParameterId, expectedWidth, expectedHeight))
            {
                return;
            }

            if (AreAlmostEqual(expectedWidth, expectedHeight))
            {
                return;
            }

            double profileAngle;
            bool hasProfileAngle = TryGetDoubleParameter(target, BuiltInParameter.PROFILE_ANGLE, out profileAngle);
            if (hasProfileAngle)
            {
                if (TrySetDoubleParameter(target, BuiltInParameter.PROFILE_ANGLE, profileAngle + QuarterTurnRadians) &&
                    TryApplyRectangularDimensions(target, widthParameterId, heightParameterId, expectedWidth, expectedHeight))
                {
                    return;
                }

                if (TrySetDoubleParameter(target, BuiltInParameter.PROFILE_ANGLE, profileAngle - QuarterTurnRadians) &&
                    TryApplyRectangularDimensions(target, widthParameterId, heightParameterId, expectedWidth, expectedHeight))
                {
                    return;
                }
            }

            if (TryRotateRectangularProfileAndMatch(target, widthParameterId, heightParameterId, expectedWidth, expectedHeight, QuarterTurnRadians))
            {
                return;
            }

            TryRotateRectangularProfileAndMatch(target, widthParameterId, heightParameterId, expectedWidth, expectedHeight, -QuarterTurnRadians);
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

            double actualWidth;
            double actualHeight;
            if (!TryGetDoubleParameter(target, widthParameterId, out actualWidth) ||
                !TryGetDoubleParameter(target, heightParameterId, out actualHeight))
            {
                return false;
            }

            return AreAlmostEqual(actualWidth, expectedWidth) && AreAlmostEqual(actualHeight, expectedHeight);
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
            LocationCurve location = curve == null ? null : curve.Location as LocationCurve;
            Line centerLine = location == null ? null : location.Curve as Line;
            if (centerLine == null)
            {
                return false;
            }

            try
            {
                Line rotationAxis = Line.CreateBound(centerLine.GetEndPoint(0), centerLine.GetEndPoint(1));
                ElementTransformUtils.RotateElement(_document, target.Id, rotationAxis, rotationRadians);
                _document.Regenerate();
            }
            catch
            {
                return false;
            }

            return TryApplyRectangularDimensions(target, widthParameterId, heightParameterId, expectedWidth, expectedHeight);
        }

        // ------------------------------------------------------------------
        // Type, system and parameter helpers
        // ------------------------------------------------------------------

        private ElementId ResolvePipeSystemTypeId(Pipe source)
        {
            if (source != null && source.MEPSystem != null)
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
            if (source != null && source.MEPSystem != null)
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

            return first == null ? ElementId.InvalidElementId : first.Id;
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
            Parameter parameter = element == null ? null : element.get_Parameter(parameterId);
            if (parameter == null || parameter.StorageType != StorageType.ElementId)
            {
                return ElementId.InvalidElementId;
            }

            ElementId value = parameter.AsElementId();
            return value ?? ElementId.InvalidElementId;
        }

        private static void CopyDoubleParameter(Element source, Element target, BuiltInParameter parameterId)
        {
            if (parameterId == BuiltInParameter.INVALID)
            {
                return;
            }

            Parameter sourceParameter = source == null ? null : source.get_Parameter(parameterId);
            Parameter targetParameter = target == null ? null : target.get_Parameter(parameterId);

            if (sourceParameter == null ||
                targetParameter == null ||
                sourceParameter.StorageType != StorageType.Double ||
                targetParameter.StorageType != StorageType.Double ||
                targetParameter.IsReadOnly)
            {
                return;
            }

            try
            {
                targetParameter.Set(sourceParameter.AsDouble());
            }
            catch
            {
                // A size that will not take is handled by the rectangular fallback above.
            }
        }

        private static void CopyStringParameter(Element source, Element target, BuiltInParameter parameterId)
        {
            Parameter sourceParameter = source == null ? null : source.get_Parameter(parameterId);
            Parameter targetParameter = target == null ? null : target.get_Parameter(parameterId);

            if (sourceParameter == null ||
                targetParameter == null ||
                sourceParameter.StorageType != StorageType.String ||
                targetParameter.StorageType != StorageType.String ||
                targetParameter.IsReadOnly)
            {
                return;
            }

            string value = sourceParameter.AsString();
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            try
            {
                targetParameter.Set(value);
            }
            catch
            {
                // Never block a connection over a comment.
            }
        }

        private static void CopyElementIdParameter(Element source, Element target, BuiltInParameter parameterId)
        {
            Parameter sourceParameter = source == null ? null : source.get_Parameter(parameterId);
            Parameter targetParameter = target == null ? null : target.get_Parameter(parameterId);

            if (sourceParameter == null ||
                targetParameter == null ||
                sourceParameter.StorageType != StorageType.ElementId ||
                targetParameter.StorageType != StorageType.ElementId ||
                targetParameter.IsReadOnly)
            {
                return;
            }

            try
            {
                targetParameter.Set(sourceParameter.AsElementId());
            }
            catch
            {
                // Worksets are not always assignable; the segment keeps the active workset.
            }
        }

        private static bool TryGetDoubleParameter(Element element, BuiltInParameter parameterId, out double value)
        {
            value = 0;
            if (parameterId == BuiltInParameter.INVALID)
            {
                return false;
            }

            Parameter parameter = element == null ? null : element.get_Parameter(parameterId);
            if (parameter == null || parameter.StorageType != StorageType.Double || !parameter.HasValue)
            {
                return false;
            }

            value = parameter.AsDouble();
            return true;
        }

        private static bool TrySetDoubleParameter(Element element, BuiltInParameter parameterId, double value)
        {
            if (parameterId == BuiltInParameter.INVALID)
            {
                return false;
            }

            Parameter parameter = element == null ? null : element.get_Parameter(parameterId);
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
            return value == null || value == ElementId.InvalidElementId || value.IntValue() <= 0;
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
