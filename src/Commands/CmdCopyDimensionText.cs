// Tool Name: Copy Dimension Text
// Description: Copies dimension text properties between selected dimensions.
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

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

                // 2) Loop: pick TARGET dimensions until ESC
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
