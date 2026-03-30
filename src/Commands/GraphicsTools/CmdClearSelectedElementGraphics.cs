using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.GraphicsTools;
using AJTools.Models.GraphicsTools;
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
            const string dialogTitle = "Clear Selected Element Graphics";

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

                SelectionCaptureResult selection = GraphicsSelectionService.GetPreselectedOrPromptElementIds(
                    uidoc,
                    selectionFilter: null,
                    prompt: "Select elements to clear element graphics in the active view.");

                if (selection.WasCancelled)
                {
                    return Result.Cancelled;
                }

                if (selection.ElementIds.Count == 0)
                {
                    TaskDialog.Show(dialogTitle, "No elements were selected.");
                    return Result.Cancelled;
                }

                GraphicsOperationSummary summary;
                using (var transaction = new Transaction(doc, "AJ Tools - Clear Selected Element Graphics"))
                {
                    transaction.Start();
                    summary = GraphicsElementService.ClearOverrides(doc, activeView, selection.ElementIds);

                    if (summary.HasChanges)
                    {
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.RollBack();
                    }
                }

                if (!summary.HasChanges)
                {
                    TaskDialog.Show(dialogTitle, "No element overrides were cleared.");
                    return Result.Cancelled;
                }

                string resultMessage = $"Cleared graphics for {summary.Applied} element(s).";
                if (summary.Skipped > 0)
                {
                    resultMessage += $"\nSkipped: {summary.Skipped}.";
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

