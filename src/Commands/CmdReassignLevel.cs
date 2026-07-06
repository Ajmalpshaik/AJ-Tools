#region Metadata
/*
 * Tool Name     : Reassign Reference Level
 * File Name     : CmdReassignLevel.cs
 * Purpose       : Reassigns supported MEP elements (MEP curves, free-standing family instances, spaces)
 *                 from one level to another across the whole project without moving them physically.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-04-14
 * Last Updated  : 2026-07-02
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, Autodesk.Revit.DB.Mechanical, AJTools.UI, AJTools.Utils
 *
 * Input         : Full Project - FROM level and TO level chosen in a dialog.
 * Output        : Matching elements re-pointed to the TO level (host offset compensated so they stay put);
 *                 single undo step; final report of reassigned / failed / skipped counts.
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Scope is Full Project, so a confirmation dialog states how many elements will change before any edit.
 * - Hosted family instances are intentionally skipped (their level follows the host) and reported.
 * - All reassignments run inside ONE transaction, so a single Ctrl+Z reverses the whole operation.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-14) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: full metadata block; added Full-Project bulk-edit confirmation;
 *                       version-safe ElementId access. Reassign behaviour unchanged.
 * v1.2.0 (2026-07-02) - Replaced inline WinForms level picker with ModernStyles-based WPF dialog.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using AJTools.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Reassigns supported MEP elements from one level to another while keeping physical positions unchanged.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdReassignLevel : IExternalCommand
    {
        private const string ToolTitle = "Reassign Level";
        private const double MetersPerFoot = 0.3048;
        private const double OneMeterInFeet = 1.0 / MetersPerFoot;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            List<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (allLevels.Count < 2)
            {
                DialogHelper.ShowError(ToolTitle, "At least 2 levels are required.");
                return Result.Cancelled;
            }

            if (!TryPromptLevels(commandData.Application, allLevels, out Level fromLevel, out Level toLevel))
            {
                return Result.Cancelled;
            }

            ElementId fromId = fromLevel.Id;
            ElementId toId = toLevel.Id;
            bool isRevit2020OrAbove = IsRevit2020OrAbove(commandData.Application);
            var offsetHelper = new OffsetHelper(doc, isRevit2020OrAbove);

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

            if (candidates.Count == 0)
            {
                DialogHelper.ShowInfo(
                    ToolTitle,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "No elements were found on \"{0}\" that can be reassigned.",
                        fromLevel.Name));
                return Result.Cancelled;
            }

            // Full Project scope - confirm the bulk change before touching the model.
            string confirmMessage = string.Format(
                CultureInfo.CurrentCulture,
                "This will reassign {0} element(s) from \"{1}\" to \"{2}\" across the whole project.\n\n" +
                "Elements stay in the same physical position. Continue?",
                candidates.Count,
                fromLevel.Name,
                toLevel.Name);
            if (!DialogHelper.ShowYesNo(ToolTitle, confirmMessage))
            {
                return Result.Cancelled;
            }

            int okCount = 0;
            int failCount = 0;

            try
            {
                using (var tx = new Transaction(doc, string.Format("Reassign Level: {0} to {1}", fromLevel.Name, toLevel.Name)))
                {
                    tx.Start();

                    foreach (Element element in candidates)
                    {
                        try
                        {
                            if (ReassignElement(doc, element, fromLevel, toLevel, fromId, toId, offsetHelper))
                            {
                                okCount++;
                            }
                            else
                            {
                                failCount++;
                            }
                        }
                        catch
                        {
                            failCount++;
                        }
                    }

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            string resultMessage = string.Format(
                CultureInfo.CurrentCulture,
                "{0} element(s) reassigned\n\nFROM : {1}\nTO   : {2}",
                okCount,
                fromLevel.Name,
                toLevel.Name);

            if (failCount > 0)
            {
                resultMessage += string.Format(CultureInfo.CurrentCulture, "\n\n{0} element(s) failed.", failCount);
            }

            if (skippedHosted > 0)
            {
                resultMessage += string.Format(CultureInfo.CurrentCulture, "\n\n{0} hosted element(s) skipped.", skippedHosted);
            }

            resultMessage += "\n\nElements should stay in the same physical location.";
            DialogHelper.ShowInfo("Reassign Level - Complete", resultMessage);
            return Result.Succeeded;
        }

        private static bool IsRevit2020OrAbove(UIApplication uiApp)
        {
            int year;
            return int.TryParse(uiApp?.Application?.VersionNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out year) &&
                   year >= 2020;
        }

        private static bool TryPromptLevels(UIApplication uiApp, IList<Level> levels, out Level fromLevel, out Level toLevel)
        {
            fromLevel = null;
            toLevel = null;

            var window = new ReassignLevelWindow(levels);
            if (uiApp != null)
            {
                new WindowInteropHelper(window)
                {
                    Owner = uiApp.MainWindowHandle
                };
            }

            if (window.ShowDialog() != true)
            {
                return false;
            }

            fromLevel = window.SelectedFromLevel;
            toLevel = window.SelectedToLevel;
            return fromLevel != null && toLevel != null && fromLevel.Id != toLevel.Id;
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

        private static bool ReassignElement(
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

        private sealed class OffsetHelper
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
