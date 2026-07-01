#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : GraphicsElementService.cs
 * Purpose       : Applies and clears element-level graphics overrides for the active view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Revit view, element ids, and override settings.
 * Output        : Element graphics operation summary (attempted / applied / skipped).
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Clearing overrides applies an empty OverrideGraphicSettings (reset to By View).
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Version-safe ElementId access; full metadata block.
 * v1.4.4 (2026-05-09) - Reviewed element override application and reset behavior for release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Services.GraphicsTools
{
    /// <summary>
    /// Element-focused graphics override operations for the active view.
    /// </summary>
    internal static class GraphicsElementService
    {
        public static GraphicsOperationSummary ApplyOverrides(
            Document doc,
            View view,
            IEnumerable<ElementId> elementIds,
            OverrideGraphicSettings settings)
        {
            var summary = new GraphicsOperationSummary();

            if (doc == null || view == null || elementIds == null || settings == null)
            {
                return summary;
            }

            var processed = new HashSet<int>();

            foreach (ElementId elementId in elementIds)
            {
                if (elementId == null || elementId == ElementId.InvalidElementId)
                {
                    continue;
                }

                int key = ElementIdHelper.GetIntegerValue(elementId);
                if (processed.Contains(key))
                {
                    continue;
                }

                processed.Add(key);
                summary.Attempted++;

                Element element = doc.GetElement(elementId);
                if (element == null)
                {
                    summary.Skipped++;
                    continue;
                }

                try
                {
                    view.SetElementOverrides(elementId, settings);
                    summary.Applied++;
                }
                catch
                {
                    summary.Skipped++;
                }
            }

            return summary;
        }

        public static GraphicsOperationSummary ClearOverrides(
            Document doc,
            View view,
            IEnumerable<ElementId> elementIds)
        {
            return ApplyOverrides(doc, view, elementIds, new OverrideGraphicSettings());
        }
    }
}
