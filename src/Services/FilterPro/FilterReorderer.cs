#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterReorderer.cs
 * Purpose       : Reorders filters in a view so newly created filters appear first, while
 *                 preserving the graphic overrides and visibility of all pre-existing filters.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
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
 * Input         : Active View, list of newly processed filter ElementIds, FilterSelection (graphics options)
 * Output        : View filter order updated; graphics and visibility of existing filters preserved
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - View.GetFilters() order is not guaranteed in Revit 2020; a per-view cache (_lastKnownOrderByView)
 *   is maintained to track the intended UI order across calls.
 * - All writes occur inside a Transaction managed by the caller.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
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
using AJTools.Utils;

namespace AJTools.Services.FilterPro
{
    internal static class FilterReorderer
    {
        // Cache last known UI order per view to avoid relying on Revit 2020 GetFilters ordering.
        // Keyed by a document-qualified string because view ids are only unique within one document,
        // so two open projects could otherwise cross-contaminate each other's cached filter order.
        private static readonly Dictionary<string, List<ElementId>> _lastKnownOrderByView
            = new Dictionary<string, List<ElementId>>();

        private static string BuildViewKey(Document doc, View view)
        {
            string docKey = doc?.PathName;
            if (string.IsNullOrEmpty(docKey))
                docKey = doc?.Title ?? "unknown";
            return docKey + "|" + AJTools.Utils.ElementIdHelper.GetIntegerValue(view.Id);
        }

        internal static void ReorderFiltersInView(Document doc,
                                                  View view,
                                                  List<ElementId> processedFilterIds,
                                                  FilterSelection selection,
                                                  ElementId solidFillId,
                                                  IList<string> skipped)
        {
            if (view == null || processedFilterIds == null || processedFilterIds.Count == 0)
                return;

            try
            {
                if (FilterApplier.IsViewControlledByTemplate(view))
                {
                    skipped?.Add($"View '{view.Name}' uses a view template. Filters were not modified.");
                    return;
                }

                var liveClean = GetLiveFilters(doc, view);
                var newFilters = GetNewFilters(doc, processedFilterIds);

                if (!newFilters.Any() && !liveClean.Any())
                    return;

                var baseline = BuildBaseline(doc, view, liveClean);
                var desiredOrder = BuildDesiredOrder(newFilters, baseline);

                if (IsOrderIdentical(baseline, desiredOrder))
                {
                    UpdateGraphicsForNewFilters(doc, view, newFilters, selection, solidFillId, skipped);
                    _lastKnownOrderByView[BuildViewKey(doc, view)] = desiredOrder.ToList();
                    return;
                }

                var (overridesMap, visibilityMap) = CaptureFilterSettings(view, liveClean);

                RemoveAllFilters(view, liveClean);
                ReapplyFilters(doc, view, desiredOrder, newFilters, selection, solidFillId, overridesMap, visibilityMap, skipped);

                _lastKnownOrderByView[BuildViewKey(doc, view)] = desiredOrder.ToList();
            }
            catch (Exception ex)
            {
                skipped?.Add($"Error reordering filters in view '{view?.Name}': {ex.Message}");
            }
        }

        private static List<ElementId> GetLiveFilters(Document doc, View view)
        {
            return (view.GetFilters() ?? new List<ElementId>())
                .Where(id => id != null &&
                             id != ElementId.InvalidElementId &&
                             doc.GetElement(id) != null)
                .ToList();
        }

        private static List<ElementId> GetNewFilters(Document doc, List<ElementId> processedFilterIds)
        {
            return processedFilterIds
                .Where(id => id != null &&
                             id != ElementId.InvalidElementId &&
                             doc.GetElement(id) != null)
                .Distinct(new ElementIdIntegerComparer())
                .ToList();
        }

        private static List<ElementId> BuildBaseline(Document doc, View view, List<ElementId> liveClean)
        {
            var key = BuildViewKey(doc, view);
            if (_lastKnownOrderByView.TryGetValue(key, out var snapshot) && snapshot != null)
            {
                var snapIds = new HashSet<int>(liveClean.Select(x => AJTools.Utils.ElementIdHelper.GetIntegerValue(x)));
                var baseline = snapshot
                    .Where(id => snapIds.Contains(AJTools.Utils.ElementIdHelper.GetIntegerValue(id)))
                    .ToList();

                foreach (var id in liveClean)
                {
                    if (!baseline.Any(x => AJTools.Utils.ElementIdHelper.GetIntegerValue(x) == AJTools.Utils.ElementIdHelper.GetIntegerValue(id)))
                        baseline.Add(id);
                }

                return baseline;
            }

            return new List<ElementId>(liveClean);
        }

        private static List<ElementId> BuildDesiredOrder(List<ElementId> newFilters, List<ElementId> baseline)
        {
            var newSet = new HashSet<int>(newFilters.Select(x => AJTools.Utils.ElementIdHelper.GetIntegerValue(x)));
            var desiredOrder = new List<ElementId>();

            foreach (var id in newFilters)
            {
                if (!desiredOrder.Any(x => AJTools.Utils.ElementIdHelper.GetIntegerValue(x) == AJTools.Utils.ElementIdHelper.GetIntegerValue(id)))
                    desiredOrder.Add(id);
            }

            foreach (var id in baseline)
            {
                if (!newSet.Contains(AJTools.Utils.ElementIdHelper.GetIntegerValue(id)) &&
                    !desiredOrder.Any(x => AJTools.Utils.ElementIdHelper.GetIntegerValue(x) == AJTools.Utils.ElementIdHelper.GetIntegerValue(id)))
                {
                    desiredOrder.Add(id);
                }
            }

            return desiredOrder;
        }

