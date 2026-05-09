// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Provides Revit graphics option data for the Apply Graphics UI.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-03-30
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document.
// Output       : Line pattern, fill pattern, line weight, and category options.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.4 - Removed dead transparency dropdown options after moving the UI to a slider.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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

        public static IList<GraphicsCategoryOption> GetCategoryOptions(
            IEnumerable<Category> categories,
            ICollection<ElementId> preselectedCategoryIds)
        {
            var selectedIds = new HashSet<int>();
            if (preselectedCategoryIds != null)
            {
                foreach (ElementId categoryId in preselectedCategoryIds)
                {
                    if (categoryId != null && categoryId != ElementId.InvalidElementId)
                    {
                        selectedIds.Add(categoryId.IntegerValue);
                    }
                }
            }

            if (categories == null)
            {
                return new List<GraphicsCategoryOption>();
            }

            return categories
                .Where(category => category != null && category.Id != null && category.Id != ElementId.InvalidElementId)
                .OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(category => new GraphicsCategoryOption(
                    category.Id,
                    category.Name,
                    selectedIds.Contains(category.Id.IntegerValue)))
                .ToList();
        }
    }
}
