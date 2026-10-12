#region Metadata
/*
 * Tool Name     : Copy Dimension Text
 * File Name     : CmdCopyDimensionText.cs
 * Purpose       : Copies the Above / Below / Prefix / Suffix text and value override from one picked
 *                 source dimension onto one or more picked target dimensions.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.1
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-10-12
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
 * - Single-segment dimensions only: the Dimension text properties are documented for one segment,
 *   and a multi-segment string keeps its text per segment. A multi-segment source is refused; a
 *   multi-segment target is skipped (session kept) and reported at the end.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.1.0 (2025-12-10) - Source/target pick loop with value-override copy.
 * v1.2.0 (2026-07-01) - Refactor/audit: full metadata block; whole pick session now assimilated into a
 *                       single undo step. Copy behaviour unchanged.
 * v1.2.1 (2026-10-12) - Multi-segment dimensions are refused plainly: a multi-segment source was read
 *                       through the single-segment properties (reported as "no text to copy"), and a
 *                       target Revit refused ended the session as Failed, undoing every earlier paste.
 *                       Each target now runs on its own (a refused one is rolled back alone), and
 *                       skipped/refused picks are reported at the end.
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

                // Above/Below/Prefix/Suffix/ValueOverride on Dimension are documented for
                // single-segment dimensions only; a multi-segment string keeps its text per segment.
                // Which segment to copy from - or which target segments should receive it - is a
                // choice the tool cannot make for you (one "CLR" note is rarely meant for every
                // segment of a chain), so multi-segment dimensions are refused plainly instead.
                if (IsMultiSegment(sourceDim))
                {
                    DialogHelper.ShowError(
                        "Copy Dim Text",
                        "The picked dimension has more than one segment. This tool copies text between single-segment dimensions only - pick a single-segment dimension as the source.");
                    return Result.Cancelled;
                }

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
                string prompt = targetPrompt;
                int pastedCount = 0;
                int multiSegmentSkipped = 0;
                int failedCount = 0;
                string lastFailure = null;

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
                                prompt);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            // ESC or right-click cancel → exit loop
                            break;
                        }

                        Dimension targetDim = doc.GetElement(targetRef) as Dimension;
                        if (targetDim == null)
                            continue;

                        // Same single-segment rule as the source. Skipping (not failing) keeps the
                        // session and every paste made so far; the status bar says why at once.
                        if (IsMultiSegment(targetDim))
                        {
                            multiSegmentSkipped++;
                            prompt = targetPrompt + " - last pick skipped: it has more than one segment";
                            continue;
                        }

                        // One target that Revit refuses is rolled back on its own. Before, the error
                        // escaped the loop and the whole session - earlier pastes included - was undone.
                        using (Transaction t = new Transaction(doc, "Paste Dimension Text"))
                        {
                            try
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
                                pastedCount++;
                                prompt = targetPrompt;
                            }
                            catch (Exception ex)
                            {
                                if (t.HasStarted() && !t.HasEnded())
                                    t.RollBack();

                                failedCount++;
                                lastFailure = ex.Message;
                                prompt = targetPrompt + " - last pick could not be changed";
                            }
                        }
                    }

                    if (pastedCount > 0)
                        group.Assimilate();
                    else
                        group.RollBack();
                }

                string leftOut = DescribeLeftOut(multiSegmentSkipped, failedCount, lastFailure);

                // 3) Behaviour after ESC
                if (pastedCount == 0)
                {
                    DialogHelper.ShowError(
                        "Copy Dim Text",
                        leftOut.Length > 0
                            ? "No dimensions were changed." + leftOut
                            : "Source captured, but no target dimensions were selected.");
                    return Result.Cancelled;
                }

                // Silent success - unless some picks were left out, which must be said.
                if (leftOut.Length > 0)
                {
                    DialogHelper.ShowInfo(
                        "Copy Dim Text",
                        $"Text copied to {pastedCount} dimension(s)." + leftOut);
                }

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

        /// <summary>
        /// A dimension string with more than one segment. Single-segment dimensions report 0 or 1.
        /// </summary>
        private static bool IsMultiSegment(Dimension dimension)
        {
            return dimension != null && dimension.NumberOfSegments > 1;
        }

        /// <summary>
        /// Plain lines for the picks that were not changed; empty when every pick was pasted.
        /// </summary>
        private static string DescribeLeftOut(int multiSegmentSkipped, int failedCount, string lastFailure)
        {
            string text = string.Empty;

            if (multiSegmentSkipped > 0)
            {
                text += $"\n\nSkipped {multiSegmentSkipped} dimension(s) with more than one segment - this tool works on single-segment dimensions only.";
            }

            if (failedCount > 0)
            {
                text += $"\n\n{failedCount} dimension(s) could not be changed"
                    + (string.IsNullOrWhiteSpace(lastFailure) ? "." : ": " + lastFailure);
            }

            return text;
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
