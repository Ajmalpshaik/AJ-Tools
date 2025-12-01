using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    /// <summary>
    /// Helper class for creating and managing Revit filters
    /// </summary>
    internal static class FilterProHelper
    {
        public static int CreateFilters(Document doc, IEnumerable<View> targetViews, FilterSelection selection, IList<string> skipped)
        {
            var viewTargets = (selection?.ApplyToView == true && targetViews != null)
                ? targetViews.Where(v => v != null).ToList()
                : new List<View>();
            int created = 0;
            foreach (FilterValueItem value in selection.Values)
            {
                IList<FilterRule> rules = BuildRules(selection.Parameter, value, selection.RuleType);
                if (rules == null || rules.Count == 0)
                {
                    skipped.Add($"{selection.Parameter.Name} (rule not supported)");
                    continue;
                }

                string filterName = ComposeFilterName(selection, value, doc);
                ParameterFilterElement existing = FindFilterByName(doc, filterName);

                if (existing != null && !selection.OverrideExisting)
                {
                    skipped.Add($"{filterName} (already exists)");
                    continue;
                }

                ElementParameterFilter elementFilter = new ElementParameterFilter(rules);
                ParameterFilterElement filter = existing;
                if (filter == null)
                {
                    filter = ParameterFilterElement.Create(doc, filterName, selection.CategoryIds, elementFilter);
                }
                else
                {
                    filter.Name = filterName;
                    filter.SetCategories(selection.CategoryIds);
                    filter.SetElementFilter(elementFilter);
                }

                created++;

                if (selection.ApplyToView && viewTargets.Any())
                {
                    foreach (var view in viewTargets)
                    {
                        ApplyToView(doc, view, filter.Id, selection);
                    }
                }
            }

            return created;
        }

        private static IList<FilterRule> BuildRules(FilterParameterItem parameter, FilterValueItem value, string ruleType)
        {
            const bool caseSensitive = false; // Revit string rules should be case-insensitive for predictable results
            var rules = new List<FilterRule>();
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    string text = value.RawValue as string ?? value.Display ?? string.Empty;
                    switch (ruleType)
                    {
                        case RuleTypes.Equals:
                            rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, text, caseSensitive));
                            break;
                        case RuleTypes.Contains:
                            rules.Add(ParameterFilterRuleFactory.CreateContainsRule(parameter.Id, text, caseSensitive));
                            break;
                        case RuleTypes.BeginsWith:
                            rules.Add(ParameterFilterRuleFactory.CreateBeginsWithRule(parameter.Id, text, caseSensitive));
                            break;
                        case RuleTypes.EndsWith:
                            rules.Add(ParameterFilterRuleFactory.CreateEndsWithRule(parameter.Id, text, caseSensitive));
                            break;
                        case RuleTypes.NotEquals:
                            rules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(parameter.Id, text, caseSensitive));
                            break;
                        case RuleTypes.NotContains:
                            rules.Add(ParameterFilterRuleFactory.CreateNotContainsRule(parameter.Id, text, caseSensitive));
                            break;
                        case RuleTypes.NotBeginsWith:
                            rules.Add(ParameterFilterRuleFactory.CreateNotBeginsWithRule(parameter.Id, text, caseSensitive));
                            break;
                        case RuleTypes.NotEndsWith:
                            rules.Add(ParameterFilterRuleFactory.CreateNotEndsWithRule(parameter.Id, text, caseSensitive));
                            break;
                        case RuleTypes.HasValue:
                            rules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameter.Id));
                            break;
                        case RuleTypes.HasNoValue:
                            rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, string.Empty, caseSensitive));
                            break;
                        default:
                            return null;
                    }
                    break;

                case StorageType.ElementId:
                    ElementId id = value.ElementId ?? value.RawValue as ElementId ?? ElementId.InvalidElementId;
                    if (id == ElementId.InvalidElementId)
                        return null;

                    if (ruleType == RuleTypes.NotEquals)
                        rules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(parameter.Id, id));
                    else if (ruleType == RuleTypes.HasValue)
                        rules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameter.Id));
                    else if (ruleType == RuleTypes.HasNoValue)
                        rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, ElementId.InvalidElementId));
                    else
                        rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, id));
                    break;

                case StorageType.Integer:
                    int intVal = Convert.ToInt32(value.RawValue);
                    if (ruleType == RuleTypes.NotEquals)
                        rules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(parameter.Id, intVal));
                    else if (ruleType == RuleTypes.Greater)
                        rules.Add(ParameterFilterRuleFactory.CreateGreaterRule(parameter.Id, intVal));
                    else if (ruleType == RuleTypes.GreaterOrEqual)
                        rules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameter.Id, intVal));
                    else if (ruleType == RuleTypes.Less)
                        rules.Add(ParameterFilterRuleFactory.CreateLessRule(parameter.Id, intVal));
                    else if (ruleType == RuleTypes.LessOrEqual)
                        rules.Add(ParameterFilterRuleFactory.CreateLessOrEqualRule(parameter.Id, intVal));
                    else if (ruleType == RuleTypes.HasValue)
                        rules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameter.Id));
                    else if (ruleType == RuleTypes.HasNoValue)
                        rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, 0));
                    else
                        rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, intVal));
                    break;

                case StorageType.Double:
                    double dblVal = Convert.ToDouble(value.RawValue);
                    const double tolerance = 1e-6;
                    if (ruleType == RuleTypes.NotEquals)
                        rules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(parameter.Id, dblVal, tolerance));
                    else if (ruleType == RuleTypes.Greater)
                        rules.Add(ParameterFilterRuleFactory.CreateGreaterRule(parameter.Id, dblVal, tolerance));
                    else if (ruleType == RuleTypes.GreaterOrEqual)
                        rules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameter.Id, dblVal, tolerance));
                    else if (ruleType == RuleTypes.Less)
                        rules.Add(ParameterFilterRuleFactory.CreateLessRule(parameter.Id, dblVal, tolerance));
                    else if (ruleType == RuleTypes.LessOrEqual)
                        rules.Add(ParameterFilterRuleFactory.CreateLessOrEqualRule(parameter.Id, dblVal, tolerance));
                    else if (ruleType == RuleTypes.HasValue)
                        rules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameter.Id));
                    else if (ruleType == RuleTypes.HasNoValue)
                        rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, 0.0, tolerance));
                    else
                        rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, dblVal, tolerance));
                    break;

                default:
                    return null;
            }

            return rules;
        }

        private static void ApplyToView(Document doc, View view, ElementId filterId, FilterSelection selection)
        {
            if (view == null)
                return;

            ICollection<ElementId> current = view.GetFilters();
            if (current == null || !current.Contains(filterId))
            {
                try
                {
                    view.AddFilter(filterId);
                }
                catch
                {
                    return;
                }
            }

            OverrideGraphicSettings ogs = view.GetFilterOverrides(filterId);
            if (ogs == null)
                ogs = new OverrideGraphicSettings();

            bool applyProjLines = selection.ColorProjectionLines;
            bool applyProjPatterns = selection.ColorProjectionPatterns;
            bool applyCutLines = selection.ColorCutLines;
            bool applyCutPatterns = selection.ColorCutPatterns;
            bool applyHalftone = selection.ColorHalftone;

            // If nothing was specified, fall back to lines (old behavior)
            if (!applyProjLines && !applyProjPatterns && !applyCutLines && !applyCutPatterns && !applyHalftone)
            {
                applyProjLines = true;
                applyCutLines = true;
            }

            // Only touch the components the user requested
            Color chosenColor = GetColor(selection, filterId);

            if (applyProjLines)
                ogs.SetProjectionLineColor(chosenColor);

            if (applyCutLines)
                ogs.SetCutLineColor(chosenColor);

            if (applyProjPatterns)
            {
                var solidId = GetSolidFillId(doc);
                if (solidId != ElementId.InvalidElementId)
                    ogs.SetSurfaceForegroundPatternId(solidId);
                ogs.SetSurfaceForegroundPatternColor(chosenColor);
            }

            if (applyCutPatterns)
            {
                var solidId = GetSolidFillId(doc);
                if (solidId != ElementId.InvalidElementId)
                    ogs.SetCutForegroundPatternId(solidId);
                ogs.SetCutForegroundPatternColor(chosenColor);
            }

            if (applyHalftone)
                ogs.SetHalftone(true);

            view.SetFilterOverrides(filterId, ogs);
        }

        private static Color GetColor(FilterSelection selection, ElementId filterId)
        {
            return selection.RandomColors ? ColorPalette.GetRandomColor() : ColorPalette.GetColorFor(filterId);
        }

        private static ElementId _solidFillId = ElementId.InvalidElementId;
        private static ElementId GetSolidFillId(Document doc)
        {
            if (_solidFillId != ElementId.InvalidElementId)
                return _solidFillId;

            try
            {
                var fpe = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Drafting, "Solid fill");
                if (fpe == null)
                    fpe = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Model, "Solid fill");
                _solidFillId = fpe != null ? fpe.Id : ElementId.InvalidElementId;
            }
            catch
            {
                _solidFillId = ElementId.InvalidElementId;
            }

            return _solidFillId;
        }

        private static string ComposeFilterName(FilterSelection selection, FilterValueItem value, Document doc)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(selection.Prefix))
                parts.Add(selection.Prefix.Trim());

            if (selection.IncludeCategory)
            {
                string catLabel = ResolveCategoryLabel(selection.CategoryIds, doc);
                if (!string.IsNullOrWhiteSpace(catLabel))
                    parts.Add(catLabel);
            }

            if (selection.IncludeParameter)
                parts.Add(selection.Parameter.Name);

            parts.Add(value.Display ?? "Value");

            string name = string.Join(" - ", parts);
            if (!string.IsNullOrWhiteSpace(selection.Suffix))
                name += " " + selection.Suffix.Trim();

            return SanitizeName(name);
        }

        private static string ResolveCategoryLabel(IEnumerable<ElementId> categoryIds, Document doc)
        {
            if (categoryIds == null)
                return string.Empty;

            var names = new List<string>();
            foreach (ElementId catId in categoryIds.Take(3))
            {
                Category cat = Category.GetCategory(doc, catId);
                if (cat != null)
                    names.Add(cat.Name);
            }

            if (!names.Any())
                return string.Empty;

            return categoryIds.Count() > 3 ? names[0] + " +" : string.Join(", ", names);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Filter";

            char[] invalid = { '<', '>', '{', '}', '[', ']', '|', ';' };
            foreach (char c in invalid)
                name = name.Replace(c, '_');
            return name.Trim();
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
