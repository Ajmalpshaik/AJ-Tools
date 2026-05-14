// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Resets category graphics overrides for all overridable categories in the active view.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-03-30
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit view.
// Output       : Active-view categories reset to By View graphics.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.4 - Reviewed Reset Category Graphics in View flow, shared validation, and metadata for release.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.GraphicsTools;
using AJTools.Services.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Clears category overrides in the active view for every category Revit allows to be overridden in that view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetCategoryGraphicsAllElements : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Reset Category Graphics in View";

            try
            {
                Result contextResult = GraphicsCommandService.TryCreateContext(
                    commandData,
                    dialogTitle,
                    ref message,
                    out GraphicsCommandContext context);
                if (contextResult != Result.Succeeded)
                    return contextResult;

                var categories = GraphicsCategoryService.GetAvailableCategories(
                    context.Document,
                    context.ActiveView,
                    includeAnnotationCategories: true);

                if (categories.Count == 0)
                {
                    return Result.Cancelled;
                }

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Reset Category Graphics in View",
                    () => GraphicsCategoryService.ApplyOverrides(
                        context.ActiveView,
                        categories,
                        new OverrideGraphicSettings(),
                        includeAnnotationCategories: true));

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
