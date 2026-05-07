// ==================================================
// Tool Name    : HVAC Schematic
// Purpose      : Resolves the most reliable level context for HVAC elements.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-07
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Revit HVAC elements and project level data.
// Output       : Resolved level labels and elevations for schematic nodes.
// Notes        : Falls back through direct parameters, hosts, owner views, connectors, and nearest elevation.
// Changelog    : v1.0.0 - Initial production-ready HVAC schematic level resolver with standardized metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace AJTools.Services.HvacSchematic
{
    internal sealed class LevelResolverService
    {
        internal sealed class LevelResolution
        {
            public LevelResolution(Level level, bool isResolved)
            {
                Level = level;
                IsResolved = isResolved;
                Label = level?.Name ?? "Unresolved Level";
                Elevation = level != null ? (double?)level.Elevation : null;
            }

            public Level Level { get; }
            public bool IsResolved { get; }
            public string Label { get; }
            public double? Elevation { get; }
        }

        private readonly Document _document;
        private readonly IReadOnlyList<Level> _levels;

        public LevelResolverService(Document document)
        {
            _document = document;
            _levels = new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ToList();
        }

        public IReadOnlyList<Level> Levels
        {
            get { return _levels; }
        }

        public LevelResolution Resolve(Element element)
        {
            Level level;
            var visited = new HashSet<int>();

            if (TryResolveDirectLevelParameter(element, out level))
            {
                return new LevelResolution(level, true);
            }

            if (TryResolveReferenceLevel(element, out level))
            {
                return new LevelResolution(level, true);
            }

            if (TryResolveFamilyInstanceLevel(element, out level))
            {
                return new LevelResolution(level, true);
            }

            if (TryResolveHostLevel(element, visited, out level))
            {
                return new LevelResolution(level, true);
            }

            if (TryResolveOwnerViewLevel(element, out level))
            {
                return new LevelResolution(level, true);
            }

            if (TryResolveConnectorContextLevel(element, visited, out level))
            {
                return new LevelResolution(level, true);
            }

            if (TryResolveNearestLevelByElevation(element, out level))
            {
                return new LevelResolution(level, true);
            }

            return new LevelResolution(null, false);
        }

        private bool TryResolveDirectLevelParameter(Element element, out Level level)
        {
            level = null;

            ElementId levelId;
            if (TryGetLevelIdFromBuiltInParameter(element, BuiltInParameter.LEVEL_PARAM, out levelId) &&
                TryGetLevel(levelId, out level))
            {
                return true;
            }

            if (TryGetLevelIdFromNamedParameter(element, "Level", out levelId) &&
                TryGetLevel(levelId, out level))
            {
                return true;
            }

            try
            {
                if (element != null &&
                    element.LevelId != null &&
                    element.LevelId != ElementId.InvalidElementId &&
                    TryGetLevel(element.LevelId, out level))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool TryResolveReferenceLevel(Element element, out Level level)
        {
            level = null;

            ElementId levelId;
            if (TryGetLevelIdFromNamedParameter(element, "Reference Level", out levelId) &&
                TryGetLevel(levelId, out level))
            {
                return true;
            }

            if (TryGetLevelIdFromBuiltInParameter(element, BuiltInParameter.RBS_START_LEVEL_PARAM, out levelId) &&
                TryGetLevel(levelId, out level))
            {
                return true;
            }

            if (TryGetLevelIdFromBuiltInParameter(element, BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM, out levelId) &&
                TryGetLevel(levelId, out level))
            {
                return true;
            }

            return false;
        }

        private bool TryResolveHostLevel(Element element, ISet<int> visited, out Level level)
        {
            level = null;

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance?.Host == null || visited == null)
            {
                return false;
            }

            return TryResolveHostElementLevel(familyInstance.Host, visited, out level);
        }

        private bool TryResolveHostElementLevel(Element host, ISet<int> visited, out Level level)
        {
            level = null;

            if (host == null || visited == null)
            {
                return false;
            }

            if (!visited.Add(host.Id.IntegerValue))
            {
                return false;
            }

            if (TryResolveDirectLevelParameter(host, out level) ||
                TryResolveReferenceLevel(host, out level) ||
                TryResolveFamilyInstanceLevel(host, out level) ||
                TryResolveOwnerViewLevel(host, out level))
            {
                return true;
            }

            FamilyInstance hostFamily = host as FamilyInstance;
            if (hostFamily?.Host != null)
            {
                return TryResolveHostElementLevel(hostFamily.Host, visited, out level);
            }

            return false;
        }

        private bool TryResolveFamilyInstanceLevel(Element element, out Level level)
        {
            level = null;

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance == null)
            {
                return false;
            }

            ElementId levelId;
            if (TryGetLevelIdFromBuiltInParameter(familyInstance, BuiltInParameter.FAMILY_LEVEL_PARAM, out levelId) &&
                TryGetLevel(levelId, out level))
            {
                return true;
            }

            if (TryGetLevelIdFromBuiltInParameter(familyInstance, BuiltInParameter.SCHEDULE_LEVEL_PARAM, out levelId) &&
                TryGetLevel(levelId, out level))
            {
                return true;
            }

            return false;
        }

        private bool TryResolveOwnerViewLevel(Element element, out Level level)
        {
            level = null;

            if (element == null)
            {
                return false;
            }

            try
            {
                if (element.OwnerViewId == null || element.OwnerViewId == ElementId.InvalidElementId)
                {
                    return false;
                }

                View ownerView = _document.GetElement(element.OwnerViewId) as View;
                if (ownerView?.GenLevel == null)
                {
                    return false;
                }

                level = ownerView.GenLevel;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryResolveConnectorContextLevel(Element element, ISet<int> visited, out Level level)
        {
            level = null;

            ConnectorManager connectorManager = TryGetConnectorManager(element);
            if (connectorManager == null || visited == null)
            {
                return false;
            }

            foreach (Connector connector in connectorManager.Connectors)
            {
                if (connector == null || !connector.IsValidObject)
                {
                    continue;
                }

                foreach (Connector referenceConnector in connector.AllRefs)
                {
                    if (referenceConnector == null || !referenceConnector.IsValidObject)
                    {
                        continue;
                    }

                    Element owner = referenceConnector.Owner;
                    if (owner == null || owner.Id.IntegerValue == element.Id.IntegerValue)
                    {
                        continue;
                    }

                    if (!visited.Add(owner.Id.IntegerValue))
                    {
                        continue;
                    }

                    if (TryResolveDirectLevelParameter(owner, out level) ||
                        TryResolveReferenceLevel(owner, out level) ||
                        TryResolveHostLevel(owner, visited, out level) ||
                        TryResolveFamilyInstanceLevel(owner, out level) ||
                        TryResolveOwnerViewLevel(owner, out level))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryResolveNearestLevelByElevation(Element element, out Level level)
        {
            level = null;

            if (_levels.Count == 0)
            {
                return false;
            }

            double elevation;
            if (!TryGetRepresentativeElevation(element, out elevation))
            {
                return false;
            }

            Level nearest = null;
            double smallestDifference = double.MaxValue;
            foreach (Level candidate in _levels)
            {
                double difference = Math.Abs(candidate.Elevation - elevation);
                if (difference < smallestDifference)
                {
                    smallestDifference = difference;
                    nearest = candidate;
                }
            }

            level = nearest;
            return level != null;
        }

        private static bool TryGetLevelIdFromNamedParameter(Element element, string parameterName, out ElementId levelId)
        {
            levelId = ElementId.InvalidElementId;

            if (element == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            try
            {
                Parameter parameter = element.LookupParameter(parameterName);
                if (parameter == null ||
                    parameter.StorageType != StorageType.ElementId ||
                    !parameter.HasValue)
                {
                    return false;
                }

                levelId = parameter.AsElementId();
                return levelId != null && levelId != ElementId.InvalidElementId;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetLevelIdFromBuiltInParameter(Element element, BuiltInParameter builtInParameter, out ElementId levelId)
        {
            levelId = ElementId.InvalidElementId;

            if (element == null)
            {
                return false;
            }

            try
            {
                Parameter parameter = element.get_Parameter(builtInParameter);
                if (parameter == null ||
                    parameter.StorageType != StorageType.ElementId ||
                    !parameter.HasValue)
                {
                    return false;
                }

                levelId = parameter.AsElementId();
                return levelId != null && levelId != ElementId.InvalidElementId;
            }
            catch
            {
                return false;
            }
        }

        private bool TryGetLevel(ElementId levelId, out Level level)
        {
            level = null;
            if (levelId == null || levelId == ElementId.InvalidElementId)
            {
                return false;
            }

            level = _document.GetElement(levelId) as Level;
            return level != null;
        }

        private static bool TryGetRepresentativeElevation(Element element, out double elevation)
        {
            elevation = 0;

            if (element == null)
            {
                return false;
            }

            LocationPoint locationPoint = element.Location as LocationPoint;
            if (locationPoint?.Point != null)
            {
                elevation = locationPoint.Point.Z;
                return true;
            }

            LocationCurve locationCurve = element.Location as LocationCurve;
            if (locationCurve?.Curve != null)
            {
                XYZ midpoint = locationCurve.Curve.Evaluate(0.5, true);
                if (midpoint != null)
                {
                    elevation = midpoint.Z;
                    return true;
                }
            }

            ConnectorManager connectorManager = TryGetConnectorManager(element);
            if (connectorManager != null)
            {
                double total = 0;
                int count = 0;
                foreach (Connector connector in connectorManager.Connectors)
                {
                    if (connector?.Origin == null)
                    {
                        continue;
                    }

                    total += connector.Origin.Z;
                    count++;
                }

                if (count > 0)
                {
                    elevation = total / count;
                    return true;
                }
            }

            BoundingBoxXYZ boundingBox = element.get_BoundingBox(null);
            if (boundingBox?.Min != null && boundingBox.Max != null)
            {
                elevation = (boundingBox.Min.Z + boundingBox.Max.Z) * 0.5;
                return true;
            }

            return false;
        }

        private static ConnectorManager TryGetConnectorManager(Element element)
        {
            MEPCurve mepCurve = element as MEPCurve;
            if (mepCurve != null)
            {
                return mepCurve.ConnectorManager;
            }

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance?.MEPModel != null)
            {
                return familyInstance.MEPModel.ConnectorManager;
            }

            return null;
        }
    }
}
