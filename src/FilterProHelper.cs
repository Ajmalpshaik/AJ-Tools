using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    /// <summary>
    /// Helper class for creating and managing Revit filters.
    /// V4: Fixed "V/G Dialog" crash by cloning Graphic Overrides.
    /// </summary>
    internal static class FilterProHelper
    {
        public static int CreateFilters(Document doc, IEnumerable<View> targetViews, FilterSelection selection, IList<string> skipped)
        {
            // 0. Safety: Ensure document is up to date
            doc.Regenerate();

            // 1. Validate inputs
            var viewTargets = (selection?.ApplyToView == true && targetViews != null)
                ? targetViews.Where(v => v != null && !v.IsTemplate).ToList()
                : new List<View>();

            var newFilterIds = new List<ElementId>();
            int created = 0;

            // 2. Validate Categories 
            var validCategoryIds = new List<ElementId>();
            foreach (var catId in selection.CategoryIds)
            {
                if (Category.GetCategory(doc, catId) != null)
                    validCategoryIds.Add(catId);
            }
            if (validCategoryIds.Count == 0)
            {
                skipped.Add("No valid categories found.");
                return 0;
            }

            // 3. Create or Update Filters
            foreach (FilterValueItem value in selection.Values)
            {
                try
                {
                    IList<FilterRule> rules = BuildRules(selection.Parameter, value, selection.RuleType);
                    if (rules == null || rules.Count == 0)
                    {
                        skipped.Add($"{selection.Parameter.Name} (Rule not supported)");
                        continue;
                    }

                    string filterName = ComposeFilterName(selection, value, doc);
                    
                    ParameterFilterElement existing = FindFilterByName(doc, filterName);
                    ElementParameterFilter elementFilter = new ElementParameterFilter(rules);
                    ParameterFilterElement filter = existing;

                    if (filter == null)
                    {
                        filter = ParameterFilterElement.Create(doc, filterName, validCategoryIds, elementFilter);
                        created++;
                        newFilterIds.Add(filter.Id);
                    }
                    else
                    {
                        if (selection.OverrideExisting)
                        {
                            if (doc.GetElement(filter.Id) == null)
                            {
                                filter = ParameterFilterElement.Create(doc, filterName, validCategoryIds, elementFilter);
                                created++;
                            }
                            else
                            {
                                filter.Name = filterName;
                                filter.SetCategories(validCategoryIds);
                                filter.SetElementFilter(elementFilter);
                                created++;
                            }
                        }
                        newFilterIds.Add(filter.Id);
                    }

                    // Apply to View (Initial)
                    // If Reordering is ON, skip this to avoid double processing
                    if (selection.ApplyToView && viewTargets.Any() && !selection.PlaceNewFiltersFirst)
                    {
                        foreach (var view in viewTargets)
                        {
                            ApplyToView(doc, view, filter.Id, selection);
                        }
                    }
                }
                catch (Exception ex)
                {
                    skipped.Add($"Error creating filter: {ex.Message}");
                }
            }

            // 4. Handle Reordering (CRITICAL FIX APPLIED HERE)
            if (selection.PlaceNewFiltersFirst && newFilterIds.Any() && viewTargets.Any())
            {
                doc.Regenerate(); // Clear any UI caching
                foreach (var view in viewTargets)
                {
                    ReorderFiltersInView(doc, view, newFilterIds, selection);
                }
            }

            return created;
        }

        private static void ReorderFiltersInView(Document doc, View view, List<ElementId> newFilterIds, FilterSelection selection)
        {
            try
            {
                if (IsViewControlledByTemplate(view)) return;

                var rawFilterIds = view.GetFilters();
                if (rawFilterIds == null) return;

                var existingFilters = new List<ElementId>();
                foreach (var id in rawFilterIds)
                {
                    if (doc.GetElement(id) != null) existingFilters.Add(id);
                }

                if (existingFilters.Count == 0 && newFilterIds.Count == 0) return;

                // --- CHECK ORDER BEFORE TOUCHING ANYTHING ---
                var newFilterSet = new HashSet<int>(newFilterIds.Select(x => x.IntegerValue));
                var otherFilters = new List<ElementId>();

                foreach (ElementId id in existingFilters)
                {
                    if (!newFilterSet.Contains(id.IntegerValue)) otherFilters.Add(id);
                }

                var desiredOrder = new List<ElementId>();
                desiredOrder.AddRange(newFilterIds);
                desiredOrder.AddRange(otherFilters);

                bool orderIsCorrect = true;
                if (existingFilters.Count != desiredOrder.Count)
                {
                    orderIsCorrect = false;
                }
                else
                {
                    int count = 0;
                    foreach (ElementId id in existingFilters)
                    {
                        if (count < desiredOrder.Count && id == desiredOrder[count]) count++;
                        else
                        {
                            orderIsCorrect = false;
                            break;
                        }
                    }
                }

                if (orderIsCorrect)
                {
                    foreach (var id in newFilterIds) ApplyGraphicsToFilter(doc, view, id, selection);
                    return;
                }

                // --- CRITICAL FIX: CLONE THE SETTINGS ---
                // We cannot hold the reference returned by GetFilterOverrides because RemoveFilter kills it.
                // We must use the Copy Constructor to create a detached clone.
                
                var overridesMap = new Dictionary<ElementId, OverrideGraphicSettings>();
                foreach (ElementId id in existingFilters)
                {
                    try 
                    { 
                        if(doc.GetElement(id) != null)
                        {
                            OverrideGraphicSettings liveSettings = view.GetFilterOverrides(id);
                            // THIS IS THE FIX: Create a new object copy
                            OverrideGraphicSettings clonedSettings = new OverrideGraphicSettings(liveSettings);
                            overridesMap[id] = clonedSettings;
                        }
                    } 
                    catch { }
                }

                // Remove Filters
                foreach (ElementId id in existingFilters)
                {
                    try { if (doc.GetElement(id) != null) view.RemoveFilter(id); } catch { }
                }

                // Add Back
                foreach (ElementId id in desiredOrder)
                {
                    try
                    {
                        if (doc.GetElement(id) == null) continue;

                        view.AddFilter(id);

                        if (newFilterSet.Contains(id.IntegerValue))
                        {
                            ApplyGraphicsToFilter(doc, view, id, selection);
                        }
                        else if (overridesMap.ContainsKey(id))
                        {
                            // Apply the CLONED settings
                            view.SetFilterOverrides(id, overridesMap[id]);
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                // Swallow reordering errors to keep Revit alive
            }
        }

        private static void ApplyToView(Document doc, View view, ElementId filterId, FilterSelection selection)
        {
            if (view == null || filterId == ElementId.InvalidElementId) return;
            if (IsViewControlledByTemplate(view)) return;
            if (doc.GetElement(filterId) == null) return;

            if (!view.GetFilters().Contains(filterId))
            {
                try { view.AddFilter(filterId); } catch { return; }
            }

            ApplyGraphicsToFilter(doc, view, filterId, selection);
        }

        private static void ApplyGraphicsToFilter(Document doc, View view, ElementId filterId, FilterSelection selection)
        {
            if (!selection.ApplyGraphics) return;

            OverrideGraphicSettings ogs = null;
            try { ogs = view.GetFilterOverrides(filterId); } catch { }
            
            // Safety: Clone it if it exists, or create new
            if (ogs != null)
                ogs = new OverrideGraphicSettings(ogs);
            else
                ogs = new OverrideGraphicSettings();

            bool applyProjLines = selection.ColorProjectionLines;
            bool applyProjPatterns = selection.ColorProjectionPatterns;
            bool applyCutLines = selection.ColorCutLines;
            bool applyCutPatterns = selection.ColorCutPatterns;
            bool applyHalftone = selection.ColorHalftone;

            if (!applyProjLines && !applyProjPatterns && !applyCutLines && !applyCutPatterns && !applyHalftone)
            {
                applyProjLines = true;
                applyCutLines = true;
            }

            Color chosenColor = GetColor(selection, filterId);
            ElementId patternId = ResolvePatternId(doc, selection.PatternId);

            if (applyProjLines) ogs.SetProjectionLineColor(chosenColor);
            if (applyCutLines) ogs.SetCutLineColor(chosenColor);
            
            if (applyProjPatterns)
            {
                if (patternId != ElementId.InvalidElementId)
                    ogs.SetSurfaceForegroundPatternId(patternId);
                ogs.SetSurfaceForegroundPatternColor(chosenColor);
            }
            
            if (applyCutPatterns)
            {
                if (patternId != ElementId.InvalidElementId)
                    ogs.SetCutForegroundPatternId(patternId);
                ogs.SetCutForegroundPatternColor(chosenColor);
            }
            
            if (applyHalftone) ogs.SetHalftone(true);

            try { view.SetFilterOverrides(filterId, ogs); } catch { }
        }

        private static bool IsViewControlledByTemplate(View view)
        {
            return view.ViewTemplateId != ElementId.InvalidElementId; 
        }

        private static Color GetColor(FilterSelection selection, ElementId filterId)
        {
            return selection.RandomColors ? ColorPalette.GetRandomColor() : ColorPalette.GetColorFor(filterId);
        }

        private static ElementId ResolvePatternId(Document doc, ElementId requested)
        {
            if (requested != null && requested != ElementId.InvalidElementId)
                if (doc.GetElement(requested) != null)
                    return requested;

            return GetSolidFillId(doc);
        }

        private static ElementId GetSolidFillId(Document doc)
        {
            try
            {
                var solidPattern = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);
                return solidPattern != null ? solidPattern.Id : ElementId.InvalidElementId;
            }
            catch { return ElementId.InvalidElementId; }
        }

        private static IList<FilterRule> BuildRules(FilterParameterItem parameter, FilterValueItem value, string ruleType)
        {
            const bool caseSensitive = false;
            if (parameter == null) return null;

            // Composite "Family and Type"
            if (value.RawValue is Tuple<string, string> familyAndType)
            {
                return new List<FilterRule>
                {
                    ParameterFilterRuleFactory.CreateEqualsRule(new ElementId(BuiltInParameter.ALL_MODEL_FAMILY_NAME), familyAndType.Item1, caseSensitive),
                    ParameterFilterRuleFactory.CreateEqualsRule(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_NAME), familyAndType.Item2, caseSensitive)
                };
            }

            var single_rules = new List<FilterRule>();
            ElementId paramId = parameter.Id;

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    string text = value.RawValue as string ?? value.Display ?? string.Empty;
                    switch (ruleType)
                    {
                        case RuleTypes.Equals: single_rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, text, caseSensitive)); break;
                        case RuleTypes.Contains: single_rules.Add(ParameterFilterRuleFactory.CreateContainsRule(paramId, text, caseSensitive)); break;
                        case RuleTypes.BeginsWith: single_rules.Add(ParameterFilterRuleFactory.CreateBeginsWithRule(paramId, text, caseSensitive)); break;
                        case RuleTypes.EndsWith: single_rules.Add(ParameterFilterRuleFactory.CreateEndsWithRule(paramId, text, caseSensitive)); break;
                        case RuleTypes.NotEquals: single_rules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, text, caseSensitive)); break;
                        case RuleTypes.NotContains: single_rules.Add(ParameterFilterRuleFactory.CreateNotContainsRule(paramId, text, caseSensitive)); break;
                        case RuleTypes.NotBeginsWith: single_rules.Add(ParameterFilterRuleFactory.CreateNotBeginsWithRule(paramId, text, caseSensitive)); break;
                        case RuleTypes.NotEndsWith: single_rules.Add(ParameterFilterRuleFactory.CreateNotEndsWithRule(paramId, text, caseSensitive)); break;
                        case RuleTypes.HasValue: single_rules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId)); break;
                        case RuleTypes.HasNoValue: single_rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, string.Empty, caseSensitive)); break;
                    }
                    break;

                case StorageType.ElementId:
                    ElementId id = value.ElementId ?? value.RawValue as ElementId ?? ElementId.InvalidElementId;
                    if (id == ElementId.InvalidElementId) return null;
                    if (ruleType == RuleTypes.NotEquals) single_rules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, id));
                    else if (ruleType == RuleTypes.HasValue) single_rules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    else if (ruleType == RuleTypes.HasNoValue) single_rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, ElementId.InvalidElementId));
                    else single_rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, id));
                    break;

                case StorageType.Integer:
                    int intVal = Convert.ToInt32(value.RawValue);
                    if (ruleType == RuleTypes.NotEquals) single_rules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, intVal));
                    else if (ruleType == RuleTypes.Greater) single_rules.Add(ParameterFilterRuleFactory.CreateGreaterRule(paramId, intVal));
                    else if (ruleType == RuleTypes.GreaterOrEqual) single_rules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, intVal));
                    else if (ruleType == RuleTypes.Less) single_rules.Add(ParameterFilterRuleFactory.CreateLessRule(paramId, intVal));
                    else if (ruleType == RuleTypes.LessOrEqual) single_rules.Add(ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, intVal));
                    else if (ruleType == RuleTypes.HasValue) single_rules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    else if (ruleType == RuleTypes.HasNoValue) single_rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, 0));
                    else single_rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, intVal));
                    break;

                case StorageType.Double:
                    double dblVal = Convert.ToDouble(value.RawValue);
                    const double tolerance = 1e-6;
                    if (ruleType == RuleTypes.NotEquals) single_rules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, dblVal, tolerance));
                    else if (ruleType == RuleTypes.Greater) single_rules.Add(ParameterFilterRuleFactory.CreateGreaterRule(paramId, dblVal, tolerance));
                    else if (ruleType == RuleTypes.GreaterOrEqual) single_rules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, dblVal, tolerance));
                    else if (ruleType == RuleTypes.Less) single_rules.Add(ParameterFilterRuleFactory.CreateLessRule(paramId, dblVal, tolerance));
                    else if (ruleType == RuleTypes.LessOrEqual) single_rules.Add(ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, dblVal, tolerance));
                    else if (ruleType == RuleTypes.HasValue) single_rules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    else if (ruleType == RuleTypes.HasNoValue) single_rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, 0.0, tolerance));
                    else single_rules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, dblVal, tolerance));
                    break;
            }

            return single_rules;
        }

        private static string ComposeFilterName(FilterSelection selection, FilterValueItem value, Document doc)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(selection.Prefix)) parts.Add(selection.Prefix.Trim());

            if (selection.IncludeCategory)
            {
                string catLabel = ResolveCategoryLabel(selection.CategoryIds, doc);
                if (!string.IsNullOrWhiteSpace(catLabel)) parts.Add(catLabel);
            }

            if (selection.IncludeParameter) parts.Add(selection.Parameter.Name);

            parts.Add(value.Display ?? "Value");

            string name = string.Join(" - ", parts);
            if (!string.IsNullOrWhiteSpace(selection.Suffix)) name += " " + selection.Suffix.Trim();

            return SanitizeName(name);
        }

        private static string ResolveCategoryLabel(IEnumerable<ElementId> categoryIds, Document doc)
        {
            if (categoryIds == null) return string.Empty;

            var names = new List<string>();
            foreach (ElementId catId in categoryIds.Take(3))
            {
                Category cat = Category.GetCategory(doc, catId);
                if (cat != null) names.Add(cat.Name);
            }

            if (!names.Any()) return string.Empty;
            return categoryIds.Count() > 3 ? names[0] + " +" : string.Join(", ", names);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Filter";
            char[] invalid = { '<', '>', '{', '}', '[', ']', '|', ';', ':', '\\', '/', '?', '*', '"' };
            foreach (char c in invalid) name = name.Replace(c, '_');
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
