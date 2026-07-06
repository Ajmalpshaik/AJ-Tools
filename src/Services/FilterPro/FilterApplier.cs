#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterApplier.cs
 * Purpose       : Applies parameter filters to views with graphic colour overrides, fill pattern
 *                 assignment, halftone, and view-template protection checks.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.1
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-02
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, System.Linq
 *
 * Input         : Active View, FilterSelection (graphics options), ElementId of filter and solid fill pattern
 * Output        : View filter graphics updated â€” colour, pattern, halftone, visibility
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - OverrideGraphicSettings surface/cut pattern API (SetSurfaceForegroundPatternId etc.) confirmed valid 2020-2026.
 * - Read-only service â€” all model writes occur inside a Transaction managed by the caller.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 * v1.1.0 (2026-07-02) - Extracted BuildOverrideSettings (pure colour/pattern/halftone construction)
 *                        out of ApplyGraphicsToFilter, and promoted ResolvePatternId and
 *                        HasAnyGraphicsToggleEnabled to internal, so the new Colorize tool can build
 *                        the same OverrideGraphicSettings and apply them directly to elements instead
 *                        of to a saved filter. No behavior change to Filter Pro's own apply path.
 * v1.1.1 (2026-07-02) - Fixed BuildOverrideSettings never calling SetSurfaceForegroundPatternVisible/
 *                        SetCutForegroundPatternVisible(true) â€” a fill pattern override in Revit needs
 *                        its own visibility flag turned on or it never renders even with a valid
 *                        pattern id and colour set. Line colour overrides were unaffected (no separate
 *                        visibility flag). Fixes both Filter Pro's and Colorize's pattern checkboxes.
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
                // Must compare by IntegerValue â€” Revit may return new ElementId wrapper objects
                // that are not reference-equal even when they represent the same filter.
                bool alreadyAdded = false;
                foreach (var existingId in current)
                {
                    if (existingId != null && AJTools.Utils.ElementIdHelper.GetIntegerValue(existingId) == AJTools.Utils.ElementIdHelper.GetIntegerValue(filterId))
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
                skipped?.Add($"Failed to add filter {AJTools.Utils.ElementIdHelper.GetIntegerValue(filterId)} to view '{view.Name}': {ex.Message}");
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

            if (!HasAnyGraphicsToggleEnabled(selection))
            {
                return;
            }

            Color chosenColor = GetColor(selection, filterId);
            ElementId patternId = ResolvePatternId(doc, selection.PatternId, solidFillId);

            try
            {
                ogs = BuildOverrideSettings(selection, ogs, chosenColor, patternId);
                view.SetFilterOverrides(filterId, ogs);
            }
            catch (Exception ex)
            {
                skipped?.Add($"Error applying graphics for filter {AJTools.Utils.ElementIdHelper.GetIntegerValue(filterId)} in view '{view.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// True when at least one graphics toggle (projection/cut lines or patterns, halftone) is enabled.
        /// Shared with Colorize so it can skip a value group that would otherwise "apply" a no-op override.
        /// </summary>
        internal static bool HasAnyGraphicsToggleEnabled(FilterSelection selection)
        {
            return selection.ColorProjectionLines ||
                   selection.ColorProjectionPatterns ||
                   selection.ColorCutLines ||
                   selection.ColorCutPatterns ||
                   selection.ColorHalftone;
        }

        /// <summary>
        /// Builds (or extends) an OverrideGraphicSettings from a FilterSelection's graphics toggles, a
        /// resolved colour, and a resolved pattern id. Pure construction â€” does not touch any view or
        /// filter. Shared by Filter Pro (applies the result to a saved filter) and Colorize (applies the
        /// result directly to matched elements).
        /// </summary>
        internal static OverrideGraphicSettings BuildOverrideSettings(
            FilterSelection selection,
            OverrideGraphicSettings baseSettings,
            Color color,
            ElementId patternId)
        {
            var ogs = baseSettings ?? new OverrideGraphicSettings();

            if (selection.ColorProjectionLines)
            {
                ogs.SetProjectionLineColor(color);
            }

            if (selection.ColorCutLines)
            {
                ogs.SetCutLineColor(color);
            }

            if (selection.ColorProjectionPatterns)
            {
                if (patternId != ElementId.InvalidElementId)
                    ogs.SetSurfaceForegroundPatternId(patternId);

                ogs.SetSurfaceForegroundPatternColor(color);
                ogs.SetSurfaceForegroundPatternVisible(true);
            }

            if (selection.ColorCutPatterns)
            {
                if (patternId != ElementId.InvalidElementId)
                    ogs.SetCutForegroundPatternId(patternId);

                ogs.SetCutForegroundPatternColor(color);
                ogs.SetCutForegroundPatternVisible(true);
            }

            if (selection.ColorHalftone)
            {
                ogs.SetHalftone(true);
            }

            return ogs;
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

        internal static ElementId ResolvePatternId(Document doc, ElementId requested, ElementId solidFillId)
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
