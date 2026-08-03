#region Metadata
/*
 * Tool Name     : Create Tags Settings
 * File Name     : CmdCreateTagsSettings.cs
 * Purpose       : Settings dialog to enable/disable the categories Create Tags can pick from, and
 *                 set the minimum element length below which an element is skipped.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.CreateTags, AJTools.Services.CreateTags,
 *                 AJTools.Services.SmartTag (SmartTagSettingsTracker.SupportedCategories/GetCategoryLabel),
 *                 AJTools.Utils, AJTools.UI.CreateTags.CreateTagsSettingsWindow (WPF)
 *
 * Input         : Active project - category enable/minimum length chosen in the settings window.
 * Output        : Saved Create Tags settings (read-only to the Revit model; no transaction).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Settings-only tool; reads category counts but does not modify the model.
 * - Modal WPF window shown from inside Execute, owned by the Revit main window.
 * - The window is pure UI: this command counts the elements, builds the rows, and owns the save.
 * - Cancel closes silently.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.CreateTags;
using AJTools.Services.CreateTags;
using AJTools.Services.SmartTag;
using AJTools.UI.CreateTags;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens Create Tags settings dialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCreateTagsSettings : IExternalCommand
    {
        private const string ToolTitle = "Create Tags";

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

            var tracker = new CreateTagsSettingsTracker(doc);
            CreateTagsSettingsState initial = CreateTagsSettingsTracker.EnsureDefaults(tracker.LastState);

            if (!TryPromptSettings(commandData.Application, doc, initial, out CreateTagsSettingsState newState))
                return Result.Cancelled;

            tracker.Save(newState);
            return Result.Succeeded;
        }

        private static bool TryPromptSettings(
            UIApplication uiapp,
            Document doc,
            CreateTagsSettingsState initialState,
            out CreateTagsSettingsState newState)
        {
            newState = null;

            Dictionary<BuiltInCategory, int> inModelCounts = CountElementsInModel(doc);

            // Build the rows here so the window stays pure UI (no collectors, no tracker access).
            var rows = new List<CreateTagsCategoryRow>();
            foreach (BuiltInCategory category in SmartTagSettingsTracker.SupportedCategories)
            {
                int countInModel = inModelCounts.TryGetValue(category, out int count) ? count : 0;
                var row = new CreateTagsCategoryRow(
                    category,
                    SmartTagSettingsTracker.GetCategoryLabel(category),
                    countInModel)
                {
                    Enabled = CreateTagsSettingsTracker.IsCategoryEnabled(initialState, category)
                };
                rows.Add(row);
            }

            double currentMinLengthMm = CreateTagsSettingsTracker.ResolveMinLengthInternal(initialState) / Constants.MM_TO_FEET;

            var window = new CreateTagsSettingsWindow(rows, currentMinLengthMm);

            if (uiapp != null)
            {
                new WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };
            }

            if (window.ShowDialog() != true)
                return false;

            newState = new CreateTagsSettingsState
            {
                CategoryEnabled = new Dictionary<BuiltInCategory, bool>(),
                MinLengthInternal = window.MinLengthMm * Constants.MM_TO_FEET
            };

            foreach (CreateTagsCategoryRow row in window.Rows)
            {
                if (row == null)
                    continue;

                newState.CategoryEnabled[row.Category] = row.Enabled;
            }

            return true;
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
