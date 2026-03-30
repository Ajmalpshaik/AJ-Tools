using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.GraphicsTools;
using AJTools.Models.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Clears category overrides in the active view for unique model categories from selected elements.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetCategoryGraphics : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Reset Category Graphics";

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
                    new ModelCategorySelectionFilter(),
                    "Select model elements to reset category graphics in the active view.");

                if (selection.WasCancelled)
                {
                    return Result.Cancelled;
                }

                if (selection.ElementIds.Count == 0)
                {
                    TaskDialog.Show(dialogTitle, "No elements were selected.");
                    return Result.Cancelled;
                }

                IList<Category> categories = GraphicsCategoryService.GetUniqueCategoriesFromElements(
                    doc,
                    activeView,
                    selection.ElementIds,
                    includeAnnotationCategories: false);

                if (categories.Count == 0)
                {
                    TaskDialog.Show(dialogTitle, "No valid model categories were found in the selection.");
                    return Result.Cancelled;
                }

                GraphicsOperationSummary summary;
                using (var transaction = new Transaction(doc, "AJ Tools - Reset Category Graphics"))
                {
                    transaction.Start();
                    summary = GraphicsCategoryService.ApplyOverrides(
                        activeView,
                        categories,
                        new OverrideGraphicSettings(),
                        includeAnnotationCategories: false);

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
                    TaskDialog.Show(dialogTitle, "No category overrides were reset.");
                    return Result.Cancelled;
                }

                string resultMessage = $"Reset graphics for {summary.Applied} category(s).";
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

