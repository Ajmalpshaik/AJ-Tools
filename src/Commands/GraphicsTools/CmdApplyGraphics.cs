#region Metadata
/*
 * Tool Name     : Apply Graphics
 * File Name     : CmdApplyGraphics.cs
 * Purpose       : Applies the same graphics overrides to selected elements or to their categories from one combined settings window.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-05-07
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View - selected source elements and the chosen graphics settings (Element mode or Category mode).
 * Output        : Element or category graphics overrides applied in the active view (single undo step per apply).
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Normal success is silent; both apply modes use one selected-element source.
 * - A single settings window instance is enforced to prevent duplicate dialogs.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Version-safe ElementId access; full metadata block.
 * v1.4.4 (2026-05-09) - Uses the reference-style split apply buttons and prevents duplicate settings windows.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.GraphicsTools;
using AJTools.Services.GraphicsTools;
using AJTools.UI.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    [Transaction(TransactionMode.Manual)]
    public class CmdApplyGraphics : IExternalCommand
    {
        private const string DialogTitle = "Apply Graphics";
        private static bool _isSettingsWindowOpen;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Result contextResult = GraphicsCommandService.TryCreateContext(
                    commandData,
                    DialogTitle,
                    ref message,
                    out GraphicsCommandContext context);
                if (contextResult != Result.Succeeded)
                {
                    return contextResult;
                }

                SelectionCaptureResult sourceSelection = GraphicsSelectionService.GetPreselectedOrPromptElementIds(
                    context.UIDocument,
                    selectionFilter: null,
                    prompt: "Select elements to use for Apply Graphics.");
                if (sourceSelection.WasCancelled)
                {
                    return Result.Cancelled;
                }

                if (sourceSelection.ElementIds.Count == 0)
                {
                    DialogHelper.ShowError(DialogTitle, "Select at least one element.");
                    return Result.Cancelled;
                }

                IList<Category> preselectedCategories = GraphicsCategoryService.GetUniqueCategoriesFromElements(
                    context.Document,
                    context.ActiveView,
                    sourceSelection.ElementIds,
                    includeAnnotationCategories: false);

                if (_isSettingsWindowOpen)
                {
                    DialogHelper.ShowError(DialogTitle, "The Apply Graphics settings window is already open.");
                    return Result.Cancelled;
                }

                GraphicsOverrideWindow settingsWindow = null;
                bool? settingsAccepted;

                try
                {
                    _isSettingsWindowOpen = true;
                    settingsWindow = new GraphicsOverrideWindow(
                        context.Document,
                        context.ActiveView,
                        "Apply Graphics Settings",
                        preselectedCategories,
                        preselectedCategories.Select(category => category.Id).ToList());
                    settingsAccepted = settingsWindow.ShowDialog();
                }
                finally
                {
                    _isSettingsWindowOpen = false;
                }

                if (settingsAccepted != true)
                {
                    return Result.Cancelled;
                }

                OverrideGraphicSettings settings = settingsWindow.SelectedOverrideSettings ?? new OverrideGraphicSettings();
                GraphicsOperationSummary summary;

                if (settingsWindow.SelectedApplyMode == GraphicsApplyMode.Categories)
                {
                    if (settingsWindow.SelectedCategoryIds.Count == 0)
                    {
                        DialogHelper.ShowError(DialogTitle, "Select at least one category.");
                        return Result.Cancelled;
                    }

                    HashSet<int> selectedCategoryKeys = new HashSet<int>(
                        settingsWindow.SelectedCategoryIds.Select(categoryId => ElementIdHelper.GetIntegerValue(categoryId)));
                    IList<Category> categories = preselectedCategories
                        .Where(category => selectedCategoryKeys.Contains(ElementIdHelper.GetIntegerValue(category.Id)))
                        .ToList();

                    summary = GraphicsCommandService.ExecuteSummaryTransaction(
                        context.Document,
                        "AJ Tools - Apply Graphics (Categories)",
                        () => GraphicsCategoryService.ApplyOverrides(
                            context.ActiveView,
                            categories,
                            settings,
                            includeAnnotationCategories: false));
                }
                else
                {
                    summary = GraphicsCommandService.ExecuteSummaryTransaction(
                        context.Document,
                        "AJ Tools - Apply Graphics (Elements)",
                        () => GraphicsElementService.ApplyOverrides(
                            context.Document,
                            context.ActiveView,
                            sourceSelection.ElementIds,
                            settings));
                }

                if (!summary.HasChanges)
                {
                    DialogHelper.ShowError(DialogTitle, "No graphics overrides were applied.");
                    return Result.Cancelled;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(DialogTitle, ex.Message);
                return Result.Failed;
            }
        }
    }
}
