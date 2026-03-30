using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;

namespace AJTools.Services.GraphicsTools
{
    /// <summary>
    /// Builds lightweight dropdown data for graphics settings UI.
    /// </summary>
    internal static class GraphicsDataProvider
    {
        public static IList<GraphicsIdOption> GetLinePatternOptions(Document doc)
        {
            var options = new List<GraphicsIdOption>
            {
                new GraphicsIdOption(ElementId.InvalidElementId, "<By View>")
            };

            if (doc == null)
            {
                return options;
            }

            var patternElements = new FilteredElementCollector(doc)
                .OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .Where(pattern => pattern != null && pattern.Id != null && pattern.Id != ElementId.InvalidElementId)
                .OrderBy(pattern => pattern.Name, StringComparer.CurrentCultureIgnoreCase);

            foreach (LinePatternElement pattern in patternElements)
            {
                options.Add(new GraphicsIdOption(pattern.Id, pattern.Name));
            }

            return options;
        }

        public static IList<GraphicsIdOption> GetFillPatternOptions(Document doc)
        {
            var options = new List<GraphicsIdOption>
            {
                new GraphicsIdOption(ElementId.InvalidElementId, "<By View>")
            };

            if (doc == null)
            {
                return options;
            }

            var fillPatternElements = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .Where(element => element != null)
                .Select(element => new
                {
                    Element = element,
                    Pattern = element.GetFillPattern()
                })
                .Where(item => item.Pattern != null && item.Pattern.IsValidObject)
                .OrderBy(item => item.Pattern.Target == FillPatternTarget.Drafting ? 0 : 1)
                .ThenBy(item => item.Pattern.Name, StringComparer.CurrentCultureIgnoreCase);

            foreach (var item in fillPatternElements)
            {
                string targetLabel = item.Pattern.Target == FillPatternTarget.Drafting ? "Drafting" : "Model";
                options.Add(new GraphicsIdOption(item.Element.Id, string.Format("{0} - {1}", targetLabel, item.Pattern.Name)));
            }

            return options;
        }

        public static IList<GraphicsLineWeightOption> GetLineWeightOptions()
        {
            var options = new List<GraphicsLineWeightOption>
            {
                new GraphicsLineWeightOption(OverrideGraphicSettings.InvalidPenNumber, "<By View>")
            };

            for (int weight = 1; weight <= 16; weight++)
            {
                options.Add(new GraphicsLineWeightOption(weight, weight.ToString()));
            }

            return options;
        }

        public static IList<int> GetTransparencyOptions()
        {
            var options = new List<int>(101);
            for (int value = 0; value <= 100; value++)
            {
                options.Add(value);
            }

            return options;
        }
    }
}
