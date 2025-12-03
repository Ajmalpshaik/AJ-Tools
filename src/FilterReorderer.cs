using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace AJTools
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
            if (view == null)
                return;

            // Nothing to reorder if we have no processed filters
            if (processedFilterIds == null || processedFilterIds.Count == 0)
                return;

            try
            {
                if (FilterApplier.IsViewControlledByTemplate(view))
                {
                    skipped?.Add($"View '{view.Name}' uses a view template. Filters were not modified.");
                    return;
                }

                // Actual filters currently on the view (physical truth)
                var liveFilters = view.GetFilters() ?? new List<ElementId>();
                var liveClean = liveFilters
                    .Where(id => id != null &&
                                 id != ElementId.InvalidElementId &&
                                 doc.GetElement(id) != null)
                    .ToList();

                // New/updated filters from THIS run only
                var newFilters = processedFilterIds
                    .Where(id => id != null &&
                                 id != ElementId.InvalidElementId &&
                                 doc.GetElement(id) != null)
                    .Distinct(new ElementIdIntegerComparer())
                    .ToList();

                if (!newFilters.Any() && !liveClean.Any())
                    return;

                // --- Build baseline (previous order) ---
                // If we have a remembered order for this view, use that as baseline,
                // but only keep filters that still exist in the view.
                var key = view.Id.IntegerValue;
                List<ElementId> baseline;

                if (_lastKnownOrderByView.TryGetValue(key, out var snapshot) && snapshot != null)
                {
                    var snapIds = new HashSet<int>(liveClean.Select(x => x.IntegerValue));
                    baseline = snapshot
                        .Where(id => snapIds.Contains(id.IntegerValue))
                        .ToList();

                    // Add any filters that exist now but were not in snapshot (e.g. user added)
                    foreach (var id in liveClean)
                    {
                        if (!baseline.Any(x => x.IntegerValue == id.IntegerValue))
                            baseline.Add(id);
                    }
                }
                else
                {
                    // First run for this view: baseline is just whatever Revit gives us
                    baseline = new List<ElementId>(liveClean);
                }

                // --- Build desired order: new filters (this run) first, then others in previous order ---
                var newSet = new HashSet<int>(newFilters.Select(x => x.IntegerValue));
                var desiredOrder = new List<ElementId>();

                // 1) New filters at the top, in the order they were created/processed
                foreach (var id in newFilters)
                {
                    if (!desiredOrder.Any(x => x.IntegerValue == id.IntegerValue))
                        desiredOrder.Add(id);
                }

                // 2) All remaining filters in their previous relative order
                foreach (var id in baseline)
                {
                    if (!newSet.Contains(id.IntegerValue) &&
                        !desiredOrder.Any(x => x.IntegerValue == id.IntegerValue))
                    {
                        desiredOrder.Add(id);
                    }
                }

                // If the "baseline" and "desired" are identical, just update graphics for new ones
                bool identical =
                    baseline.Count == desiredOrder.Count &&
                    !baseline.Where((t, i) => t.IntegerValue != desiredOrder[i].IntegerValue).Any();

                if (identical)
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

                    // Update snapshot
                    _lastKnownOrderByView[key] = desiredOrder.ToList();
                    return;
                }

                // --- Capture existing overrides/visibility from the current live filters only ---
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
                        // ignore, leave ogs missing if we can't read it
                    }

                    try
                    {
                        visibilityMap[id] = view.GetFilterVisibility(id);
                    }
                    catch
                    {
                        visibilityMap[id] = true;
                    }
                }

                // --- Remove all currently-applied filters from the view ---
                foreach (var id in liveClean)
                {
                    try { view.RemoveFilter(id); }
                    catch { }
                }

                // --- Add filters back in the desired order ---
                foreach (var id in desiredOrder)
                {
                    try
                    {
                        if (doc.GetElement(id) == null)
                            continue;

                        // Avoid duplicates just in case
                        var current = view.GetFilters() ?? new List<ElementId>();
                        if (!current.Any(x => x.IntegerValue == id.IntegerValue))
                            view.AddFilter(id);

                        if (newSet.Contains(id.IntegerValue))
                        {
                            // New/updated this run: apply new graphics & force visible
                            FilterApplier.ApplyGraphicsToFilter(doc, view, id, selection, solidFillId, skipped);
                            try { view.SetFilterVisibility(id, true); } catch { }
                        }
                        else
                        {
                            // Existing filter: restore previous visibility & overrides if we have them
                            if (visibilityMap.TryGetValue(id, out bool vis))
                            {
                                try { view.SetFilterVisibility(id, vis); } catch { }
                            }

                            if (overridesMap.TryGetValue(id, out var ogs))
                            {
                                try { view.SetFilterOverrides(id, ogs); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        skipped?.Add($"Error reapplying filter {id.IntegerValue} in view '{view.Name}': {ex.Message}");
                    }
                }

                // Save the new order as our snapshot for the next run
                _lastKnownOrderByView[key] = desiredOrder.ToList();
            }
            catch (Exception ex)
            {
                skipped?.Add($"Error reordering filters in view '{view?.Name}': {ex.Message}");
            }
        }
    }
}