        private static bool IsOrderIdentical(List<ElementId> baseline, List<ElementId> desiredOrder)
        {
            if (baseline.Count != desiredOrder.Count)
                return false;

            for (int i = 0; i < baseline.Count; i++)
            {
                if (AJTools.Utils.ElementIdHelper.GetIntegerValue(baseline[i]) != AJTools.Utils.ElementIdHelper.GetIntegerValue(desiredOrder[i]))
                    return false;
            }

            return true;
        }

        private static void UpdateGraphicsForNewFilters(Document doc,
                                                        View view,
                                                        List<ElementId> newFilters,
                                                        FilterSelection selection,
                                                        ElementId solidFillId,
                                                        IList<string> skipped)
        {
            foreach (var id in newFilters)
            {
                try
                {
                    FilterApplier.ApplyGraphicsToFilter(doc, view, id, selection, solidFillId, skipped);
                    view.SetFilterVisibility(id, true);
                }
                catch (Exception ex)
                {
                    skipped?.Add($"Error updating filter {AJTools.Utils.ElementIdHelper.GetIntegerValue(id)} in view '{view.Name}': {ex.Message}");
                }
            }
        }

        private static (Dictionary<ElementId, OverrideGraphicSettings>, Dictionary<ElementId, bool>)
            CaptureFilterSettings(View view, List<ElementId> liveClean)
        {
            var overridesMap = new Dictionary<ElementId, OverrideGraphicSettings>(new ElementIdIntegerComparer());
            var visibilityMap = new Dictionary<ElementId, bool>(new ElementIdIntegerComparer());

            foreach (var id in liveClean)
            {
                try
                {
                    var ogs = view.GetFilterOverrides(id);
                    if (ogs != null)
                        overridesMap[id] = FilterApplier.CloneOverrideGraphics(ogs);
                }
                catch
                {
                    // Ignore if unable to read overrides.
                }

                try
                {
                    visibilityMap[id] = view.GetFilterVisibility(id);
                }
                catch
                {
                    // Default to visible if we cannot read it.
                    visibilityMap[id] = true;
                }
            }

            return (overridesMap, visibilityMap);
        }

        private static void RemoveAllFilters(View view, List<ElementId> liveClean)
        {
            foreach (var id in liveClean)
            {
                try
                {
                    view.RemoveFilter(id);
                }
                catch
                {
                    // Ignore if filter cannot be removed.
                }
            }
        }

        private static void ReapplyFilters(Document doc,
                                           View view,
                                           List<ElementId> desiredOrder,
                                           List<ElementId> newFilters,
                                           FilterSelection selection,
                                           ElementId solidFillId,
                                           Dictionary<ElementId, OverrideGraphicSettings> overridesMap,
                                           Dictionary<ElementId, bool> visibilityMap,
                                           IList<string> skipped)
        {
            var newSet = new HashSet<int>(newFilters.Select(x => AJTools.Utils.ElementIdHelper.GetIntegerValue(x)));
            var appliedIds = new HashSet<int>(
                (view.GetFilters() ?? new List<ElementId>()).Select(x => AJTools.Utils.ElementIdHelper.GetIntegerValue(x)));

            foreach (var id in desiredOrder)
            {
                try
                {
                    if (doc.GetElement(id) == null)
                        continue;

                    if (!appliedIds.Contains(AJTools.Utils.ElementIdHelper.GetIntegerValue(id)))
                    {
                        view.AddFilter(id);
                        appliedIds.Add(AJTools.Utils.ElementIdHelper.GetIntegerValue(id));
                    }

                    if (newSet.Contains(AJTools.Utils.ElementIdHelper.GetIntegerValue(id)))
                    {
                        // Newly created/updated filters: apply fresh graphics and make visible.
                        FilterApplier.ApplyGraphicsToFilter(doc, view, id, selection, solidFillId, skipped);
                        try
                        {
                            view.SetFilterVisibility(id, true);
                        }
                        catch
                        {
                            // Ignore visibility errors for new filters.
                        }
                    }
                    else
                    {
                        // Existing filters: restore previous visibility and overrides.
                        if (visibilityMap.TryGetValue(id, out bool vis))
                        {
                            try
                            {
                                view.SetFilterVisibility(id, vis);
                            }
                            catch
                            {
                                // Ignore.
                            }
                        }

                        if (overridesMap.TryGetValue(id, out var ogs))
                        {
                            try
                            {
                                view.SetFilterOverrides(id, ogs);
                            }
                            catch
                            {
                                // Ignore.
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    skipped?.Add($"Error reapplying filter {AJTools.Utils.ElementIdHelper.GetIntegerValue(id)} in view '{view.Name}': {ex.Message}");
                }
            }
        }
    }
}
