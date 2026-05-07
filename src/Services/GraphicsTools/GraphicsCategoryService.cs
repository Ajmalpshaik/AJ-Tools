// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Collects and applies category-level graphics overrides for active views.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.2
// Created      : 2026-03-30
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Revit view, categories, elements, and override settings.
// Output       : Category graphics operation summary.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.2 - Supports the unified Apply Graphics category workflow.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;

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

                int key = category.Id.IntegerValue;
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
            var result = new List<Category>();
            if (doc == null || view == null)
            {
                return result;
            }

            Categories categories = doc.Settings?.Categories;
            if (categories == null)
            {
                return result;
            }

            foreach (Category category in categories)
            {
                if (IsCategoryValidForOverride(category, view, includeAnnotationCategories))
                {
                    result.Add(category);
                }
            }

            return result
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

                int key = category.Id.IntegerValue;
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
                return category.get_AllowsVisibilityControl(view);
            }
            catch
            {
                return false;
            }
        }
    }
}
