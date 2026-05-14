// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Clears element-level graphics overrides from document elements in the active view.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2025-12-10
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit view.
// Output       : Active-view element overrides reset to By View graphics.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.4 - Aligned Reset Element Graphics in View with shared Graphics command validation and transactions.
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

                ICollection<ElementId> elementIds = new FilteredElementCollector(context.Document)
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
