#region Metadata
/*
 * Tool Name     : Reset Element Graphics in View
 * File Name     : CmdResetOverrides.cs
 * Purpose       : Clears element-level graphics overrides for the elements shown in the active view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View - all elements visible in the active view.
 * Output        : Active-view element overrides reset to By View graphics (single undo step).
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Collection is scoped to the active view (FilteredElementCollector(doc, view.Id)) - avoids a full-model scan.
 * - Read/reset of view graphics only; the model is never changed. Normal success is silent.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Scoped the collector to the active view (performance); full metadata block.
 * v1.4.4 (2026-05-09) - Aligned Reset Element Graphics in View with shared Graphics command validation and transactions.
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

namespace AJTools.Commands
{
    /// <summary>
    /// Clears per-element graphic overrides in the active view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetOverrides : IExternalCommand
    {
        /// <summary>
        /// Executes the reset overrides workflow.
        /// </summary>
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            const string dialogTitle = "Reset Element Graphics in View";

            try
            {
                Result contextResult = GraphicsCommandService.TryCreateContext(
                    commandData,
                    dialogTitle,
                    ref message,
                    out GraphicsCommandContext context);
                if (contextResult != Result.Succeeded)
                {
                    return contextResult;
                }

                ICollection<ElementId> elementIds = new FilteredElementCollector(context.Document, context.ActiveView.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                if (elementIds.Count == 0)
                {
                    return Result.Cancelled;
                }

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Reset Element Graphics in View",
                    () => GraphicsElementService.ClearOverrides(
                        context.Document,
                        context.ActiveView,
                        elementIds));

                return summary.HasChanges ? Result.Succeeded : Result.Cancelled;
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
