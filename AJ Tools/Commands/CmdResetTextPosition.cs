using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    [Transaction(TransactionMode.Manual)]
    public class CmdResetTextPosition : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            if (uidoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TaskDialog.Show("Reset Text Position", "Select text notes or tags to reset their text offset.");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            int resetCount = 0;

            using (Transaction t = new Transaction(doc, "Reset Text Position"))
            {
                t.Start();

                foreach (ElementId id in selectedIds)
                {
                    Element el = doc.GetElement(id);
                    if (el == null)
                        continue;

                    // Many text-bearing annotations derive from TextElement; use reflection to set Coord when available.
                    if (el is TextElement)
                    {
                        PropertyInfo coordProp = el.GetType().GetProperty("Coord", BindingFlags.Public | BindingFlags.Instance);
                        if (coordProp != null && coordProp.CanWrite)
                        {
                            coordProp.SetValue(el, XYZ.Zero, null);
                            resetCount++;
                            continue;
                        }
                    }
                }

                t.Commit();
            }

            if (resetCount == 0)
            {
                TaskDialog.Show("Reset Text Position", "No supported text elements were reset. Select text notes or tags with editable text offsets.");
                return Result.Cancelled;
            }

            return Result.Succeeded;
        }
    }
}
