#region Metadata
/*
 * Tool Name     : Copy Dimension Text
 * File Name     : CmdCopyDimensionText.cs
 * Purpose       : Copies the Above / Below / Prefix / Suffix text and value override from one picked
 *                 source dimension onto one or more picked target dimensions.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (DimensionSelectionFilter, DialogHelper)
 *
 * Input         : Active View - one source dimension, then target dimensions picked one-by-one (Esc to finish).
 * Output        : Target dimension text fields matched to the source (single undo step); silent success.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - The whole pick session is wrapped in one TransactionGroup and assimilated, so a single Ctrl+Z
 *   reverses every pasted dimension.
 * - Esc during a pick is a normal cancel (handled silently); normal success is silent.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.1.0 (2025-12-10) - Source/target pick loop with value-override copy.
 * v1.2.0 (2026-07-01) - Refactor/audit: full metadata block; whole pick session now assimilated into a
 *                       single undo step. Copy behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Copies dimension text fields from a source dimension to selected targets.
    /// Workflow:
    /// 1) Pick SOURCE dimension once.
    /// 2) Repeatedly pick TARGET dimensions (one by one).
    /// 3) Press ESC to finish.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCopyDimensionText : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            Document doc = uidoc.Document;

            try
            {
                // 1) Pick SOURCE dimension
                Reference sourceRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new DimensionSelectionFilter(),
                    "Select SOURCE dimension to copy text from");

                Dimension sourceDim = doc.GetElement(sourceRef) as Dimension;
                if (sourceDim == null)
                    return Result.Cancelled;

                string textAbove = sourceDim.Above;
                string textBelow = sourceDim.Below;
                string textPrefix = sourceDim.Prefix;
                string textSuffix = sourceDim.Suffix;
                string valueOverride = sourceDim.ValueOverride;
                bool copyValueOverride = !string.IsNullOrEmpty(valueOverride);

                bool hasCopyableText =
                    !string.IsNullOrEmpty(textAbove) ||
                    !string.IsNullOrEmpty(textBelow) ||
                    !string.IsNullOrEmpty(textPrefix) ||
                    !string.IsNullOrEmpty(textSuffix) ||
                    copyValueOverride;

                if (!hasCopyableText)
                {
                    DialogHelper.ShowError("Copy Dim Text", "The selected dimension has no Above/Below/Prefix/Suffix text or value override to copy.");
                    return Result.Cancelled;
                }

                const string targetPrompt = "Select TARGET dimension (ESC to finish)";
                int pastedCount = 0;

                // 2) Loop: pick TARGET dimensions until ESC. The whole session is grouped so a single
                //    Ctrl+Z reverses every pasted dimension.
                using (TransactionGroup group = new TransactionGroup(doc, "AJ-Tools: Copy Dimension Text"))
                {
                    group.Start();

                    while (true)
                    {
                        Reference targetRef;
                        try
                        {
                            targetRef = uidoc.Selection.PickObject(
                                ObjectType.Element,
                                new DimensionSelectionFilter(),
                                targetPrompt);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            // ESC or right-click cancel → exit loop
                            break;
                        }

                        Dimension targetDim = doc.GetElement(targetRef) as Dimension;
                        if (targetDim == null)
                            continue;

                        using (Transaction t = new Transaction(doc, "Paste Dimension Text"))
                        {
                            t.Start();
                            ApplyDimensionText(
                                targetDim,
                                textAbove,
                                textBelow,
                                textPrefix,
                                textSuffix,
                                copyValueOverride,
                                valueOverride);
                            t.Commit();
                        }

                        pastedCount++;
                    }

                    if (pastedCount > 0)
                        group.Assimilate();
                    else
                        group.RollBack();
                }

                // 3) Behaviour after ESC
                if (pastedCount == 0)
                {
                    DialogHelper.ShowError("Copy Dim Text", "Source captured, but no target dimensions were selected.");
                    return Result.Cancelled;
                }

                // No final success popup (as per your style) – just silent success
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Cancel while picking SOURCE → tool cancelled
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static void ApplyDimensionText(
            Dimension targetDim,
            string textAbove,
            string textBelow,
            string textPrefix,
            string textSuffix,
            bool copyValueOverride,
            string valueOverride)
        {
            targetDim.Above = textAbove ?? string.Empty;
            targetDim.Below = textBelow ?? string.Empty;
            targetDim.Prefix = textPrefix ?? string.Empty;
            targetDim.Suffix = textSuffix ?? string.Empty;

            if (copyValueOverride)
            {
                targetDim.ValueOverride = valueOverride;
            }
        }
    }
}
