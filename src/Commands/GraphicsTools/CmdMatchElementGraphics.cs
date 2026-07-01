#region Metadata
/*
 * Tool Name     : Match Element Graphics
 * File Name     : CmdMatchElementGraphics.cs
 * Purpose       : Copies element-level graphics overrides from one picked source element to one or more picked target elements in the active view.
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
 * Input         : Active View - one source element, then one or more target elements (pick, ESC to finish).
 * Output        : Target element graphics overrides matched to the source in the active view (single undo step).
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - All target matches are grouped in one TransactionGroup so a single Ctrl+Z reverses the whole operation.
 * - ESC during a pick cancels silently (no error dialog); normal success is silent.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Grouped multi-target matching into one TransactionGroup (single undo); version-safe ElementId access; full metadata block.
 * v1.4.4 (2026-05-09) - Reviewed Match Element Graphics flow, shared validation, and metadata for release.
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

                using (var transactionGroup = new TransactionGroup(context.Document, "AJ Tools - Match Element Graphics"))
                {
                    transactionGroup.Start();

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
                            processedElementIds.Contains(ElementIdHelper.GetIntegerValue(targetElementId)) ||
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
                                processedElementIds.Add(ElementIdHelper.GetIntegerValue(targetElementId));
                                appliedCount++;
                            }
                            catch
                            {
                                transaction.RollBack();
                            }
                        }
                    }

                    if (appliedCount > 0)
                    {
                        transactionGroup.Assimilate();
                    }
                    else
                    {
                        transactionGroup.RollBack();
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
