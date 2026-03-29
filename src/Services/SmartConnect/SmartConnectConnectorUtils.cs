// Tool Name: Smart Connect - Connector Utilities
// Description: Shared connector lookup and pairing logic for Smart Connect routing.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-25
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Services.SmartConnect
{
    /// <summary>
    /// Connector utilities scoped to Smart Connect workflows.
    /// </summary>
    internal static class SmartConnectConnectorUtils
    {
        private const double DirectionTolerance = 1e-9;

        public static IList<Connector> GetOpenConnectors(Element element)
        {
            var result = new List<Connector>();
            ConnectorManager manager = GetConnectorManager(element);
            if (manager == null)
            {
                return result;
            }

            foreach (Connector connector in manager.Connectors)
            {
                if (!IsConnectorUsable(connector))
                {
                    continue;
                }

                if (!connector.IsConnected)
                {
                    result.Add(connector);
                }
            }

            return result;
        }

        public static Connector FindClosestConnector(Element element, XYZ point, bool requireOpen)
        {
            ConnectorManager manager = GetConnectorManager(element);
            if (manager == null || point == null)
            {
                return null;
            }

            Connector best = null;
            double bestDistance = double.MaxValue;

            foreach (Connector connector in manager.Connectors)
            {
                if (!IsConnectorUsable(connector))
                {
                    continue;
                }

                if (requireOpen && connector.IsConnected)
                {
                    continue;
                }

                double distance = connector.Origin.DistanceTo(point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = connector;
                }
            }

            return best;
        }

        public static bool TryGetBestOpenConnectorPair(
            Element firstElement,
            Element secondElement,
            out Connector firstConnector,
            out Connector secondConnector,
            out string errorMessage)
        {
            firstConnector = null;
            secondConnector = null;
            errorMessage = string.Empty;

            IList<Connector> firstOpen = GetOpenConnectors(firstElement);
            if (firstOpen.Count == 0)
            {
                errorMessage = "No open connector found on the first selected element.";
                return false;
            }

            IList<Connector> secondOpen = GetOpenConnectors(secondElement);
            if (secondOpen.Count == 0)
            {
                errorMessage = "No open connector found on the second selected element.";
                return false;
            }

            double bestScore = double.MaxValue;
            foreach (Connector first in firstOpen)
            {
                foreach (Connector second in secondOpen)
                {
                    if (!AreDomainsCompatible(first, second))
                    {
                        continue;
                    }

                    double distanceScore = first.Origin.DistanceTo(second.Origin);
                    double orientationPenalty = ComputeOrientationPenalty(first, second);
                    double totalScore = distanceScore + orientationPenalty;

                    if (totalScore < bestScore)
                    {
                        bestScore = totalScore;
                        firstConnector = first;
                        secondConnector = second;
                    }
                }
            }

            if (firstConnector == null || secondConnector == null)
            {
                errorMessage = "Could not find a compatible pair of open connectors.";
                return false;
            }

            return true;
        }

        public static bool TryGetConnectorAxis(Connector connector, out XYZ axis)
        {
            axis = null;
            if (connector == null || !connector.IsValidObject)
            {
                return false;
            }

            Transform coordinateSystem = connector.CoordinateSystem;
            XYZ basis = coordinateSystem?.BasisZ;
            if (basis == null || basis.GetLength() <= DirectionTolerance)
            {
                return false;
            }

            axis = basis.Normalize();
            return true;
        }

        public static bool AreConnected(Connector first, Connector second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            try
            {
                return first.IsConnectedTo(second);
            }
            catch
            {
                return false;
            }
        }

        private static ConnectorManager GetConnectorManager(Element element)
        {
            if (element is MEPCurve mepCurve)
            {
                return mepCurve.ConnectorManager;
            }

            if (element is FamilyInstance familyInstance && familyInstance.MEPModel != null)
            {
                return familyInstance.MEPModel.ConnectorManager;
            }

            return null;
        }

        private static bool IsConnectorUsable(Connector connector)
        {
            if (connector == null || !connector.IsValidObject)
            {
                return false;
            }

            return connector.ConnectorType == ConnectorType.End;
        }

        private static bool AreDomainsCompatible(Connector first, Connector second)
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

        private static double ComputeOrientationPenalty(Connector first, Connector second)
        {
            if (!TryGetConnectorAxis(first, out XYZ firstAxis) || !TryGetConnectorAxis(second, out XYZ secondAxis))
            {
                return 0;
            }

            XYZ between = second.Origin.Subtract(first.Origin);
            if (between.GetLength() <= DirectionTolerance)
            {
                return 0;
            }

            XYZ routeDir = between.Normalize();
            double firstFacing = 1.0 - Math.Max(-1.0, Math.Min(1.0, firstAxis.DotProduct(routeDir)));
            double secondFacing = 1.0 - Math.Max(-1.0, Math.Min(1.0, secondAxis.Multiply(-1.0).DotProduct(routeDir)));

            return firstFacing + secondFacing;
        }
    }
}
