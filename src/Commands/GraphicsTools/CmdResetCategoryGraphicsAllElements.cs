#region Metadata
/*
 * Tool Name     : Reset Category Graphics in View
 * File Name     : CmdResetCategoryGraphicsAllElements.cs
 * Purpose       : Resets category graphics overrides for every overridable category in the active view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View - all overridable model and annotation categories.
 * Output        : Active-view categories reset to By View graphics (single undo step).
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Normal success is silent; validation and critical errors are reported to the user.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Reviewed Reset Category Graphics in View flow, shared validation, and metadata for release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
