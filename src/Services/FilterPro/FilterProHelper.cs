#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterProHelper.cs
 * Purpose       : Orchestrates Filter Pro operations â€” validates categories, delegates to
 *                 FilterCreator and FilterApplier/FilterReorderer, and returns affected count.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, System.Linq
 *
 * Input         : Document, target views, FilterSelection (caller owns the transaction scope)
 * Output        : Integer count of filters created or updated; skipped reasons logged to caller list
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Caller must own the transaction scope â€” this class makes no transactions of its own.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; fixed silent catch in ValidateCategories;
 *                        replaced O(nÂ²) dedup loop with O(1) HashSet lookup.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
            var processedIdSet = new HashSet<int>();

            if (creationResult.ProcessedFilterIds != null)
            {
                foreach (var id in creationResult.ProcessedFilterIds)
                {
                    if (id != null &&
                        id != ElementId.InvalidElementId &&
                        processedIdSet.Add(AJTools.Utils.ElementIdHelper.GetIntegerValue(id)))
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
                    catch (Exception ex)
                    {
                        skipped?.Add($"Category ID {AJTools.Utils.ElementIdHelper.GetIntegerValue(catId)}: skipped â€” {ex.Message}");
                    }
                }
            }

            if (validCategoryIds.Count == 0)
                skipped?.Add("No valid categories found for parameter filter.");

            return validCategoryIds;
        }
    }
}
