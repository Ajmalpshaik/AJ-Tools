// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Applies graphics overrides to selected elements or selected categories from one combined tool.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.2.0
// Created      : 2026-05-07
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active view, selected graphics settings, and either selected elements or selected categories.
// Output       : Element or category graphics overrides applied in the active view.
// Notes        : Normal success is silent; the UI drives one shared Apply Graphics workflow for both modes.
// Changelog    : v1.2.0 - Combined element and category apply commands into one production-ready tool.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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

                IList<ElementId> preselectedElementIds = GraphicsSelectionService.GetValidPreselectedElementIds(
                    context.UIDocument,
                    selectionFilter: null);
                IList<Category> preselectedCategories = GraphicsCategoryService.GetUniqueCategoriesFromElements(
                    context.Document,
                    context.ActiveView,
                    preselectedElementIds,
                    includeAnnotationCategories: false);

                var settingsWindow = new GraphicsOverrideWindow(
                    context.Document,
                    context.ActiveView,
                    "Apply Graphics Settings",
                    preselectedCategories.Select(category => category.Id).ToList());
                if (settingsWindow.ShowDialog() != true)
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
                        settingsWindow.SelectedCategoryIds.Select(categoryId => categoryId.IntegerValue));
                    IList<Category> categories = GraphicsCategoryService.GetAvailableCategories(
                            context.Document,
                            context.ActiveView,
                            includeAnnotationCategories: false)
                        .Where(category => selectedCategoryKeys.Contains(category.Id.IntegerValue))
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
                    SelectionCaptureResult selection = GraphicsSelectionService.GetPreselectedOrPromptElementIds(
                        context.UIDocument,
                        selectionFilter: null,
                        prompt: "Select elements to apply graphics in the active view.");

                    if (selection.WasCancelled)
                    {
                        return Result.Cancelled;
                    }

                    if (selection.ElementIds.Count == 0)
                    {
                        DialogHelper.ShowError(DialogTitle, "Select at least one element.");
                        return Result.Cancelled;
                    }

                    summary = GraphicsCommandService.ExecuteSummaryTransaction(
                        context.Document,
                        "AJ Tools - Apply Graphics (Elements)",
                        () => GraphicsElementService.ApplyOverrides(
                            context.Document,
                            context.ActiveView,
                            selection.ElementIds,
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
