#region Metadata
/*
 * Tool Name     : Apply Graphics
 * File Name     : GraphicsDataProvider.cs
 * Purpose       : Builds line pattern, fill pattern, line weight, and category dropdown data for the Apply Graphics UI.
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
 * Input         : Active Revit document.
 * Output        : Line pattern, fill pattern, line weight, and category options.
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Read-only collection; no transaction.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Version-safe ElementId access; full metadata block.
 * v1.4.4 (2026-05-09) - Removed dead transparency dropdown options after moving the UI to a slider.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;
using AJTools.Utils;

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
                        selectedIds.Add(ElementIdHelper.GetIntegerValue(categoryId));
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
                    selectedIds.Contains(ElementIdHelper.GetIntegerValue(category.Id))))
                .ToList();
        }
    }
}
