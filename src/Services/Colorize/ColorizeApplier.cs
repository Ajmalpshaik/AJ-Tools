#region Metadata
/*
 * Tool Name     : Colorize
 * File Name     : ColorizeApplier.cs
 * Purpose       : Orchestrates the Colorize apply step — for each selected parameter value (or, if
 *                 no parameter is selected, for the selected categories as a whole), finds the
 *                 matching elements in the target view(s) and overrides them directly. No
 *                 ParameterFilterElement is ever created; this is the "colorize, don't filter"
 *                 counterpart to Filter Pro's FilterCreator + FilterApplier.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-02
 * Last Updated  : 2026-07-02
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Document, active View, FilterSelection (categories, parameter, values, rule type,
 *                 graphics toggles — reusing the exact model FilterProWindow already populates).
 * Output        : GraphicsOperationSummary (attempted/applied/skipped) for the caller's transaction.
 *
 * Notes         :
 * - Reuses FilterRuleBuilder (rule building), FilterApplier.GetSolidFillId/ResolvePatternId/
 *   BuildOverrideSettings (colour + pattern + halftone construction), ColorPalette (colour source),
 *   and GraphicsElementService.ApplyOverrides (the actual view.SetElementOverrides loop) — all
 *   already-audited Filter Pro / Graphics Tools code. The only new logic here is orchestration and
 *   ColorizeElementMatcher's live (non-filter) element lookup.
 * - Does not start its own Transaction — the caller wraps this in
 *   GraphicsCommandService.ExecuteSummaryTransaction so the whole Colorize apply (across every
 *   target view) is one undo step.
 * - One color group per selected value, mirroring Filter Pro's "one filter per value" model, but
 *   applied directly to matched elements instead of being saved as separate filters.
 * - ApplyColorizeToViews mirrors Filter Pro's multi-view apply scope (Active View / Selected Views).
 *
 * Changelog     :
 * v1.0.0 (2026-07-02) - Initial release, built for the Colorize tool.
 * v1.1.0 (2026-07-02) - Added ApplyColorizeToViews to support the Apply tab's multi-view scope,
 *                       mirroring Filter Pro's own Active View / Selected Views option.
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

namespace AJTools.Services.Colorize
{
    /// <summary>
    /// Applies Colorize overrides directly to matched elements — no saved filter is created.
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

        internal static GraphicsOperationSummary ApplyColorize(
            Document doc,
            View view,
            FilterSelection selection,
            IList<string> skipped)
        {
            return ApplyColorize(doc, view, selection, ResolveColors(selection), skipped);
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

            if (!FilterApplier.HasAnyGraphicsToggleEnabled(selection))
            {
                skipped?.Add("No graphics options (lines, patterns, or halftone) were enabled.");
                return summary;
            }

            ElementId solidFillId = FilterApplier.GetSolidFillId(doc);
            ElementId patternId = FilterApplier.ResolvePatternId(doc, selection.PatternId, solidFillId);

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
            OverrideGraphicSettings settings = FilterApplier.BuildOverrideSettings(
                selection, new OverrideGraphicSettings(), color, patternId);

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

                IList<FilterRule> rules = FilterRuleBuilder.BuildRules(
                    selection.Parameter, value, selection.RuleType, selection.CaseSensitive, skipped);

                if (rules == null || rules.Count == 0)
                {
                    skipped?.Add(
                        $"Filter rules not supported for parameter '{selection.Parameter?.Name}' and value '{value.Display}'.");
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

                OverrideGraphicSettings settings = FilterApplier.BuildOverrideSettings(
                    selection, new OverrideGraphicSettings(), color, patternId);

                MergeSummary(summary, GraphicsElementService.ApplyOverrides(doc, view, matched, settings));
                valueIndex++;
            }
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
