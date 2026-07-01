#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : GraphicsCategoryService.cs
 * Purpose       : Collects overridable categories and applies category-level graphics overrides for the active view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Revit view, categories, elements, and override settings.
 * Output        : Category graphics operation summary (attempted / applied / skipped).
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Only categories that the view reports as overridable are written; others are skipped with a count.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Version-safe ElementId access; full metadata block.
 * v1.4.4 (2026-05-09) - Reviewed category collection and override application for release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Services.GraphicsTools
{
    /// <summary>
    /// Category-focused graphics override operations for the active view.
    /// </summary>
    internal static class GraphicsCategoryService
    {
        public static IList<Category> GetUniqueCategoriesFromElements(
            Document doc,
            View view,
            IEnumerable<ElementId> elementIds,
            bool includeAnnotationCategories)
        {
            var result = new Dictionary<int, Category>();

            if (doc == null || view == null || elementIds == null)
            {
                return new List<Category>();
            }

            foreach (ElementId elementId in elementIds)
            {
                if (elementId == null || elementId == ElementId.InvalidElementId)
                {
                    continue;
                }

                Element element = doc.GetElement(elementId);
                Category category = GetCategoryFromElement(element, view, includeAnnotationCategories);
                if (category == null)
                {
                    continue;
                }

                int key = ElementIdHelper.GetIntegerValue(category.Id);
                if (!result.ContainsKey(key))
                {
                    result.Add(key, category);
                }
            }

            return result.Values
                .OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static IList<Category> GetAvailableCategories(
            Document doc,
            View view,
            bool includeAnnotationCategories)
        {
            var result = new Dictionary<int, Category>();
            if (doc == null || view == null)
            {
                return new List<Category>();
            }

            Categories categories = doc.Settings?.Categories;
            if (categories == null)
            {
                return new List<Category>();
            }

            foreach (Category category in categories)
            {
                AddCategoryIfValid(category, view, includeAnnotationCategories, result);
            }

            return result.Values
                .OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static Category GetCategoryFromElement(Element element, View view, bool includeAnnotationCategories)
        {
            Category category = element?.Category;
            return IsCategoryValidForOverride(category, view, includeAnnotationCategories)
                ? category
                : null;
        }

        public static GraphicsOperationSummary ApplyOverrides(
            View view,
            IEnumerable<Category> categories,
            OverrideGraphicSettings settings,
            bool includeAnnotationCategories)
        {
            var summary = new GraphicsOperationSummary();

            if (view == null || categories == null || settings == null)
            {
                return summary;
            }

            var processed = new HashSet<int>();

            foreach (Category category in categories)
            {
                if (category == null || category.Id == null || category.Id == ElementId.InvalidElementId)
                {
                    continue;
                }

                int key = ElementIdHelper.GetIntegerValue(category.Id);
                if (processed.Contains(key))
                {
                    continue;
                }

                processed.Add(key);
                summary.Attempted++;

                if (!IsCategoryValidForOverride(category, view, includeAnnotationCategories))
                {
                    summary.Skipped++;
                    continue;
                }

                try
                {
                    view.SetCategoryOverrides(category.Id, settings);
                    summary.Applied++;
                }
                catch
                {
                    summary.Skipped++;
                }
            }

            return summary;
        }

        private static void AddCategoryIfValid(
            Category category,
            View view,
            bool includeAnnotationCategories,
            IDictionary<int, Category> result)
        {
            if (category == null || result == null)
            {
                return;
            }

            if (IsCategoryValidForOverride(category, view, includeAnnotationCategories))
            {
                int key = ElementIdHelper.GetIntegerValue(category.Id);
                if (!result.ContainsKey(key))
                {
                    result.Add(key, category);
                }
            }

            CategoryNameMap subCategories = category.SubCategories;
            if (subCategories == null)
            {
                return;
            }

            foreach (Category subCategory in subCategories)
            {
                AddCategoryIfValid(subCategory, view, includeAnnotationCategories, result);
            }
        }

        private static bool IsCategoryValidForOverride(Category category, View view, bool includeAnnotationCategories)
        {
            if (category == null || view == null)
            {
                return false;
            }

            if (category.Id == null || category.Id == ElementId.InvalidElementId)
            {
                return false;
            }

            if (!includeAnnotationCategories && category.CategoryType != CategoryType.Model)
            {
                return false;
            }

            try
            {
                return view.IsCategoryOverridable(category.Id);
            }
            catch
            {
                return false;
            }
        }
    }
}
