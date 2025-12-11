// Tool Name: Filter Pro - Reorderer
// Description: Maintains deterministic filter ordering and visibility states on views.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Linq
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models;
using AJTools.Utils;

namespace AJTools.Services
{
    internal static class FilterReorderer
    {
        // Cache last known UI order per view to avoid relying on Revit 2020 GetFilters ordering.
        private static readonly Dictionary<int, List<ElementId>> _lastKnownOrderByView
            = new Dictionary<int, List<ElementId>>();

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

                var baseline = BuildBaseline(view, liveClean);
                var desiredOrder = BuildDesiredOrder(newFilters, baseline);

                if (IsOrderIdentical(baseline, desiredOrder))
                {
                    UpdateGraphicsForNewFilters(doc, view, newFilters, selection, solidFillId, skipped);
                    _lastKnownOrderByView[view.Id.IntegerValue] = desiredOrder.ToList();
                    return;
                }

                var (overridesMap, visibilityMap) = CaptureFilterSettings(view, liveClean);

                RemoveAllFilters(view, liveClean);
                ReapplyFilters(doc, view, desiredOrder, newFilters, selection, solidFillId, overridesMap, visibilityMap, skipped);

                _lastKnownOrderByView[view.Id.IntegerValue] = desiredOrder.ToList();
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

        private static List<ElementId> BuildBaseline(View view, List<ElementId> liveClean)
        {
            var key = view.Id.IntegerValue;
            if (_lastKnownOrderByView.TryGetValue(key, out var snapshot) && snapshot != null)
            {
                var snapIds = new HashSet<int>(liveClean.Select(x => x.IntegerValue));
                var baseline = snapshot
                    .Where(id => snapIds.Contains(id.IntegerValue))
                    .ToList();

                foreach (var id in liveClean)
                {
                    if (!baseline.Any(x => x.IntegerValue == id.IntegerValue))
                        baseline.Add(id);
                }

                return baseline;
            }

            return new List<ElementId>(liveClean);
        }

        private static List<ElementId> BuildDesiredOrder(List<ElementId> newFilters, List<ElementId> baseline)
        {
            var newSet = new HashSet<int>(newFilters.Select(x => x.IntegerValue));
            var desiredOrder = new List<ElementId>();

            foreach (var id in newFilters)
            {
                if (!desiredOrder.Any(x => x.IntegerValue == id.IntegerValue))
                    desiredOrder.Add(id);
            }

            foreach (var id in baseline)
            {
                if (!newSet.Contains(id.IntegerValue) &&
                    !desiredOrder.Any(x => x.IntegerValue == id.IntegerValue))
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
                if (baseline[i].IntegerValue != desiredOrder[i].IntegerValue)
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
                    skipped?.Add($"Error updating filter {id.IntegerValue} in view '{view.Name}': {ex.Message}");
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
            var newSet = new HashSet<int>(newFilters.Select(x => x.IntegerValue));
            var appliedIds = new HashSet<int>(
                (view.GetFilters() ?? new List<ElementId>()).Select(x => x.IntegerValue));

            foreach (var id in desiredOrder)
            {
                try
                {
                    if (doc.GetElement(id) == null)
                        continue;

                    if (!appliedIds.Contains(id.IntegerValue))
                    {
                        view.AddFilter(id);
                        appliedIds.Add(id.IntegerValue);
                    }

                    if (newSet.Contains(id.IntegerValue))
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
                    skipped?.Add($"Error reapplying filter {id.IntegerValue} in view '{view.Name}': {ex.Message}");
                }
            }
        }
    }
}
