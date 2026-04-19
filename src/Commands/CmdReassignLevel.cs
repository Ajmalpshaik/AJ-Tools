// Tool Name: Reassign Level
// Description: Reassigns supported MEP elements from one level to another without changing physical position.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-14
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, Autodesk.Revit.DB.Mechanical, AJTools.Utils

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using AJTools.Utils;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

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

            if (!TryPromptLevels(allLevels, out Level fromLevel, out Level toLevel))
            {
                return Result.Cancelled;
            }

            ElementId fromId = fromLevel.Id;
            ElementId toId = toLevel.Id;
            bool isRevit2020OrAbove = IsRevit2020OrAbove(commandData.Application);
            var offsetHelper = new OffsetHelper(doc, isRevit2020OrAbove);

            var allElements = new FilteredElementCollector(doc)
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

        private static bool TryPromptLevels(IList<Level> levels, out Level fromLevel, out Level toLevel)
        {
            fromLevel = null;
            toLevel = null;

            var levelItems = levels.Select(level => new LevelChoice(level)).ToList();

            using (var form = new WinForms.Form())
            using (var intro = new WinForms.Label())
            using (var fromLabel = new WinForms.Label())
            using (var fromCombo = new WinForms.ComboBox())
            using (var toLabel = new WinForms.Label())
            using (var toCombo = new WinForms.ComboBox())
            using (var okButton = new WinForms.Button())
            using (var cancelButton = new WinForms.Button())
            {
                form.Text = ToolTitle;
                form.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                form.StartPosition = WinForms.FormStartPosition.CenterScreen;
                form.ClientSize = new Drawing.Size(460, 225);
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;

                intro.Text = "Switch the level reference of supported MEP elements from one level to another without moving them.";
                intro.AutoSize = false;
                intro.Size = new Drawing.Size(430, 32);
                intro.Location = new Drawing.Point(15, 12);

                fromLabel.Text = "FROM Level:";
                fromLabel.AutoSize = true;
                fromLabel.Font = new Drawing.Font(form.Font, Drawing.FontStyle.Bold);
                fromLabel.ForeColor = Drawing.Color.FromArgb(192, 0, 0);
                fromLabel.Location = new Drawing.Point(15, 56);

                fromCombo.DropDownStyle = WinForms.ComboBoxStyle.DropDownList;
                fromCombo.FormattingEnabled = true;
                fromCombo.Width = 430;
                fromCombo.Location = new Drawing.Point(15, 76);

                toLabel.Text = "TO Level:";
                toLabel.AutoSize = true;
                toLabel.Font = new Drawing.Font(form.Font, Drawing.FontStyle.Bold);
                toLabel.ForeColor = Drawing.Color.FromArgb(0, 102, 0);
                toLabel.Location = new Drawing.Point(15, 112);

                toCombo.DropDownStyle = WinForms.ComboBoxStyle.DropDownList;
                toCombo.FormattingEnabled = true;
                toCombo.Width = 430;
                toCombo.Location = new Drawing.Point(15, 132);

                foreach (LevelChoice item in levelItems)
                {
                    fromCombo.Items.Add(item);
                    toCombo.Items.Add(item);
                }

                if (fromCombo.Items.Count > 0)
                {
                    fromCombo.SelectedIndex = 0;
                }

                if (toCombo.Items.Count > 1)
                {
                    toCombo.SelectedIndex = 1;
                }
                else if (toCombo.Items.Count > 0)
                {
                    toCombo.SelectedIndex = 0;
                }

                okButton.Text = "Reassign Elements";
                okButton.DialogResult = WinForms.DialogResult.OK;
                okButton.Width = 130;
                okButton.Location = new Drawing.Point(235, 178);

                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = WinForms.DialogResult.Cancel;
                cancelButton.Width = 95;
                cancelButton.Location = new Drawing.Point(350, 178);

                form.Controls.Add(intro);
                form.Controls.Add(fromLabel);
                form.Controls.Add(fromCombo);
                form.Controls.Add(toLabel);
                form.Controls.Add(toCombo);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() != WinForms.DialogResult.OK)
                {
                    return false;
                }

                LevelChoice fromChoice = fromCombo.SelectedItem as LevelChoice;
                LevelChoice toChoice = toCombo.SelectedItem as LevelChoice;

                if (fromChoice == null || toChoice == null)
                {
                    DialogHelper.ShowError(ToolTitle, "Please select both levels.");
                    return false;
                }

                if (fromChoice.Level.Id == toChoice.Level.Id)
                {
                    DialogHelper.ShowError(ToolTitle, "FROM and TO levels must be different.");
                    return false;
                }

                fromLevel = fromChoice.Level;
                toLevel = toChoice.Level;
                return true;
            }
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

        private static double FeetToMeters(double feet)
        {
            return feet * MetersPerFoot;
        }

        private sealed class LevelChoice
        {
            public LevelChoice(Level level)
            {
                Level = level;
                Label = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1:0.000} m)",
                    level?.Name ?? "<Unnamed>",
                    level == null ? 0 : FeetToMeters(level.Elevation));
            }

            public Level Level { get; }
            public string Label { get; }

            public override string ToString()
            {
                return Label;
            }
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
                        _categoryIdsToOffset.Add(category.Id.IntegerValue);
                    }
                }
            }

            public bool IsRequired(Element element)
            {
                try
                {
                    return element?.Category != null &&
                           _categoryIdsToOffset.Contains(element.Category.Id.IntegerValue);
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
