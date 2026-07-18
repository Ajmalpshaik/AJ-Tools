#region Metadata
/*
 * Tool Name     : Reassign Reference Level
 * File Name     : ReassignLevelService.cs
 * Purpose       : Implements the level-reassignment engine for MEP curves, free-standing family
 *                 instances, and spaces - candidate collection, eligibility checks, per-category
 *                 host-offset compensation, and per-parameter space copying used to re-point
 *                 elements from one level to another without moving them physically.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-17
 * Last Updated  : 2026-07-17
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, Autodesk.Revit.DB.Mechanical, AJTools.Utils
 *
 * Input         : Document, candidate Elements, source/target Level, and an OffsetHelper built from
 *                 the active Revit version - all supplied by CmdReassignLevel.cs, which owns UI
 *                 prompting, the confirmation dialog, and the transaction.
 * Output        : Candidate element list for a given FROM level (plus a hosted-instance skip count),
 *                 and per-element reassignment - elements re-pointed to the target level with host
 *                 offset compensated so they stay put; boolean success/failure per element for the
 *                 caller to tally.
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Hosted family instances are intentionally left untouched here (caller skips and reports them).
 * - Pure algorithm - no direct UI/dialog/transaction interaction; the caller owns the transaction.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-17) - Initial extraction from CmdReassignLevel.cs (code review cleanup pass) -
 *                       no behavior change.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Services.ReassignLevel
{
    /// <summary>
    /// Level-reassignment engine: candidate collection, eligibility checks, host-offset
    /// compensation, and per-element reassignment for MEP curves, free-standing family
    /// instances, and spaces. Contains no direct UI/dialog/transaction interaction.
    /// </summary>
    internal static class ReassignLevelService
    {
        private const double MetersPerFoot = 0.3048;
        private const double OneMeterInFeet = 1.0 / MetersPerFoot;

        /// <summary>
        /// Returns true when the active Revit version is 2020 or above.
        /// </summary>
        internal static bool IsRevit2020OrAbove(UIApplication uiApp)
        {
            int year;
            return int.TryParse(uiApp?.Application?.VersionNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out year) &&
                   year >= 2020;
        }

        /// <summary>
        /// Collects every supported element (MEP curve, free-standing family instance, space)
        /// currently on the FROM level. Hosted family instances are excluded from the returned
        /// list and counted separately via <paramref name="skippedHostedCount"/>.
        /// </summary>
        internal static List<Element> CollectCandidates(Document doc, ElementId fromId, out int skippedHostedCount)
        {
            var filter = new LogicalOrFilter(new List<ElementFilter>
            {
                new ElementClassFilter(typeof(MEPCurve)),
                new ElementClassFilter(typeof(FamilyInstance)),
                new ElementClassFilter(typeof(SpatialElement))
            });

            var allElements = new FilteredElementCollector(doc)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .ToElements();

            var candidates = new List<Element>();
            int skippedHosted = 0;

            foreach (Element element in allElements)
            {
                if (element is Level)
                {
                    continue;
                }

                FamilyInstance hostedFamily = element as FamilyInstance;
                if (hostedFamily != null && hostedFamily.Host != null)
                {
                    if (IsFamilyInstanceOnLevel(hostedFamily, fromId))
                    {
                        skippedHosted++;
                    }

                    continue;
                }

                if (ElementOnFromLevel(element, fromId))
                {
                    candidates.Add(element);
                }
            }

            skippedHostedCount = skippedHosted;
            return candidates;
        }

        private static bool ElementOnFromLevel(Element element, ElementId fromId)
        {
            if (element == null || fromId == null || fromId == ElementId.InvalidElementId)
            {
                return false;
            }

            if (element is MEPCurve mepCurve)
            {
                return IsMepCurveOnLevel(mepCurve, fromId);
            }

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance != null && familyInstance.Host == null)
            {
                return IsFamilyInstanceOnLevel(familyInstance, fromId);
            }

            Space space = element as Space;
            if (space != null)
            {
                return space.LevelId == fromId;
            }

            return false;
        }

        private static bool IsMepCurveOnLevel(MEPCurve curve, ElementId levelId)
        {
            if (curve == null || levelId == null || levelId == ElementId.InvalidElementId)
            {
                return false;
            }

            try
            {
                if (curve.ReferenceLevel != null && curve.ReferenceLevel.Id == levelId)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore and continue with fallback checks.
            }

            if (curve.LevelId == levelId)
            {
                return true;
            }

            Parameter startLevel = curve.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            return startLevel != null &&
                   startLevel.StorageType == StorageType.ElementId &&
                   startLevel.AsElementId() == levelId;
        }

        private static bool IsFamilyInstanceOnLevel(FamilyInstance familyInstance, ElementId levelId)
        {
            if (familyInstance == null || levelId == null || levelId == ElementId.InvalidElementId)
            {
                return false;
            }

            Parameter levelParam = GetFamilyLevelParameter(familyInstance);
            if (levelParam != null &&
                levelParam.StorageType == StorageType.ElementId &&
                levelParam.AsElementId() == levelId)
            {
                return true;
            }

            return familyInstance.LevelId == levelId;
        }

        private static Parameter GetFamilyLevelParameter(Element element)
        {
            Parameter levelParam = element?.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
            if (levelParam != null && !levelParam.IsReadOnly)
            {
                return levelParam;
            }

            Parameter referenceLevelParam = element?.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            if (referenceLevelParam != null && !referenceLevelParam.IsReadOnly)
            {
                return referenceLevelParam;
            }

            return null;
        }

        /// <summary>
        /// Reassigns a single candidate element from the FROM level to the TO level without moving
        /// it physically. Returns true on success, false when the element could not be reassigned.
        /// Caller (CmdReassignLevel) runs this inside its own transaction and tallies the result.
        /// </summary>
        internal static bool ReassignElement(
            Document doc,
            Element element,
            Level fromLevel,
            Level toLevel,
            ElementId fromId,
            ElementId toId,
            OffsetHelper offsetHelper)
        {
            MEPCurve mepCurve = element as MEPCurve;
            if (mepCurve != null)
            {
                if (!IsMepCurveOnLevel(mepCurve, fromId))
                {
                    return false;
                }

                try
                {
                    mepCurve.ReferenceLevel = toLevel;
                    return true;
                }
                catch
                {
                    Parameter startLevel = mepCurve.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
                    if (startLevel == null ||
                        startLevel.IsReadOnly ||
                        startLevel.StorageType != StorageType.ElementId ||
                        startLevel.AsElementId() != fromId)
                    {
                        return false;
                    }

                    return startLevel.Set(toId);
                }
            }

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance != null && familyInstance.Host == null)
            {
                Parameter levelParam = GetFamilyLevelParameter(familyInstance);
                if (levelParam == null || levelParam.StorageType != StorageType.ElementId)
                {
                    return false;
                }

                ElementId currentLevelId = levelParam.AsElementId();
                if (currentLevelId != fromId)
                {
                    return false;
                }

                Level oldLevel = doc.GetElement(currentLevelId) as Level;
                if (oldLevel == null)
                {
                    return false;
                }

                if (offsetHelper.IsRequired(familyInstance))
                {
                    Parameter offsetParam = familyInstance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                    if (offsetParam != null &&
                        !offsetParam.IsReadOnly &&
                        offsetParam.StorageType == StorageType.Double)
                    {
                        double newOffset = offsetParam.AsDouble() + oldLevel.Elevation - toLevel.Elevation;
                        offsetParam.Set(newOffset);
                    }
                }

                return levelParam.Set(toId);
            }

            Space sourceSpace = element as Space;
            if (sourceSpace != null)
            {
                if (sourceSpace.LevelId != fromId)
                {
                    return false;
                }

                LocationPoint locationPoint = sourceSpace.Location as LocationPoint;
                if (locationPoint?.Point == null)
                {
                    return false;
                }

                XYZ point = locationPoint.Point;
                Space targetSpace = doc.Create.NewSpace(toLevel, new UV(point.X, point.Y));
                if (targetSpace == null)
                {
                    return false;
                }

                CopySpaceParameters(sourceSpace, targetSpace, toId);
                doc.Delete(sourceSpace.Id);
                return true;
            }

            return false;
        }

        private static void CopySpaceParameters(Space sourceSpace, Space targetSpace, ElementId toId)
        {
            foreach (Parameter sourceParameter in sourceSpace.Parameters)
            {
                if (sourceParameter == null || sourceParameter.IsReadOnly || sourceParameter.Definition == null)
                {
                    continue;
                }

                object value = GetParameterValue(sourceParameter);
                if (value == null)
                {
                    continue;
                }

                if (sourceParameter.StorageType == StorageType.String && string.IsNullOrEmpty(value as string))
                {
                    continue;
                }

                if (sourceParameter.StorageType == StorageType.Integer && value is int integerValue && integerValue == 0)
                {
                    continue;
                }

                try
                {
                    Parameter targetParameter = targetSpace.LookupParameter(sourceParameter.Definition.Name);
                    if (targetParameter == null || targetParameter.IsReadOnly)
                    {
                        continue;
                    }

                    TrySetParameterValue(targetParameter, value);
                }
                catch
                {
                    // Ignore per-parameter copy failures.
                }
            }

            try
            {
                Parameter upperLevel = targetSpace.get_Parameter(BuiltInParameter.ROOM_UPPER_LEVEL);
                if (upperLevel != null &&
                    !upperLevel.IsReadOnly &&
                    upperLevel.StorageType == StorageType.ElementId)
                {
                    upperLevel.Set(toId);
                }
            }
            catch
            {
                // Ignore if parameter is unavailable.
            }

            try
            {
                Parameter upperOffset = targetSpace.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET);
                if (upperOffset != null &&
                    !upperOffset.IsReadOnly &&
                    upperOffset.StorageType == StorageType.Double &&
                    upperOffset.AsDouble() <= 0)
                {
                    upperOffset.Set(OneMeterInFeet);
                }
            }
            catch
            {
                // Ignore if parameter is unavailable.
            }
        }

        private static object GetParameterValue(Parameter parameter)
        {
            switch (parameter.StorageType)
            {
                case StorageType.Double:
                    return parameter.AsDouble();
                case StorageType.ElementId:
                    return parameter.AsElementId();
                case StorageType.Integer:
                    return parameter.AsInteger();
                case StorageType.String:
                    return parameter.AsString();
                default:
                    return null;
            }
        }

        private static bool TrySetParameterValue(Parameter parameter, object value)
        {
            if (parameter == null || parameter.IsReadOnly)
            {
                return false;
            }

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.Double:
                        if (value is double doubleValue)
                        {
                            return parameter.Set(doubleValue);
                        }
                        break;
                    case StorageType.ElementId:
                        if (value is ElementId elementIdValue)
                        {
                            return parameter.Set(elementIdValue);
                        }
                        break;
                    case StorageType.Integer:
                        if (value is int intValue)
                        {
                            return parameter.Set(intValue);
                        }
                        break;
                    case StorageType.String:
                        return parameter.Set(value as string);
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Determines, per element category and Revit version, whether a free-standing family
        /// instance's host offset needs to be compensated when its level parameter is repointed.
        /// </summary>
        internal sealed class OffsetHelper
        {
            private readonly HashSet<int> _categoryIdsToOffset = new HashSet<int>();

            public OffsetHelper(Document doc, bool isRevit2020OrAbove)
            {
                var toOffsetCategories = new HashSet<BuiltInCategory>
                {
                    BuiltInCategory.OST_DuctAccessory,
                    BuiltInCategory.OST_DuctFitting,
                    BuiltInCategory.OST_PipeAccessory,
                    BuiltInCategory.OST_PipeFitting,
                    BuiltInCategory.OST_CableTrayFitting,
                    BuiltInCategory.OST_ConduitFitting,
                    BuiltInCategory.OST_DuctTerminal,
                    BuiltInCategory.OST_PlumbingFixtures,
                    BuiltInCategory.OST_GenericModel,
                    BuiltInCategory.OST_MechanicalEquipment
                };

                var noOffsetFrom2020Categories = new HashSet<BuiltInCategory>
                {
                    BuiltInCategory.OST_DuctAccessory,
                    BuiltInCategory.OST_DuctFitting,
                    BuiltInCategory.OST_PipeAccessory,
                    BuiltInCategory.OST_PipeFitting,
                    BuiltInCategory.OST_CableTrayFitting,
                    BuiltInCategory.OST_ConduitFitting
                };

                IEnumerable<BuiltInCategory> categories = isRevit2020OrAbove
                    ? toOffsetCategories.Where(c => !noOffsetFrom2020Categories.Contains(c))
                    : toOffsetCategories;

                foreach (BuiltInCategory categoryId in categories)
                {
                    Category category = Category.GetCategory(doc, categoryId);
                    if (category != null)
                    {
                        _categoryIdsToOffset.Add(ElementIdHelper.GetIntegerValue(category.Id));
                    }
                }
            }

            public bool IsRequired(Element element)
            {
                try
                {
                    return element?.Category != null &&
                           _categoryIdsToOffset.Contains(ElementIdHelper.GetIntegerValue(element.Category.Id));
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
