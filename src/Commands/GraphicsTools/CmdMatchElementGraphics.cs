using System;
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
                UIDocument uidoc = commandData.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocumentAndView(uidoc, out message))
                {
                    TaskDialog.Show(dialogTitle, message);
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                {
                    TaskDialog.Show(dialogTitle, message);
                    return Result.Failed;
                }

                View activeView = doc.ActiveView;

                if (!GraphicsSelectionService.TryPickSingleElementId(
                    uidoc,
                    selectionFilter: null,
                    prompt: "Pick SOURCE element to copy element graphics from.",
                    out ElementId sourceElementId,
                    out _))
                {
                    return Result.Cancelled;
                }

                if (doc.GetElement(sourceElementId) == null)
                {
                    TaskDialog.Show(dialogTitle, "Source element is no longer valid.");
                    return Result.Cancelled;
                }

                OverrideGraphicSettings sourceSettings = GraphicsOverrideBuilder.Clone(
                    activeView.GetElementOverrides(sourceElementId));

                int appliedCount = 0;
                int skippedCount = 0;

                while (true)
                {
                    if (!GraphicsSelectionService.TryPickSingleElementId(
                        uidoc,
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

                    if (targetElementId == sourceElementId || doc.GetElement(targetElementId) == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    using (var transaction = new Transaction(doc, "AJ Tools - Match Element Graphics"))
                    {
                        transaction.Start();
                        try
                        {
                            activeView.SetElementOverrides(targetElementId, sourceSettings);
                            transaction.Commit();
                            appliedCount++;
                        }
                        catch
                        {
                            transaction.RollBack();
                            skippedCount++;
                        }
                    }
                }

                if (appliedCount == 0)
                {
                    TaskDialog.Show(dialogTitle, "No element overrides were applied.");
                    return Result.Cancelled;
                }

                string resultMessage = $"Matched graphics to {appliedCount} element(s).";
                if (skippedCount > 0)
                {
                    resultMessage += $"\nSkipped: {skippedCount}.";
                }

                TaskDialog.Show(dialogTitle, resultMessage);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(dialogTitle, ex.Message);
                return Result.Failed;
            }
        }
    }
}

