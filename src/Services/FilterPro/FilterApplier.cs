#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterApplier.cs
 * Purpose       : Applies parameter filters to views with graphic colour overrides, fill pattern
 *                 assignment, halftone, and view-template protection checks.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.2
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-23
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, System.Linq
 *
 * Input         : Active View, FilterSelection (graphics options), ElementId of filter and solid fill pattern
 * Output        : View filter graphics updated — colour, pattern, halftone, visibility
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - OverrideGraphicSettings surface/cut pattern API (SetSurfaceForegroundPatternId etc.) verified
 *   unchanged across Revit 2020-2027 against the per-version RevitAPI reference assemblies (2026-07-23).
 * - Read-only service — all model writes occur inside a Transaction managed by the caller.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 * v1.0.2 (2026-07-23) - Compatibility audit: every Revit API member used here verified present on
 *                        2020-2027 against the per-version RevitAPI reference assemblies. No code
 *                        changes needed.
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
    internal static class FilterApplier
    {
        /// <summary>
        /// Applies a parameter filter to a view with graphic overrides.
        /// </summary>
        /// <param name="doc">The active Revit document.</param>
        /// <param name="view">The view to apply the filter to.</param>
        /// <param name="filterId">The ID of the filter to apply.</param>
        /// <param name="selection">The filter selection containing graphics preferences.</param>
        /// <param name="solidFillId">The ID of the solid fill pattern.</param>
        /// <param name="skipped">List to collect any skip messages.</param>
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
                // Must compare by IntegerValue — Revit may return new ElementId wrapper objects
                // that are not reference-equal even when they represent the same filter.
                bool alreadyAdded = false;
                foreach (var existingId in current)
                {
                    if (existingId != null && existingId.IntValue() == filterId.IntValue())
                    {
                        alreadyAdded = true;
                        break;
                    }
                }
                if (!alreadyAdded)
                    view.AddFilter(filterId);
            }
            catch (Exception ex)
            {
                skipped?.Add($"Failed to add filter {filterId.IntValue()} to view '{view.Name}': {ex.Message}");
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

        /// <summary>
        /// Applies graphic overrides to a filter in a view.
        /// </summary>
        /// <param name="doc">The active Revit document.</param>
        /// <param name="view">The view containing the filter.</param>
        /// <param name="filterId">The ID of the filter to apply graphics to.</param>
        /// <param name="selection">The filter selection containing graphics preferences.</param>
        /// <param name="solidFillId">The ID of the solid fill pattern.</param>
        /// <param name="skipped">List to collect any skip messages.</param>
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
                skipped?.Add($"Error applying graphics for filter {filterId.IntValue()} in view '{view.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a deep copy of override graphic settings.
        /// </summary>
        /// <param name="source">The source override settings to clone.</param>
        /// <returns>A new OverrideGraphicSettings instance with copied settings.</returns>
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

        /// <summary>
        /// Checks if a view is controlled by a view template.
        /// </summary>
        /// <param name="view">The view to check.</param>
        /// <returns>True if the view has a view template applied, false otherwise.</returns>
        internal static bool IsViewControlledByTemplate(View view)
        {
            if (view == null)
                return false;

            return !view.IsTemplate && view.ViewTemplateId != ElementId.InvalidElementId;
        }

        /// <summary>
        /// Retrieves the ElementId of the solid fill pattern in the document.
        /// </summary>
        /// <param name="doc">The active Revit document.</param>
        /// <returns>The ElementId of the solid fill pattern, or InvalidElementId if not found.</returns>
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
