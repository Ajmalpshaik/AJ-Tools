// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Applies and clears element-level graphics overrides.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-03-30
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Revit view, element ids, and override settings.
// Output       : Element graphics operation summary.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.4 - Reviewed element override application and reset behavior for release.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;

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

                int key = elementId.IntegerValue;
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
