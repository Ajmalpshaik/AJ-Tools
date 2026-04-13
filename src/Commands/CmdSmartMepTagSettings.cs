// Tool Name: Smart MEP Tag Settings Command
// Description: Opens settings to configure Smart MEP Tag category selection.
// Author: Ajmal P.S.
// Version: 1.2.0
// Revit Version: 2020

using System.Collections.Generic;
using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.SmartTag;
using AJTools.Services.SmartTag;
using AJTools.Utils;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens Smart MEP Tag settings dialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSmartMepTagSettings : IExternalCommand
    {
        private const string ToolTitle = "Smart MEP Tag";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

            var tracker = new SmartTagSettingsTracker(doc);
            SmartTagSettingsState initial = SmartTagSettingsTracker.EnsureDefaults(tracker.LastState);

            if (!TryPromptSettings(doc, initial, out SmartTagSettingsState newState))
                return Result.Cancelled;

            tracker.Save(newState);
            DialogHelper.ShowInfo(ToolTitle, "Settings saved.");
            return Result.Succeeded;
        }

        private static bool TryPromptSettings(
            Document doc,
            SmartTagSettingsState initialState,
            out SmartTagSettingsState newState)
        {
            newState = null;

            Dictionary<BuiltInCategory, int> inModelCounts = CountElementsInModel(doc);
            var rowCategoryMap = new Dictionary<int, BuiltInCategory>();

            using (var form = new WinForms.Form())
            using (var title = new WinForms.Label())
            using (var note = new WinForms.Label())
            using (var grid = new WinForms.DataGridView())
            using (var save = new WinForms.Button())
            using (var cancel = new WinForms.Button())
            {
                form.Text = "Smart MEP Tag Settings";
                form.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                form.StartPosition = WinForms.FormStartPosition.CenterScreen;
                form.ClientSize = new Drawing.Size(660, 420);
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                title.Text = "Smart MEP Tag Settings";
                title.Font = new Drawing.Font(form.Font, Drawing.FontStyle.Bold);
                title.AutoSize = true;
                title.Location = new Drawing.Point(12, 12);

                note.Text = "Choose category-wise Tag ON/OFF and Priority.";
                note.AutoSize = true;
                note.ForeColor = Drawing.Color.DimGray;
                note.Location = new Drawing.Point(12, 36);

                grid.Location = new Drawing.Point(12, 60);
                grid.Size = new Drawing.Size(636, 300);
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.AllowUserToResizeRows = false;
                grid.MultiSelect = false;
                grid.RowHeadersVisible = false;
                grid.SelectionMode = WinForms.DataGridViewSelectionMode.CellSelect;
                grid.AutoSizeColumnsMode = WinForms.DataGridViewAutoSizeColumnsMode.Fill;

                var colCategory = new WinForms.DataGridViewTextBoxColumn
                {
                    HeaderText = "Element Type",
                    ReadOnly = true
                };

                var colCount = new WinForms.DataGridViewTextBoxColumn
                {
                    HeaderText = "In Model",
                    ReadOnly = true,
                    FillWeight = 60
                };

                var colEnable = new WinForms.DataGridViewCheckBoxColumn
                {
                    HeaderText = "Tag?",
                    FillWeight = 45
                };
                
                var colPriority = new WinForms.DataGridViewComboBoxColumn
                {
                    HeaderText = "Priority",
                    FillWeight = 70,
                    DisplayStyle = WinForms.DataGridViewComboBoxDisplayStyle.DropDownButton
                };
                colPriority.Items.AddRange("High", "Medium", "Low");

                grid.Columns.Add(colCategory);
                grid.Columns.Add(colCount);
                grid.Columns.Add(colPriority);
                grid.Columns.Add(colEnable);

                foreach (BuiltInCategory category in SmartTagSettingsTracker.SupportedCategories)
                {
                    bool enabled = SmartTagSettingsTracker.IsCategoryEnabled(initialState, category);
                    TagPriority priority = SmartTagSettingsTracker.ResolvePriority(initialState, category);
                    int countInModel = inModelCounts.TryGetValue(category, out int count) ? count : 0;

                    int rowIndex = grid.Rows.Add(
                        SmartTagSettingsTracker.GetCategoryLabel(category),
                        countInModel.ToString(),
                        GetPriorityDisplay(priority),
                        enabled);
                    rowCategoryMap[rowIndex] = category;
                }

                save.Text = "Save";
                save.DialogResult = WinForms.DialogResult.OK;
                save.Location = new Drawing.Point(492, 376);
                save.Width = 75;

                cancel.Text = "Cancel";
                cancel.DialogResult = WinForms.DialogResult.Cancel;
                cancel.Location = new Drawing.Point(573, 376);
                cancel.Width = 75;

                form.Controls.Add(title);
                form.Controls.Add(note);
                form.Controls.Add(grid);
                form.Controls.Add(save);
                form.Controls.Add(cancel);
                form.AcceptButton = save;
                form.CancelButton = cancel;

                if (form.ShowDialog() != WinForms.DialogResult.OK)
                    return false;

                grid.EndEdit();

                if (!TryBuildStateFromGrid(
                    grid,
                    rowCategoryMap,
                    initialState,
                    out newState,
                    out string error))
                {
                    DialogHelper.ShowError(ToolTitle, error);
                    return false;
                }
            }

            return true;
        }

        private static bool TryBuildStateFromGrid(
            WinForms.DataGridView grid,
            IDictionary<int, BuiltInCategory> rowCategoryMap,
            SmartTagSettingsState initialState,
            out SmartTagSettingsState state,
            out string error)
        {
            state = new SmartTagSettingsState
            {
                CategoryEnabled = new Dictionary<BuiltInCategory, bool>(),
                CategoryOffsetInternal = new Dictionary<BuiltInCategory, double>(),
                CategoryPriority = new Dictionary<BuiltInCategory, TagPriority>()
            };
            error = null;

            int enabledCount = 0;
            double firstEnabledOffset = 0;

            foreach (WinForms.DataGridViewRow row in grid.Rows)
            {
                if (row == null || !rowCategoryMap.TryGetValue(row.Index, out BuiltInCategory category))
                    continue;

                bool enabled = false;
                object enabledRaw = row.Cells[3].Value;
                if (enabledRaw is bool b)
                    enabled = b;
                
                object priorityRaw = row.Cells[2].Value;
                if (!TryParsePriority(priorityRaw, out TagPriority priority))
                {
                    error = string.Format("Select a valid priority for '{0}'.", SmartTagSettingsTracker.GetCategoryLabel(category));
                    return false;
                }

                double offsetInternal = SmartTagSettingsTracker.ResolveOffsetInternal(initialState, category);

                state.CategoryEnabled[category] = enabled;
                state.CategoryOffsetInternal[category] = offsetInternal;
                state.CategoryPriority[category] = priority;

                if (enabled)
                {
                    enabledCount++;
                    if (enabledCount == 1)
                        firstEnabledOffset = offsetInternal;
                }
            }

            if (enabledCount == 0)
            {
                error = "Enable at least one category to run Smart MEP Tag.";
                return false;
            }

            state.OffsetInternal = firstEnabledOffset > Constants.ZERO_LENGTH_TOLERANCE
                ? firstEnabledOffset
                : SmartTagSettingsTracker.ResolveOffsetInternal(initialState);

            return true;
        }

        private static string GetPriorityDisplay(TagPriority priority)
        {
            switch (priority)
            {
                case TagPriority.High:
                    return "High";
                case TagPriority.Medium:
                    return "Medium";
                default:
                    return "Low";
            }
        }

        private static bool TryParsePriority(object value, out TagPriority priority)
        {
            priority = TagPriority.Low;
            string text = value as string;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (string.Equals(text, "High", StringComparison.OrdinalIgnoreCase))
            {
                priority = TagPriority.High;
                return true;
            }

            if (string.Equals(text, "Medium", StringComparison.OrdinalIgnoreCase))
            {
                priority = TagPriority.Medium;
                return true;
            }

            if (string.Equals(text, "Low", StringComparison.OrdinalIgnoreCase))
            {
                priority = TagPriority.Low;
                return true;
            }

            return false;
        }

        private static Dictionary<BuiltInCategory, int> CountElementsInModel(Document doc)
        {
            var counts = new Dictionary<BuiltInCategory, int>();
            if (doc == null)
                return counts;

            foreach (BuiltInCategory category in SmartTagSettingsTracker.SupportedCategories)
            {
                int count = 0;
                try
                {
                    count = new FilteredElementCollector(doc)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
                }
                catch
                {
                    count = 0;
                }

                counts[category] = count;
            }

            return counts;
        }
    }
}
