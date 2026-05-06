// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Applies category graphics overrides to selected model categories.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Selected model elements and graphics override settings.
// Output       : Category graphics overrides applied in the active view.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.1.0 - Cleaned Graphics Tools command flow, shared validation/transaction handling, and metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.GraphicsTools;
using AJTools.Models.GraphicsTools;
using AJTools.UI.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Applies category graphics overrides to unique model categories from selected elements.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCategoryGraphics : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Category Graphics";

            try
            {
                Result contextResult = GraphicsCommandService.TryCreateContext(
                    commandData,
                    dialogTitle,
                    ref message,
                    out GraphicsCommandContext context);
                if (contextResult != Result.Succeeded)
                    return contextResult;

                SelectionCaptureResult selection = GraphicsSelectionService.GetPreselectedOrPromptElementIds(
                    context.UIDocument,
                    new ModelCategorySelectionFilter(),
                    "Select model elements to apply category graphics in the active view.");

                if (selection.WasCancelled)
                {
                    return Result.Cancelled;
                }

                if (selection.ElementIds.Count == 0)
                {
                    return Result.Cancelled;
                }

                IList<Category> categories = GraphicsCategoryService.GetUniqueCategoriesFromElements(
                    context.Document,
                    context.ActiveView,
                    selection.ElementIds,
                    includeAnnotationCategories: false);

                if (categories.Count == 0)
                {
                    return Result.Cancelled;
                }

                var settingsWindow = new GraphicsOverrideWindow(
                    context.Document,
                    "Category Graphics Settings",
                    GraphicsOverrideWindowMode.CategoryApply);
                if (settingsWindow.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                OverrideGraphicSettings settings = settingsWindow.SelectedOverrideSettings ?? new OverrideGraphicSettings();

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Category Graphics",
                    () => GraphicsCategoryService.ApplyOverrides(
                        context.ActiveView,
                        categories,
                        settings,
                        includeAnnotationCategories: false));

                if (!summary.HasChanges)
                {
                    return Result.Cancelled;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(dialogTitle, ex.Message);
                return Result.Failed;
            }
        }
    }
}

