// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Copies element override settings from a source element to target elements.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : One source element and one or more target elements.
// Output       : Target element graphics overrides matched in the active view.
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
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Matches source element graphics override to selected target elements in the active view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMatchElementGraphics : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Match Element Graphics";

            try
            {
                Result contextResult = GraphicsCommandService.TryCreateContext(
                    commandData,
                    dialogTitle,
                    ref message,
                    out GraphicsCommandContext context);
                if (contextResult != Result.Succeeded)
                    return contextResult;

                if (!GraphicsSelectionService.TryPickSingleElementId(
                    context.UIDocument,
                    selectionFilter: null,
                    prompt: "Pick SOURCE element to copy element graphics from.",
                    out ElementId sourceElementId,
                    out _))
                {
                    return Result.Cancelled;
                }

                if (context.Document.GetElement(sourceElementId) == null)
                {
                    DialogHelper.ShowError(dialogTitle, "Source element is no longer valid.");
                    return Result.Cancelled;
                }

                OverrideGraphicSettings sourceSettings = GraphicsOverrideBuilder.Clone(
                    context.ActiveView.GetElementOverrides(sourceElementId));

                var processedElementIds = new HashSet<int>();
                int appliedCount = 0;

                while (true)
                {
                    if (!GraphicsSelectionService.TryPickSingleElementId(
                        context.UIDocument,
                        selectionFilter: null,
                        prompt: "Select TARGET element (ESC to finish).",
                        out ElementId targetElementId,
                        out bool wasCancelled))
                    {
                        if (wasCancelled)
                        {
                            break;
                        }

                        continue;
                    }

                    if (targetElementId == sourceElementId ||
                        processedElementIds.Contains(targetElementId.IntegerValue) ||
                        context.Document.GetElement(targetElementId) == null)
                    {
                        continue;
                    }

                    using (var transaction = new Transaction(context.Document, "AJ Tools - Match Element Graphics"))
                    {
                        transaction.Start();
                        try
                        {
                            context.ActiveView.SetElementOverrides(targetElementId, sourceSettings);
                            transaction.Commit();
                            processedElementIds.Add(targetElementId.IntegerValue);
                            appliedCount++;
                        }
                        catch
                        {
                            transaction.RollBack();
                        }
                    }
                }

                if (appliedCount == 0)
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

