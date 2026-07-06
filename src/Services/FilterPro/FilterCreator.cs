#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterCreator.cs
 * Purpose       : Creates and updates ParameterFilterElements — builds filter rules by storage type,
 *                 composes and sanitises filter names, and ensures name uniqueness in the project.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-02
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, System.Linq
 *
 * Input         : FilterSelection (categories, parameter, values, rule type, naming options)
 * Output        : ParameterFilterElements created or updated in the Revit document
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - ParameterFilterRuleFactory string overloads with caseSensitive bool are confirmed valid for 2020-2026.
 * - ElementId.IntegerValue is deprecated in Revit 2024+ (replaced by ElementId.Value returning long).
 *   Current usage is safe on all versions; upgrading to .Value requires a #if REVIT2024 guard.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.1.0 (2026-05-25) - Optimised name-uniqueness check from O(n²) DB queries to O(1) dictionary
 *                        lookups; added HashSet dedup for processed filter IDs; removed dead fallback method.
 * v1.1.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 * v1.2.0 (2026-07-02) - Extracted rule-building logic (BuildRules + storage-type helpers) into the
 *                        new shared FilterRuleBuilder so the Colorize tool can reuse the same rule
 *                        engine without duplicating it. No behavior change.
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
    internal class FilterCreationResult
    {
        public List<ElementId> ProcessedFilterIds { get; } = new List<ElementId>();
        public int Created { get; set; }
        public int Updated { get; set; }
        public int TotalAffected => Created + Updated;
    }

    internal static class FilterCreator
    {
        private const int MaxFilterNameLength = 120;

        internal static FilterCreationResult CreateOrUpdateFilters(
            Document doc,
            FilterSelection selection,
            IList<ElementId> validCategoryIds,
            IList<string> skipped)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            var result = new FilterCreationResult();

            if (selection == null)
            {
                skipped?.Add("No selection provided for filter creation.");
                return result;
            }

            if (selection.Values == null || selection.Values.Count == 0)
            {
                skipped?.Add("No values selected for filter creation.");
                return result;
            }

            if (validCategoryIds == null || validCategoryIds.Count == 0)
            {
                skipped?.Add("No valid categories supplied for filter creation.");
                return result;
            }

            // Pre-collect all existing filter names ONCE to avoid repeated DB queries in the loop.
            // This reduces EnsureUniqueFilterName from O(n^2) DB queries to O(1) dictionary lookups.
            var existingFilterNames = new Dictionary<string, ParameterFilterElement>(StringComparer.OrdinalIgnoreCase);
            foreach (ParameterFilterElement pfe in new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>())
            {
                if (!existingFilterNames.ContainsKey(pfe.Name))
                    existingFilterNames[pfe.Name] = pfe;
            }

            // Track processed filter IDs using a HashSet for O(1) dedup lookups.
            var processedIds = new HashSet<int>();

            foreach (FilterValueItem value in selection.Values)
            {
                if (value == null)
                    continue;

                try
                {
                    var rules = FilterRuleBuilder.BuildRules(selection.Parameter, value, selection.RuleType, selection.CaseSensitive, skipped);
                    if (rules == null || rules.Count == 0)
                    {
                        if (selection.Parameter != null)
                        {
                            skipped?.Add(
                                $"Filter rules not supported for parameter '{selection.Parameter.Name}' and value '{value.Display}'.");
                        }
                        else
                        {
                            skipped?.Add(
                                $"Filter rules not supported because no parameter was selected for value '{value.Display}'.");
                        }

                        continue;
                    }

                    string filterName = ComposeFilterName(selection, value, doc);
                    filterName = EnsureUniqueFilterName(filterName, existingFilterNames, selection.OverrideExisting);

                    existingFilterNames.TryGetValue(filterName, out ParameterFilterElement existing);
                    ElementParameterFilter elementFilter = new ElementParameterFilter(rules);
                    ParameterFilterElement filter = existing;

                    bool createdNew = false;
                    bool modifiedExisting = false;

                    if (filter == null)
                    {
                        if (selection.RandomColors)
                        {
                            // Randomize path should not create new filters; skip missing ones.
                            skipped?.Add(
                                $"Filter '{filterName}' not found; randomize colors only updates existing filters.");
                            continue;
                        }

                        filter = ParameterFilterElement.Create(doc, filterName, validCategoryIds, elementFilter);
                        // Register the newly created filter in the in-memory name map to keep it consistent.
                        existingFilterNames[filter.Name] = filter;
                        createdNew = true;
                        result.Created++;
                    }
                    else if (selection.OverrideExisting)
                    {
                        filter.Name = filterName;
                        filter.SetCategories(validCategoryIds);
                        filter.SetElementFilter(elementFilter);
                        modifiedExisting = true;
                        result.Updated++;
                    }

                    if (createdNew || modifiedExisting)
                    {
                        // O(1) dedup using HashSet — avoids O(n) linear scan from previous version.
                        if (processedIds.Add(filter.Id.IntegerValue))
                            result.ProcessedFilterIds.Add(filter.Id);
                    }
                }
                catch (Exception ex)
                {
                    skipped?.Add(
                        $"Error creating/updating filter for value '{value.Display}': {ex.Message}");
                }
            }

            return result;
        }

        internal static string ComposeFilterName(
            FilterSelection selection,
            FilterValueItem value,
            Document doc)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(selection.Prefix))
                parts.Add(selection.Prefix.Trim());

            if (selection.IncludeCategory)
            {
                string catLabel = ResolveCategoryLabel(selection.CategoryIds, doc);
                if (!string.IsNullOrWhiteSpace(catLabel))
                    parts.Add(catLabel);
            }

            if (selection.IncludeParameter && selection.Parameter != null)
                parts.Add(selection.Parameter.Name);

            parts.Add(value.Display ?? "Value");

            string separator = string.IsNullOrEmpty(selection.Separator)
                ? "_"
                : selection.Separator;

            string core = string.Join(separator, parts);

            if (!string.IsNullOrWhiteSpace(selection.Suffix))
                core += " " + selection.Suffix.Trim();

            return SanitizeName(core);
        }

        private static string ResolveCategoryLabel(IEnumerable<ElementId> categoryIds, Document doc)
        {
            var catList = categoryIds?.ToList() ?? new List<ElementId>();
            if (!catList.Any())
                return string.Empty;

            var names = new List<string>();
            foreach (ElementId catId in catList.Take(3))
            {
                Category cat = Category.GetCategory(doc, catId);
                if (cat != null)
                    names.Add(cat.Name);
            }

            if (!names.Any())
                return string.Empty;

            return catList.Count > 3
                ? names[0] + " +"
                : string.Join(", ", names);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Filter";

            char[] invalid = { '<', '>', '{', '}', '[', ']', '|', ';', ':', '\\', '?', '*', '"' };
            foreach (char c in invalid)
                name = name.Replace(c, '_');

            name = name.Trim();
            if (name.Length > MaxFilterNameLength)
                name = name.Substring(0, MaxFilterNameLength);

            return name;
        }

        /// <summary>
        /// Ensures the filter name is unique using a pre-collected in-memory name map.
        /// Avoids O(n^2) repeated Revit database queries from the previous implementation.
        /// </summary>
        private static string EnsureUniqueFilterName(
            string name,
            Dictionary<string, ParameterFilterElement> existingNames,
            bool overrideExisting)
        {
            if (overrideExisting)
                return name;

            string baseName = name.Length > MaxFilterNameLength
                ? name.Substring(0, MaxFilterNameLength)
                : name;

            if (!existingNames.ContainsKey(baseName))
                return baseName;

            int suffix = 2;
            while (true)
            {
                string suffixText = $" ({suffix++})";
                int maxBaseLength = Math.Max(1, MaxFilterNameLength - suffixText.Length);

                string trimmedBase = baseName.Length > maxBaseLength
                    ? baseName.Substring(0, maxBaseLength)
                    : baseName;

                string candidate = trimmedBase + suffixText;

                if (!existingNames.ContainsKey(candidate))
                    return candidate;
            }
        }

    }
}
