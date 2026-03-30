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
    /// Matches source category overrides to target categories based on picked target elements.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMatchCategoryGraphics : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Match Category Graphics";

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
                    new ModelCategorySelectionFilter(),
                    "Pick SOURCE element to copy category graphics from.",
                    out ElementId sourceElementId,
                    out _))
                {
                    return Result.Cancelled;
                }

                Element sourceElement = doc.GetElement(sourceElementId);
                Category sourceCategory = GraphicsCategoryService.GetCategoryFromElement(
                    sourceElement,
                    activeView,
                    includeAnnotationCategories: false);

                if (sourceCategory == null)
                {
                    TaskDialog.Show(dialogTitle, "Source element does not have a valid overridable model category in this view.");
                    return Result.Cancelled;
                }

                OverrideGraphicSettings sourceSettings = GraphicsOverrideBuilder.Clone(
                    activeView.GetCategoryOverrides(sourceCategory.Id));

                var processedCategoryIds = new HashSet<int>();
                int appliedCount = 0;
                int skippedCount = 0;

                while (true)
                {
                    if (!GraphicsSelectionService.TryPickSingleElementId(
                        uidoc,
                        new ModelCategorySelectionFilter(),
                        "Select TARGET element (ESC to finish).",
                        out ElementId targetElementId,
                        out bool wasCancelled))
                    {
                        if (wasCancelled)
                        {
                            break;
                        }

                        continue;
                    }

                    if (targetElementId == sourceElementId)
                    {
                        skippedCount++;
                        continue;
                    }

                    Element targetElement = doc.GetElement(targetElementId);
                    Category targetCategory = GraphicsCategoryService.GetCategoryFromElement(
                        targetElement,
                        activeView,
                        includeAnnotationCategories: false);

                    if (targetCategory == null ||
                        targetCategory.Id == null ||
                        targetCategory.Id == ElementId.InvalidElementId ||
                        targetCategory.Id.IntegerValue == sourceCategory.Id.IntegerValue)
                    {
                        skippedCount++;
                        continue;
                    }

                    int categoryKey = targetCategory.Id.IntegerValue;
                    if (processedCategoryIds.Contains(categoryKey))
                    {
                        skippedCount++;
                        continue;
                    }

                    using (var transaction = new Transaction(doc, "AJ Tools - Match Category Graphics"))
                    {
                        transaction.Start();
                        try
                        {
                            activeView.SetCategoryOverrides(targetCategory.Id, sourceSettings);
                            transaction.Commit();
                            processedCategoryIds.Add(categoryKey);
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
                    TaskDialog.Show(dialogTitle, "No category overrides were applied.");
                    return Result.Cancelled;
                }

                string resultMessage =
                    $"Source Category: {sourceCategory.Name}" +
                    $"\nUpdated Categories: {appliedCount}";

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

