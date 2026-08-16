// Tool Name: Smart Connect - Connector Utilities
// Description: Shared connector lookup logic for Smart Connect routing.
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2026-08-16
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB
//
// v1.1.0 - Removed TryGetBestOpenConnectorPair, AreDomainsCompatible and ComputeOrientationPenalty.
// They had no callers anywhere in the codebase - leftover from before SmartConnectRouteBuilder's
// rewrite, which does its own inline domain check and never called into this scoring method.

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

    }
}
