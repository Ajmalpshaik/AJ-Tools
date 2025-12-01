using System;
using System.Collections.Generic;
using System.Linq;
using SD = System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using DB = Autodesk.Revit.DB;

namespace AJTools
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdFilterPro : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show("Filter Pro", "Open a project document before running this command.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            DB.View activeView = uiDoc.ActiveView;
            
            var window = new FilterProWindow(doc, activeView);
            window.ShowDialog();
            return Result.Succeeded;
        }

        internal static int CreateFilters(Document doc, DB.View activeView, FilterSelection selection, IList<string> skipped)
        {
            var targets = new List<DB.View>();
            if (activeView != null)
                targets.Add(activeView);

            return FilterProHelper.CreateFilters(doc, targets, selection, skipped);
        }

        internal static IList<FilterRule> BuildRules(FilterParameterItem parameter, FilterValueItem value, string ruleType)
        {
            // This method is now in FilterProHelper but kept here for compatibility
            return null;
        }

        private static void ApplyToView(DB.View view, ElementId filterId, bool randomColors)
        {
            if (view == null)
                return;

            ICollection<ElementId> current = view.GetFilters();
            if (current == null || !current.Contains(filterId))
            {
                try
                {
                    view.AddFilter(filterId);
                }
                catch
                {
                    return;
                }
            }

            OverrideGraphicSettings ogs = view.GetFilterOverrides(filterId);
            if (ogs == null)
                ogs = new OverrideGraphicSettings();

            DB.Color color = randomColors ? ColorPalette.GetRandomColor() : ColorPalette.GetColorFor(filterId);
            ogs.SetProjectionLineColor(color);
            ogs.SetCutLineColor(color);

            view.SetFilterOverrides(filterId, ogs);
        }

        private static string ComposeFilterName(FilterSelection selection, FilterValueItem value, Document doc)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(selection.Prefix))
                parts.Add(selection.Prefix.Trim());

            if (selection.IncludeCategory)
            {
                string catLabel = ResolveCategoryLabel(selection.CategoryIds, doc);
                if (!string.IsNullOrWhiteSpace(catLabel))
                    parts.Add(catLabel);
            }

            if (selection.IncludeParameter)
                parts.Add(selection.Parameter.Name);

            parts.Add(value.Display ?? "Value");

            string name = string.Join(" - ", parts);
            if (!string.IsNullOrWhiteSpace(selection.Suffix))
                name += " " + selection.Suffix.Trim();

            return SanitizeName(name);
        }

        private static string ResolveCategoryLabel(IEnumerable<ElementId> categoryIds, Document doc)
        {
            if (categoryIds == null)
                return string.Empty;

            var names = new List<string>();
            foreach (ElementId catId in categoryIds.Take(3))
            {
                Category cat = Category.GetCategory(doc, catId);
                if (cat != null)
                    names.Add(cat.Name);
            }

            if (!names.Any())
                return string.Empty;

            return categoryIds.Count() > 3 ? names[0] + " +" : string.Join(", ", names);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Filter";

            char[] invalid = { '<', '>', '{', '}', '[', ']', '|', ';' };
            foreach (char c in invalid)
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static ParameterFilterElement FindFilterByName(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
