#region Metadata
/*
 * Tool Name     : Smart MEP Tagging Settings
 * File Name     : CmdSmartMepTagSettings.cs
 * Purpose       : Settings dialog to enable/disable and prioritise the MEP categories that Smart MEP Tags
 *                 will process, showing each category's element count in the model.
 *
 * Author        : Ajmal P.S.
 * Version       : 2.0.0
 *
 * Created Date  : 2026-04-20
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Services.SmartTag, AJTools.Utils,
 *                 AJTools.UI.SmartMepTag.SmartMepTagSettingsWindow (WPF)
 *
 * Input         : Active project - category enable/priority chosen in the settings window.
 * Output        : Saved Smart MEP Tag settings (read-only to the Revit model; no transaction).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Settings-only tool; reads category counts but does not modify the model.
 * - Modal WPF window shown from inside Execute, owned by the Revit main window.
 * - The window is pure UI: this command counts the elements, builds the rows, and owns the save.
 * - Cancel closes silently.
 *
 * Changelog     :
 * v1.2.0 (2026-04-20) - Category enable/priority grid with model counts.
 * v1.3.0 (2026-07-01) - Refactor/audit: added full metadata block. Settings behaviour unchanged.
 * v2.0.0 (2026-07-28) - UI rebuilt as a themed WPF window (was the last WinForms dialog in the suite):
 *                       live inline validation - unticking every category now disables Save with a
 *                       message instead of closing the dialog with an error popup and cancelling the
 *                       command; priority is a fixed-choice dropdown so an invalid value is impossible;
 *                       added Tag all / Tag none buttons; window owned by the Revit main window.
 *                       Dropped the "Settings saved." success popup per the house no-success-popup rule.
 *                       Offset carry-over and saved-state shape unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.SmartTag;
using AJTools.Services.SmartTag;
using AJTools.UI.SmartMepTag;
using AJTools.Utils;

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

            if (!TryPromptSettings(
                    commandData.Application, doc, initial,
                    out SmartTagSettingsState newState, out bool skipVerticalRuns))
            {
                return Result.Cancelled;
            }

            tracker.Save(newState);

            // Skip-vertical-runs is the one tagging setting shared with Create Tags and Stack Tags, and
            // the only one here that survives closing Revit. TrySetSkipVerticalRuns does the
            // read-modify-write so the Fix Tag Clash settings in the same file are left alone.
            if (!TagClashSettings.TrySetSkipVerticalRuns(skipVerticalRuns))
            {
                DialogHelper.ShowError(
                    ToolTitle,
                    "Your categories and priorities were saved, but the vertical-run tick box could not be.\n\n"
                    + "Check that AJ Tools can write to your AppData folder, then set it again.");
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private static bool TryPromptSettings(
            UIApplication uiapp,
            Document doc,
            SmartTagSettingsState initialState,
            out SmartTagSettingsState newState,
            out bool skipVerticalRuns)
        {
            newState = null;
            skipVerticalRuns = TagClashSettings.DefaultSkipVerticalRuns;

            Dictionary<BuiltInCategory, int> inModelCounts = CountElementsInModel(doc);

            // Build the rows here so the window stays pure UI (no collectors, no tracker access).
            var rows = new List<SmartTagCategoryRow>();
            foreach (BuiltInCategory category in SmartTagSettingsTracker.SupportedCategories)
            {
                int countInModel = inModelCounts.TryGetValue(category, out int count) ? count : 0;
                var row = new SmartTagCategoryRow(
                    category,
                    SmartTagSettingsTracker.GetCategoryLabel(category),
                    countInModel)
                {
                    Enabled = SmartTagSettingsTracker.IsCategoryEnabled(initialState, category),
                    PriorityText = GetPriorityDisplay(SmartTagSettingsTracker.ResolvePriority(initialState, category))
                };
                rows.Add(row);
            }

            var window = new SmartMepTagSettingsWindow(rows, TagClashSettings.ShouldSkipVerticalRuns());

            if (uiapp != null)
            {
                new WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };
            }

            if (window.ShowDialog() != true)
                return false;

            skipVerticalRuns = window.SkipVerticalRuns;

            return TryBuildStateFromRows(window.Rows, initialState, out newState);
        }

        private static bool TryBuildStateFromRows(
            IReadOnlyList<SmartTagCategoryRow> rows,
            SmartTagSettingsState initialState,
            out SmartTagSettingsState state)
        {
            state = new SmartTagSettingsState
            {
                CategoryEnabled = new Dictionary<BuiltInCategory, bool>(),
                CategoryOffsetInternal = new Dictionary<BuiltInCategory, double>(),
                CategoryPriority = new Dictionary<BuiltInCategory, TagPriority>()
            };

            int enabledCount = 0;
            double firstEnabledOffset = 0;

            foreach (SmartTagCategoryRow row in rows)
            {
                if (row == null)
                    continue;

                TagPriority priority = ParsePriority(row.PriorityText, SmartTagSettingsTracker.ResolvePriority(initialState, row.Category));
                double offsetInternal = SmartTagSettingsTracker.ResolveOffsetInternal(initialState, row.Category);

                state.CategoryEnabled[row.Category] = row.Enabled;
                state.CategoryOffsetInternal[row.Category] = offsetInternal;
                state.CategoryPriority[row.Category] = priority;

                if (row.Enabled)
                {
                    enabledCount++;
                    if (enabledCount == 1)
                        firstEnabledOffset = offsetInternal;
                }
            }

            // The window disables Save when nothing is ticked; re-check anyway.
            if (enabledCount == 0)
                return false;

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
                    return SmartTagCategoryRow.PriorityHigh;
                case TagPriority.Medium:
                    return SmartTagCategoryRow.PriorityMedium;
                default:
                    return SmartTagCategoryRow.PriorityLow;
            }
        }

        private static TagPriority ParsePriority(string text, TagPriority fallback)
        {
            if (string.Equals(text, SmartTagCategoryRow.PriorityHigh, StringComparison.OrdinalIgnoreCase))
                return TagPriority.High;

            if (string.Equals(text, SmartTagCategoryRow.PriorityMedium, StringComparison.OrdinalIgnoreCase))
                return TagPriority.Medium;

            if (string.Equals(text, SmartTagCategoryRow.PriorityLow, StringComparison.OrdinalIgnoreCase))
                return TagPriority.Low;

            return fallback;
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
