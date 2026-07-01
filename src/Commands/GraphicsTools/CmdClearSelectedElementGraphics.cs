#region Metadata
/*
 * Tool Name     : Reset Element Graphics by Selection
 * File Name     : CmdClearSelectedElementGraphics.cs
 * Purpose       : Clears element-level graphics overrides from the selected elements in the active view.
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
 * Input         : Active View - selected elements (preselected, or picked when none preselected).
 * Output        : Selected element overrides reset to By View graphics (single undo step).
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Normal success is silent; missing selection and critical errors are reported to the user.
 * - ESC during a pick cancels silently (no error dialog).
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Aligned dialog title with the ribbon label and added a missing-selection message; full metadata block.
 * v1.4.4 (2026-05-09) - Reviewed Graphics reset flow, shared validation, and metadata for release.
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
    /// Clears element-level graphics overrides for selected elements in the active view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdClearSelectedElementGraphics : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Reset Element Graphics by Selection";

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
                    selectionFilter: null,
                    prompt: "Select elements to clear element graphics in the active view.");

                if (selection.WasCancelled)
                {
                    return Result.Cancelled;
                }

                if (selection.ElementIds.Count == 0)
                {
                    DialogHelper.ShowError(dialogTitle, "Select at least one element.");
                    return Result.Cancelled;
                }

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Reset Element Graphics by Selection",
                    () => GraphicsElementService.ClearOverrides(
                        context.Document,
                        context.ActiveView,
                        selection.ElementIds));

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
