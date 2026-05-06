// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Resets category graphics overrides for categories represented by selected elements.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Selected model elements in the active view.
// Output       : Selected categories reset to By View graphics.
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
using AJTools.Models.GraphicsTools;
using AJTools.Services.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Clears category overrides in the active view for unique model categories from selected elements.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetCategoryGraphics : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Reset Category Graphics";

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
                    "Select model elements to reset category graphics in the active view.");

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

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Reset Category Graphics",
                    () => GraphicsCategoryService.ApplyOverrides(
                        context.ActiveView,
                        categories,
                        new OverrideGraphicSettings(),
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

