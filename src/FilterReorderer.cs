using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace AJTools
{
    internal static class FilterReorderer
    {
        internal static void ReorderFiltersInView(Document doc,
                                                  View view,
                                                  List<ElementId> processedFilterIds,
                                                  FilterSelection selection,
                                                  ElementId solidFillId,
                                                  IList<string> skipped)
        {
            if (view == null)
                return;

            try
            {
                if (FilterApplier.IsViewControlledByTemplate(view))
                {
                    skipped?.Add($"View '{view.Name}' uses a view template. Filters were not modified.");
                    return;
                }

                var liveFilters = view.GetFilters() ?? new List<ElementId>();
                var liveClean = liveFilters
                    .Where(id => doc.GetElement(id) != null)
                    .ToList();

                // Filters touched/created in THIS run
                var newFilters = (processedFilterIds ?? new List<ElementId>())
                    .Where(id => id != null &&
                                 id != ElementId.InvalidElementId &&
                                 doc.GetElement(id) != null)
                    .Distinct(new ElementIdIntegerComparer())
                    .ToList();

                if (!liveClean.Any() && !newFilters.Any())
                    return;

                // ------------------------------------------------------------------
                // Desired order:
                //   1) All filters from THIS run (in the order we processed them)
                //   2) Then all other existing filters, keeping their current order
                //
                // Example:
                //   Before: A,B,C
                //   Run1 (new D,E): => D,E,A,B,C
                //   Run2 (new F):   => F,D,E,A,B,C   ✅
                // ------------------------------------------------------------------
                var desiredOrder = new List<ElementId>();
                var seen = new HashSet<int>();

                // 1) This run's filters first
                foreach (var id in newFilters)
                {
                    if (seen.Add(id.IntegerValue))
                        desiredOrder.Add(id);
                }

                // 2) Then all other live filters in their existing order
                foreach (var id in liveClean)
                {
                    if (seen.Add(id.IntegerValue))
                        desiredOrder.Add(id);
                }

                // ------------------------------------------------------------------
                // Capture overrides & visibility BEFORE we remove anything
                // ------------------------------------------------------------------
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
                        // ignore per filter
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

                // Remove all existing filters from the view
                foreach (var id in liveClean)
                {
                    try { view.RemoveFilter(id); }
                    catch { }
                }

                // ------------------------------------------------------------------
                // Add back in desired order, restoring overrides
                // ------------------------------------------------------------------
                var newSet = new HashSet<int>(newFilters.Select(x => x.IntegerValue));

                foreach (var id in desiredOrder)
                {
                    if (doc.GetElement(id) == null)
                        continue;

                    try
                    {
                        // Add (no duplicate check needed, we removed all already)
                        view.AddFilter(id);

                        if (newSet.Contains(id.IntegerValue))
                        {
                            // This run's filters: apply graphics + ensure visible
                            FilterApplier.ApplyGraphicsToFilter(doc, view, id, selection, solidFillId, skipped);
                            try { view.SetFilterVisibility(id, true); } catch { }
                        }
                        else
                        {
                            // Existing filters: restore prior visibility and overrides
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
            }
            catch (Exception ex)
            {
                skipped?.Add($"Error reordering filters in view '{view?.Name}': {ex.Message}");
            }
        }
    }
}
