using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.GraphicsTools;
using AJTools.Services.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Clears category overrides in the active view for model categories found from all elements visible in that view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetCategoryGraphicsAllElements : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Reset Category Graphics - All Elements";

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

                ICollection<ElementId> allElementIdsInView = new FilteredElementCollector(doc, activeView.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                if (allElementIdsInView.Count == 0)
                {
                    TaskDialog.Show(dialogTitle, "No elements were found in the active view.");
                    return Result.Cancelled;
                }

                IList<Category> categories = GraphicsCategoryService.GetUniqueCategoriesFromElements(
                    doc,
                    activeView,
                    allElementIdsInView,
                    includeAnnotationCategories: false);

                if (categories.Count == 0)
                {
                    TaskDialog.Show(dialogTitle, "No valid model categories were found in the active view.");
                    return Result.Cancelled;
                }

                GraphicsOperationSummary summary;
                using (var transaction = new Transaction(doc, "AJ Tools - Reset Category Graphics (All Elements)"))
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
