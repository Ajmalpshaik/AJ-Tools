// Tool Name: Filter Pro - Helper
// Description: Utility helpers for filter creation, naming, and parameter handling.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Linq
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.Services.FilterPro
{
    /// <summary>
    /// Orchestrates FilterPro operations: validation, creation/update, applying, and ordering.
    /// Caller must own the transaction scope.
    /// </summary>
    internal static class FilterProHelper
    {
        public static int CreateFilters(Document doc,
                                        IEnumerable<View> targetViews,
                                        FilterSelection selection,
                                        IList<string> skipped)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

#if DEBUG
            if (!doc.IsModifiable)
                throw new InvalidOperationException(
                    "FilterProHelper.CreateFilters must be called inside an open Transaction or TransactionGroup.");
#endif

            if (selection == null)
            {
                skipped?.Add("Selection is null.");
                return 0;
            }

            if (selection.Parameter == null)
            {
                skipped?.Add("Parameter is not defined in selection.");
                return 0;
            }

            if (selection.Values == null || !selection.Values.Any())
            {
                skipped?.Add("No values provided for filter creation.");
                return 0;
            }

            var viewTargets = (selection.ApplyToView && targetViews != null)
                ? targetViews.Where(v => v != null).ToList()
                : new List<View>();

            var validCategoryIds = ValidateCategories(doc, selection.CategoryIds, skipped);
            if (validCategoryIds.Count == 0)
                return 0;

            var creationResult = FilterCreator.CreateOrUpdateFilters(doc, selection, validCategoryIds, skipped);
            if (creationResult == null)
            {
                skipped?.Add("No filters were created or updated.");
                return 0;
            }

            var processedFilterIds = new List<ElementId>();
            if (creationResult.ProcessedFilterIds != null && creationResult.ProcessedFilterIds.Any())
            {
                foreach (var id in creationResult.ProcessedFilterIds)
                {
                    if (id != null &&
                        id != ElementId.InvalidElementId &&
                        !processedFilterIds.Any(x => x.IntegerValue == id.IntegerValue))
                    {
                        processedFilterIds.Add(id);
                    }
                }
            }

            if (!selection.ApplyToView ||
                !viewTargets.Any() ||
                !processedFilterIds.Any())
            {
                return creationResult.TotalAffected;
            }

            var solidFillId = FilterApplier.GetSolidFillId(doc);

            if (!selection.PlaceNewFiltersFirst)
            {
                foreach (var view in viewTargets)
                {
                    foreach (var filterId in processedFilterIds)
                    {
                        FilterApplier.ApplyToView(doc, view, filterId, selection, solidFillId, skipped);
                    }
                }
            }
            else
            {
                foreach (var view in viewTargets)
                {
                    FilterReorderer.ReorderFiltersInView(doc, view, processedFilterIds, selection, solidFillId, skipped);
                }
            }

            return creationResult.TotalAffected;
        }

        private static List<ElementId> ValidateCategories(Document doc,
                                                          IEnumerable<ElementId> categoryIds,
                                                          IList<string> skipped)
        {
            var validCategoryIds = new List<ElementId>();

            if (categoryIds != null)
            {
                foreach (var catId in categoryIds)
                {
                    try
                    {
                        var cat = Category.GetCategory(doc, catId);
                        // In Revit 2020, use presence of category only. IsCategoryValidForParameterFilter is not available.
                        if (cat != null)
                        {
                            validCategoryIds.Add(catId);
                        }
                    }
                    catch
                    {
                        // ignore bad category ids
                    }
                }
            }

            if (validCategoryIds.Count == 0)
                skipped?.Add("No valid categories found for parameter filter.");

            return validCategoryIds;
        }
    }
}
