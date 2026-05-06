// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Resets category graphics overrides for model categories found in the active view.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit view with visible model elements.
// Output       : Active-view model categories reset to By View graphics.
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
    /// Clears category overrides in the active view for model categories found from all elements visible in that view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetCategoryGraphicsAllElements : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Reset Category Graphics - All Elements";

            try
            {
                Result contextResult = GraphicsCommandService.TryCreateContext(
                    commandData,
                    dialogTitle,
                    ref message,
                    out GraphicsCommandContext context);
                if (contextResult != Result.Succeeded)
                    return contextResult;

                ICollection<ElementId> allElementIdsInView = new FilteredElementCollector(context.Document, context.ActiveView.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                if (allElementIdsInView.Count == 0)
                {
                    return Result.Cancelled;
                }

                IList<Category> categories = GraphicsCategoryService.GetUniqueCategoriesFromElements(
                    context.Document,
                    context.ActiveView,
                    allElementIdsInView,
                    includeAnnotationCategories: false);

                if (categories.Count == 0)
                {
                    return Result.Cancelled;
                }

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Reset Category Graphics (All Elements)",
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
