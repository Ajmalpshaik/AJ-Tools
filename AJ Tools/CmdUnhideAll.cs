using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    [Autodesk.Revit.Attributes.Transaction(
        Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdUnhideAll : IExternalCommand
    {
        private const string TITLE = "Unhide All Elements in Active View";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null)
                {
                    TaskDialog.Show(TITLE, "Open a project view before running this command.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                View view = doc.ActiveView;

                if (view == null || view.IsTemplate)
                {
                    TaskDialog.Show(TITLE, "Please run this tool inside a normal project view.");
                    return Result.Failed;
                }

                var ids = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                if (ids == null || ids.Count == 0)
                    return Result.Succeeded;

                using (Transaction t = new Transaction(doc, TITLE))
                {
                    t.Start();
                    view.UnhideElements(ids);
                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(TITLE, ex.Message);
                return Result.Failed;
            }
        }
    }
}
