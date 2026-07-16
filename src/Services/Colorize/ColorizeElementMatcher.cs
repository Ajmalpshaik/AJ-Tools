#region Metadata
/*
 * Tool Name     : Colorize
 * File Name     : ColorizeElementMatcher.cs
 * Purpose       : Finds the elements in a given view that match a set of categories and
 *                 (optionally) parameter filter rules, right now - the read-only counterpart to
 *                 what Filter Pro wraps in a saved ParameterFilterElement.
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
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Document, a target View, selected category ids, and FilterRules built by ColorizeApplier.
 * Output        : The distinct ElementIds (instances only) currently matching, in that view.
 *
 * Notes         :
 * - No ParameterFilterElement is created and no view filter is touched - this only runs a live
 *   FilteredElementCollector query with the same category + rule shape Filter Pro uses.
 * - Scoped to the given view (FilteredElementCollector(doc, view.Id)) and to instances only
 *   (WhereElementIsNotElementType) since only instances receive per-element graphics overrides.
 *   ColorizeApplier calls this once per target view (Active View, or every Selected View).
 * - When rules is null/empty, matches by category only (category-wide colorize, no parameter rule).
 * - Read-only service - no Transaction required.
 *
 * Changelog     :
 * v1.0.0 (2026-07-13) - Ported from the standalone AJ Tools tree into the live multi-version src/
 *                       project so the Colorize tool actually gets built and deployed (it previously
 *                       existed only in the stale pre-multiversion copy and could never appear on the
 *                       ribbon no matter how many times the add-in was rebuilt). Logic unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Services.Colorize
{
    /// <summary>
    /// Runs category + parameter-rule matching live against a target view (no saved filter).
    /// </summary>
    internal static class ColorizeElementMatcher
    {
        internal static ICollection<ElementId> GetMatchingElementIds(
            Document doc,
            View view,
            IList<ElementId> categoryIds,
            IList<FilterRule> rules)
        {
            if (doc == null || view == null || categoryIds == null || categoryIds.Count == 0)
            {
                return new List<ElementId>();
            }

            var categoryFilter = new ElementMulticategoryFilter(categoryIds);
            FilteredElementCollector collector = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .WherePasses(categoryFilter);

            if (rules != null && rules.Count > 0)
            {
                var elementFilter = new ElementParameterFilter(rules);
                collector = collector.WherePasses(elementFilter);
            }

            return collector.ToElementIds();
        }
    }
}
