using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.GraphicsTools;
using AJTools.Models.GraphicsTools;
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
                    prompt: "Select elements to apply element graphics in the active view.");

                if (selection.WasCancelled)
                {
                    return Result.Cancelled;
                }

                if (selection.ElementIds.Count == 0)
                {
                    TaskDialog.Show(dialogTitle, "No elements were selected.");
                    return Result.Cancelled;
                }

                var settingsWindow = new GraphicsOverrideWindow(doc, "Element Graphics Settings");
                if (settingsWindow.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                OverrideGraphicSettings settings = settingsWindow.SelectedOverrideSettings ?? new OverrideGraphicSettings();

                GraphicsOperationSummary summary;
                using (var transaction = new Transaction(doc, "AJ Tools - Element Graphics"))
                {
                    transaction.Start();
                    summary = GraphicsElementService.ApplyOverrides(doc, activeView, selection.ElementIds, settings);

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
                    TaskDialog.Show(dialogTitle, "No element overrides were applied.");
                    return Result.Cancelled;
                }

                string resultMessage = $"Applied graphics to {summary.Applied} element(s).";
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

