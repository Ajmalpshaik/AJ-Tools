// Tool Name: Filter Pro - Filter Creator
// Description: Creates and updates parameter filters with naming, ordering, and graphics.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Linq
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using System.Linq;
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

            foreach (FilterValueItem value in selection.Values)
            {
                if (value == null)
                    continue;

                try
                {
                    var rules = BuildRules(selection.Parameter, value, selection.RuleType, selection.CaseSensitive, skipped);
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
                    filterName = EnsureUniqueFilterName(doc, filterName, selection.OverrideExisting);

                    ParameterFilterElement existing = FindFilterByName(doc, filterName);
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
                        // Track once per run, preserving touch order
                        if (!result.ProcessedFilterIds.Any(x => x.IntegerValue == filter.Id.IntegerValue))
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

        private static IList<FilterRule> BuildRules(
            FilterParameterItem parameter,
            FilterValueItem value,
            string ruleType,
            bool caseSensitive,
            IList<string> skipped)
        {
            if (parameter == null)
                return null;

            if (value.RawValue is Tuple<string, string> familyAndType)
            {
                return new List<FilterRule>
                {
                    ParameterFilterRuleFactory.CreateEqualsRule(
                        new ElementId(BuiltInParameter.ALL_MODEL_FAMILY_NAME),
                        familyAndType.Item1,
                        caseSensitive),
                    ParameterFilterRuleFactory.CreateEqualsRule(
                        new ElementId(BuiltInParameter.ALL_MODEL_TYPE_NAME),
                        familyAndType.Item2,
                        caseSensitive)
                };
            }

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return BuildStringRules(parameter.Id, value, ruleType, caseSensitive);
                case StorageType.ElementId:
                    return BuildElementIdRules(parameter.Id, value, ruleType);
                case StorageType.Integer:
                    return BuildIntegerRules(parameter, value, ruleType, skipped);
                case StorageType.Double:
                    return BuildDoubleRules(parameter, value, ruleType, skipped);
                default:
                    return null;
            }
        }

        private static IList<FilterRule> BuildStringRules(
            ElementId paramId,
            FilterValueItem value,
            string ruleType,
            bool caseSensitive)
        {
            string text = value.RawValue as string ?? value.Display ?? string.Empty;
            var singleRules = new List<FilterRule>();

            switch (ruleType)
            {
                case RuleTypes.EqualsRule:
                    singleRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.Contains:
                    singleRules.Add(ParameterFilterRuleFactory.CreateContainsRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.BeginsWith:
                    singleRules.Add(ParameterFilterRuleFactory.CreateBeginsWithRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.EndsWith:
                    singleRules.Add(ParameterFilterRuleFactory.CreateEndsWithRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.NotEquals:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.NotContains:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotContainsRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.NotBeginsWith:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotBeginsWithRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.NotEndsWith:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotEndsWithRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.HasValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    break;
                case RuleTypes.HasNoValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));
                    break;
                default:
                    return null;
            }

            return singleRules;
        }

        private static IList<FilterRule> BuildElementIdRules(
            ElementId paramId,
            FilterValueItem value,
            string ruleType)
        {
            var singleRules = new List<FilterRule>();
            ElementId id = value.ElementId ?? value.RawValue as ElementId ?? ElementId.InvalidElementId;

            if (ruleType == RuleTypes.HasValue)
            {
                singleRules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
            }
            else if (ruleType == RuleTypes.HasNoValue)
            {
                singleRules.Add(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));
            }
            else
            {
                if (id == ElementId.InvalidElementId)
                    return null;

                if (ruleType == RuleTypes.NotEquals)
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, id));
                else
                    singleRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, id));
            }

            return singleRules;
        }

        private static IList<FilterRule> BuildIntegerRules(
            FilterParameterItem parameter,
            FilterValueItem value,
            string ruleType,
            IList<string> skipped)
        {
            var singleRules = new List<FilterRule>();
            ElementId paramId = parameter.Id;

            if (!TryGetInt(value.RawValue, out int intVal) &&
                ruleType != RuleTypes.HasValue &&
                ruleType != RuleTypes.HasNoValue)
            {
                skipped?.Add($"Invalid integer value for parameter '{parameter.Name}'.");
                return null;
            }

            switch (ruleType)
            {
                case RuleTypes.NotEquals:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, intVal));
                    break;
                case RuleTypes.Greater:
                    singleRules.Add(ParameterFilterRuleFactory.CreateGreaterRule(paramId, intVal));
                    break;
                case RuleTypes.GreaterOrEqual:
                    singleRules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, intVal));
                    break;
                case RuleTypes.Less:
                    singleRules.Add(ParameterFilterRuleFactory.CreateLessRule(paramId, intVal));
                    break;
                case RuleTypes.LessOrEqual:
                    singleRules.Add(ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, intVal));
                    break;
                case RuleTypes.HasValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    break;
                case RuleTypes.HasNoValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));
                    break;
                default:
                    singleRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, intVal));
                    break;
            }

            return singleRules;
        }

        private static IList<FilterRule> BuildDoubleRules(
            FilterParameterItem parameter,
            FilterValueItem value,
            string ruleType,
            IList<string> skipped)
        {
            var singleRules = new List<FilterRule>();
            ElementId paramId = parameter.Id;
            const double tolerance = 1e-6;

            if (!TryGetDouble(value.RawValue, out double dblVal) &&
                ruleType != RuleTypes.HasValue &&
                ruleType != RuleTypes.HasNoValue)
            {
                skipped?.Add($"Invalid double value for parameter '{parameter.Name}'.");
                return null;
            }

            switch (ruleType)
            {
                case RuleTypes.NotEquals:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.Greater:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateGreaterRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.GreaterOrEqual:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.Less:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateLessRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.LessOrEqual:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.HasValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    break;
                case RuleTypes.HasNoValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));
                    break;
                default:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateEqualsRule(paramId, dblVal, tolerance));
                    break;
            }

            return singleRules;
        }

        private static bool TryGetInt(object raw, out int value)
        {
            if (raw is int i)
            {
                value = i;
                return true;
            }

            return int.TryParse(raw?.ToString(), out value);
        }

        private static bool TryGetDouble(object raw, out double value)
        {
            if (raw is double d)
            {
                value = d;
                return true;
            }

            return double.TryParse(raw?.ToString(), out value);
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

            string separator = string.IsNullOrWhiteSpace(selection.Separator)
                ? " - "
                : $" {selection.Separator.Trim()} ";

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

            char[] invalid = { '<', '>', '{', '}', '[', ']', '|', ';', ':', '\\', '/', '?', '*', '"' };
            foreach (char c in invalid)
                name = name.Replace(c, '_');

            name = name.Trim();
            if (name.Length > MaxFilterNameLength)
                name = name.Substring(0, MaxFilterNameLength);

            return name;
        }

        private static string EnsureUniqueFilterName(
            Document doc,
            string name,
            bool overrideExisting)
        {
            if (overrideExisting)
                return name;

            string baseName = name.Length > MaxFilterNameLength
                ? name.Substring(0, MaxFilterNameLength)
                : name;

            if (FindFilterByName(doc, baseName) == null)
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

                if (FindFilterByName(doc, candidate) == null)
                    return candidate;
            }
        }

        private static ParameterFilterElement FindFilterByName(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
