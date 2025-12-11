// Tool Name: Filter Pro - Filter Applier
// Description: Applies parameter filters to views with graphic overrides and order management.
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

namespace AJTools.Services
{
    internal static class FilterApplier
    {
        internal static void ApplyToView(
            Document doc,
            View view,
            ElementId filterId,
            FilterSelection selection,
            ElementId solidFillId,
            IList<string> skipped)
        {
            if (view == null || filterId == ElementId.InvalidElementId)
                return;

            if (IsViewControlledByTemplate(view))
            {
                skipped?.Add($"View '{view.Name}' uses a view template. Filters were not modified.");
                return;
            }

            if (doc.GetElement(filterId) == null)
                return;

            try
            {
                var current = view.GetFilters() ?? new List<ElementId>();
                if (!current.Contains(filterId))
                    view.AddFilter(filterId);
            }
            catch (Exception ex)
            {
                skipped?.Add($"Failed to add filter {filterId.IntegerValue} to view '{view.Name}': {ex.Message}");
                return;
            }

            ApplyGraphicsToFilter(doc, view, filterId, selection, solidFillId, skipped);

            try
            {
                view.SetFilterVisibility(filterId, true);
            }
            catch
            {
                // Ignore errors when setting filter visibility, as it's not critical.
            }
        }

        internal static void ApplyGraphicsToFilter(
            Document doc,
            View view,
            ElementId filterId,
            FilterSelection selection,
            ElementId solidFillId,
            IList<string> skipped)
        {
            if (selection == null || !selection.ApplyGraphics)
                return;

            OverrideGraphicSettings existing = null;
            try
            {
                existing = view.GetFilterOverrides(filterId);
            }
            catch
            {
                // Ignore and treat as no overrides
            }

            var ogs = existing != null
                ? CloneOverrideGraphics(existing)
                : new OverrideGraphicSettings();

            bool applyProjLines = selection.ColorProjectionLines;
            bool applyProjPatterns = selection.ColorProjectionPatterns;
            bool applyCutLines = selection.ColorCutLines;
            bool applyCutPatterns = selection.ColorCutPatterns;
            bool applyHalftone = selection.ColorHalftone;

            if (!applyProjLines && !applyProjPatterns &&
                !applyCutLines && !applyCutPatterns &&
                !applyHalftone)
            {
                return;
            }

            Color chosenColor = GetColor(selection, filterId);
            ElementId patternId = ResolvePatternId(doc, selection.PatternId, solidFillId);

            try
            {
                if (applyProjLines)
                {
                    ogs.SetProjectionLineColor(chosenColor);
                }

                if (applyCutLines)
                {
                    ogs.SetCutLineColor(chosenColor);
                }

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

                if (applyHalftone)
                {
                    ogs.SetHalftone(true);
                }

                view.SetFilterOverrides(filterId, ogs);
            }
            catch (Exception ex)
            {
                skipped?.Add($"Error applying graphics for filter {filterId.IntegerValue} in view '{view.Name}': {ex.Message}");
            }
        }

        internal static OverrideGraphicSettings CloneOverrideGraphics(OverrideGraphicSettings source)
        {
            if (source == null)
                return new OverrideGraphicSettings();

            var clone = new OverrideGraphicSettings();

            // Revit API may throw if a property was never set; ignore and continue.
            try { clone.SetProjectionLineColor(source.ProjectionLineColor); } catch { }
            try { clone.SetCutLineColor(source.CutLineColor); } catch { }
            try { clone.SetSurfaceForegroundPatternId(source.SurfaceForegroundPatternId); } catch { }
            try { clone.SetSurfaceForegroundPatternColor(source.SurfaceForegroundPatternColor); } catch { }
            try { clone.SetCutForegroundPatternId(source.CutForegroundPatternId); } catch { }
            try { clone.SetCutForegroundPatternColor(source.CutForegroundPatternColor); } catch { }
            try { clone.SetHalftone(source.Halftone); } catch { }

            return clone;
        }

        internal static bool IsViewControlledByTemplate(View view)
        {
            if (view == null)
                return false;

            return !view.IsTemplate && view.ViewTemplateId != ElementId.InvalidElementId;
        }

        internal static ElementId GetSolidFillId(Document doc)
        {
            try
            {
                var solidPattern = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                return solidPattern?.Id ?? ElementId.InvalidElementId;
            }
            catch
            {
                return ElementId.InvalidElementId;
            }
        }

        private static Color GetColor(FilterSelection selection, ElementId filterId)
        {
            return selection.RandomColors
                ? ColorPalette.GetRandomColor()
                : ColorPalette.GetColorFor(filterId);
        }

        private static ElementId ResolvePatternId(Document doc, ElementId requested, ElementId solidFillId)
        {
            try
            {
                if (requested != null &&
                    requested != ElementId.InvalidElementId &&
                    doc.GetElement(requested) != null)
                {
                    return requested;
                }

                if (solidFillId != null && solidFillId != ElementId.InvalidElementId)
                    return solidFillId;
            }
            catch
            {
                // Ignore lookup issues and fall back to invalid
            }

            return ElementId.InvalidElementId;
        }
    }
}
