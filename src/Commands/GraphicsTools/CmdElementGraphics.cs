// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Applies element graphics overrides to selected elements.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Selected elements and graphics override settings.
// Output       : Element graphics overrides applied in the active view.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.1.0 - Cleaned Graphics Tools command flow, shared validation/transaction handling, and metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.GraphicsTools;
using AJTools.Services.GraphicsTools;
using AJTools.UI.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Applies element-level graphics overrides to selected elements in the active view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdElementGraphics : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Element Graphics";

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
                    prompt: "Select elements to apply element graphics in the active view.");

                if (selection.WasCancelled)
                {
                    return Result.Cancelled;
                }

                if (selection.ElementIds.Count == 0)
                {
                    return Result.Cancelled;
                }

                var settingsWindow = new GraphicsOverrideWindow(context.Document, "Element Graphics Settings");
                if (settingsWindow.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                OverrideGraphicSettings settings = settingsWindow.SelectedOverrideSettings ?? new OverrideGraphicSettings();

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Element Graphics",
                    () => GraphicsElementService.ApplyOverrides(
                        context.Document,
                        context.ActiveView,
                        selection.ElementIds,
                        settings));

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

