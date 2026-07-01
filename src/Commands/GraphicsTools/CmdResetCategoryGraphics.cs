#region Metadata
/*
 * Tool Name     : Reset Category Graphics by Selection
 * File Name     : CmdResetCategoryGraphics.cs
 * Purpose       : Resets category graphics overrides for the categories represented by the selected elements in the active view.
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
 * Input         : Active View - selected model elements (preselected, or picked when none preselected).
 * Output        : The categories of the selected elements reset to By View graphics (single undo step).
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Normal success is silent; missing selection and critical errors are reported to the user.
 * - ESC during a pick cancels silently (no error dialog).
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Added a missing-selection message; full metadata block.
 * v1.4.4 (2026-05-09) - Reviewed Reset Category Graphics flow, shared validation, and metadata for release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
            const string dialogTitle = "Reset Category Graphics by Selection";

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
                    DialogHelper.ShowError(dialogTitle, "Select at least one model element.");
                    return Result.Cancelled;
                }

                IList<Category> categories = GraphicsCategoryService.GetUniqueCategoriesFromElements(
                    context.Document,
                    context.ActiveView,
                    selection.ElementIds,
                    includeAnnotationCategories: false);

                if (categories.Count == 0)
                {
                    DialogHelper.ShowError(dialogTitle, "The selected elements have no overridable model category in this view.");
                    return Result.Cancelled;
                }

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Reset Category Graphics by Selection",
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
