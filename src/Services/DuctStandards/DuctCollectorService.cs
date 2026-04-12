using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Services.DuctStandards
{
    internal static class DuctCollectorService
    {
        public static List<Element> GetProjectDucts(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DuctCurves)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        public static List<Element> GetActiveViewDucts(Document doc)
        {
            var activeView = doc.ActiveView;
            if (activeView == null)
                return new List<Element>();

            return new FilteredElementCollector(doc, activeView.Id)
                .OfCategory(BuiltInCategory.OST_DuctCurves)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        public static List<Element> GetSelectedDucts(UIDocument uidoc)
        {
            var doc = uidoc.Document;
            var selection = uidoc.Selection.GetElementIds();
            var ducts = new List<Element>();

            foreach (var id in selection)
            {
                var elem = doc.GetElement(id);
                if (elem == null) continue;
                var cat = elem.Category;
                if (cat != null && cat.Id.IntegerValue == (int)BuiltInCategory.OST_DuctCurves)
                    ducts.Add(elem);
            }

            return ducts;
        }
    }
}
