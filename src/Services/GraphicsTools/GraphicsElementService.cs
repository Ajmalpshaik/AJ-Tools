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
