#region Metadata
/*
 * Tool Name     : Colorize
 * File Name     : ColorizeApplier.cs
 * Purpose       : Orchestrates the Colorize apply step - for each selected parameter value (or, if
 *                 no parameter is selected, for the selected categories as a whole), finds the
 *                 matching elements in the target view(s) and overrides them directly. No
 *                 ParameterFilterElement is ever created; this is the "colorize, don't filter"
 *                 counterpart to Filter Pro's FilterCreator + FilterApplier.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-13
 * Last Updated  : 2026-07-13
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.FilterPro (FilterApplier.GetSolidFillId only),
 *                 AJTools.Utils (FilterRuleCompat), AJTools.Services.GraphicsTools (GraphicsElementService)
 *
 * Input         : Document, active View, FilterSelection (categories, parameter, values, graphics
 *                 toggles - reusing the exact model FilterProWindow already populates).
 * Output        : GraphicsOperationSummary (attempted/applied/skipped) for the caller's transaction.
 *
 * Notes         :
 * - Colorize always matches with an exact Equals rule (there is no rule-type step in its UI, unlike
 *   Filter Pro), so this owns a small Equals-only rule builder rather than reusing FilterCreator's
 *   private, ruleType-switching BuildRules - duplicating one branch was less risky than widening a
 *   Filter Pro internal's visibility for a single caller.
 * - Filter Pro's current FilterApplier is filter-centric (it mutates a saved ParameterFilterElement's
 *   overrides via view.GetFilterOverrides/SetFilterOverrides) and no longer exposes a reusable
 *   "build an OverrideGraphicSettings from selection+color+pattern" helper, so that piece is owned
 *   here too. GetSolidFillId is still a plain, reusable lookup and IS reused from FilterApplier.
 * - Does not start its own Transaction - the caller wraps this in
 *   GraphicsCommandService.ExecuteSummaryTransaction so the whole Colorize apply (across every
 *   target view) is one undo step.
 * - One color group per selected value, mirroring Filter Pro's "one filter per value" model, but
 *   applied directly to matched elements instead of being saved as separate filters.
 * - ApplyColorizeToViews mirrors Filter Pro's multi-view apply scope (Active View / Selected Views).
 *
 * Changelog     :
 * v1.0.0 (2026-07-13) - Ported from the standalone AJ Tools tree into the live multi-version src/
 *                       project so the Colorize tool actually gets built and deployed (it previously
 *                       existed only in the stale pre-multiversion copy and could never appear on the
 *                       ribbon no matter how many times the add-in was rebuilt). Re-adapted to the
 *                       current Filter Pro/Graphics Tools internals, which changed since the original
 *                       hand-port: rule-building and override-settings construction are now owned
 *                       here directly (see Notes) instead of calling FilterRuleBuilder.BuildRules /
 *                       FilterApplier.BuildOverrideSettings / FilterApplier.HasAnyGraphicsToggleEnabled,
 *                       none of which exist in the live Filter Pro anymore. Colorize's own behaviour
 *                       (category/value matching, per-value palette colour, multi-view apply) is
 *                       unchanged from the original.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models;
using AJTools.Models.GraphicsTools;
using AJTools.Services.FilterPro;
using AJTools.Services.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Services.Colorize
{
    /// <summary>
    /// Applies Colorize overrides directly to matched elements - no saved filter is created.
    /// </summary>
    internal static class ColorizeApplier
    {
        /// <summary>
        /// Applies Colorize to every view in <paramref name="views"/>, merging the results into a
        /// single summary so the caller can commit/roll back the whole multi-view apply as one step.
        /// </summary>
        internal static GraphicsOperationSummary ApplyColorizeToViews(
            Document doc,
            IEnumerable<View> views,
            FilterSelection selection,
            IList<string> skipped)
        {
            var summary = new GraphicsOperationSummary();

            if (doc == null || views == null || selection == null)
            {
                return summary;
            }

            // Colors are resolved ONCE for the whole multi-view apply so the same parameter value gets
            // the same color in every target view, instead of RandomColors rolling a fresh color per view.
            IList<Color> colors = ResolveColors(selection);

            foreach (View view in views)
            {
                if (view == null)
                {
                    continue;
                }

                MergeSummary(summary, ApplyColorize(doc, view, selection, colors, skipped));
            }

            return summary;
        }

        private static IList<Color> ResolveColors(FilterSelection selection)
        {
            var colors = new List<Color>();
            if (selection == null)
            {
                return colors;
            }

            bool hasParameterValues = selection.Parameter != null &&
                                      selection.Values != null &&
                                      selection.Values.Count > 0;

            if (!hasParameterValues)
            {
                colors.Add(selection.RandomColors ? ColorPalette.GetRandomColor() : ColorPalette.GetColorAt(0));
                return colors;
            }

            for (int i = 0; i < selection.Values.Count; i++)
            {
                colors.Add(selection.RandomColors ? ColorPalette.GetRandomColor() : ColorPalette.GetColorAt(i));
            }

            return colors;
        }

        private static GraphicsOperationSummary ApplyColorize(
            Document doc,
            View view,
            FilterSelection selection,
            IList<Color> colors,
            IList<string> skipped)
        {
            var summary = new GraphicsOperationSummary();

            if (doc == null || view == null || selection == null)
            {
                return summary;
            }

            IList<ElementId> categoryIds = selection.CategoryIds;
            if (categoryIds == null || categoryIds.Count == 0)
            {
                skipped?.Add("No categories selected for Colorize.");
                return summary;
            }

            if (!HasAnyGraphicsToggleEnabled(selection))
            {
                skipped?.Add("No graphics options (lines, patterns, or halftone) were enabled.");
                return summary;
            }

            ElementId solidFillId = FilterApplier.GetSolidFillId(doc);
            ElementId patternId = ResolvePatternId(doc, selection.PatternId, solidFillId);

            bool hasParameterValues = selection.Parameter != null &&
                                      selection.Values != null &&
                                      selection.Values.Count > 0;

            if (!hasParameterValues)
            {
                ApplyCategoryOnly(doc, view, selection, categoryIds, patternId, colors, summary, skipped);
                return summary;
            }

            ApplyPerValue(doc, view, selection, categoryIds, patternId, colors, summary, skipped);
            return summary;
        }

        private static void ApplyCategoryOnly(
            Document doc,
            View view,
            FilterSelection selection,
            IList<ElementId> categoryIds,
            ElementId patternId,
            IList<Color> colors,
            GraphicsOperationSummary summary,
            IList<string> skipped)
        {
            ICollection<ElementId> matched = ColorizeElementMatcher.GetMatchingElementIds(doc, view, categoryIds, null);
            if (matched.Count == 0)
            {
                skipped?.Add("No elements matched the selected categories in the active view.");
                return;
            }

            Color color = colors != null && colors.Count > 0 ? colors[0] : ColorPalette.GetColorAt(0);
            OverrideGraphicSettings settings = BuildOverrideSettings(selection, color, patternId);

            MergeSummary(summary, GraphicsElementService.ApplyOverrides(doc, view, matched, settings));
        }

        private static void ApplyPerValue(
            Document doc,
            View view,
            FilterSelection selection,
            IList<ElementId> categoryIds,
            ElementId patternId,
            IList<Color> colors,
            GraphicsOperationSummary summary,
            IList<string> skipped)
        {
            int valueIndex = 0;

            foreach (FilterValueItem value in selection.Values)
            {
                if (value == null)
                {
                    continue;
                }

                IList<FilterRule> rules = BuildEqualsRules(selection.Parameter, value, selection.CaseSensitive);
                if (rules == null || rules.Count == 0)
                {
                    skipped?.Add(
                        $"Filter rule not supported for parameter '{selection.Parameter?.Name}' and value '{value.Display}'.");
                    valueIndex++;
                    continue;
                }

                ICollection<ElementId> matched;
                try
                {
                    matched = ColorizeElementMatcher.GetMatchingElementIds(doc, view, categoryIds, rules);
                }
                catch (System.Exception ex)
                {
                    skipped?.Add($"Error matching elements for value '{value.Display}': {ex.Message}");
                    valueIndex++;
                    continue;
                }

                if (matched.Count == 0)
                {
                    valueIndex++;
                    continue;
                }

                Color color = selection.RandomColors
                    ? ColorPalette.GetRandomColor()
                    : ColorPalette.GetColorAt(valueIndex);

                OverrideGraphicSettings settings = BuildOverrideSettings(selection, color, patternId);

                MergeSummary(summary, GraphicsElementService.ApplyOverrides(doc, view, matched, settings));
                valueIndex++;
            }
        }

        /// <summary>
        /// Builds the Equals rule(s) for the given parameter/value pair. Colorize never exposes a
        /// rule-type choice in its UI, so only the Equals branch of Filter Pro's storage-type switch
        /// is needed here (String/ElementId/Integer/Double). Family+Type returns TWO rules (family
        /// name AND type name) - ColorizeElementMatcher ANDs every rule it's given via a single
        /// ElementParameterFilter, so both must come back together or a "Family + Type" value would
        /// match every element of that family regardless of type.
        /// </summary>
        private static IList<FilterRule> BuildEqualsRules(FilterParameterItem parameter, FilterValueItem value, bool caseSensitive)
        {
            if (parameter == null || value == null)
            {
                return null;
            }

            if (value.RawValue is System.Tuple<string, string> familyAndType)
            {
                return new List<FilterRule>
                {
                    FilterRuleCompat.Equals(new ElementId(BuiltInParameter.ALL_MODEL_FAMILY_NAME), familyAndType.Item1, caseSensitive),
                    FilterRuleCompat.Equals(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_NAME), familyAndType.Item2, caseSensitive)
                };
            }

            FilterRule rule;
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    string text = value.RawValue as string ?? value.Display ?? string.Empty;
                    rule = FilterRuleCompat.Equals(parameter.Id, text, caseSensitive);
                    break;

                case StorageType.ElementId:
                    ElementId id = value.ElementId ?? value.RawValue as ElementId ?? ElementId.InvalidElementId;
                    if (id == ElementId.InvalidElementId)
                    {
                        return null;
                    }
                    rule = ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, id);
                    break;

                case StorageType.Integer:
                    if (!TryGetInt(value.RawValue, out int intVal))
                    {
                        return null;
                    }
                    rule = ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, intVal);
                    break;

                case StorageType.Double:
                    if (!TryGetDouble(value.RawValue, out double dblVal))
                    {
                        return null;
                    }
                    rule = ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, dblVal, 1e-6);
                    break;

                default:
                    return null;
            }

            return new List<FilterRule> { rule };
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

        private static bool HasAnyGraphicsToggleEnabled(FilterSelection selection)
        {
            return selection.ColorProjectionLines || selection.ColorProjectionPatterns ||
                   selection.ColorCutLines || selection.ColorCutPatterns || selection.ColorHalftone;
        }

        private static ElementId ResolvePatternId(Document doc, ElementId requested, ElementId solidFillId)
        {
            try
            {
                if (requested != null && requested != ElementId.InvalidElementId && doc.GetElement(requested) != null)
                {
                    return requested;
                }

                if (solidFillId != null && solidFillId != ElementId.InvalidElementId)
                {
                    return solidFillId;
                }
            }
            catch
            {
                // Ignore lookup issues and fall back to invalid.
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Builds a fresh per-element OverrideGraphicSettings from the selection's graphics toggles -
        /// never clones an existing override, since Shuffle Colors re-randomizes from scratch each click.
        /// </summary>
        private static OverrideGraphicSettings BuildOverrideSettings(FilterSelection selection, Color color, ElementId patternId)
        {
            var settings = new OverrideGraphicSettings();

            if (selection.ColorProjectionLines)
            {
                settings.SetProjectionLineColor(color);
            }

            if (selection.ColorCutLines)
            {
                settings.SetCutLineColor(color);
            }

            if (selection.ColorProjectionPatterns)
            {
                if (patternId != ElementId.InvalidElementId)
                {
                    settings.SetSurfaceForegroundPatternId(patternId);
                }

                settings.SetSurfaceForegroundPatternColor(color);
            }

            if (selection.ColorCutPatterns)
            {
                if (patternId != ElementId.InvalidElementId)
                {
                    settings.SetCutForegroundPatternId(patternId);
                }

                settings.SetCutForegroundPatternColor(color);
            }

            if (selection.ColorHalftone)
            {
                settings.SetHalftone(true);
            }

            return settings;
        }

        private static void MergeSummary(GraphicsOperationSummary target, GraphicsOperationSummary group)
        {
            if (target == null || group == null)
            {
                return;
            }

            target.Attempted += group.Attempted;
            target.Applied += group.Applied;
            target.Skipped += group.Skipped;
        }
    }
}
