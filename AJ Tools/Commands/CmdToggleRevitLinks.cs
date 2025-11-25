using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace AJTools
{
    [Autodesk.Revit.Attributes.Transaction(
        Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdToggleRevitLinks : IExternalCommand
    {
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
                    TaskDialog.Show("Linked Models Toggle",
                        "No active document. Please open a project and try again.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                View view = doc.ActiveView;

                // Only active project view (not templates)
                if (view == null || view.IsTemplate)
                {
                    TaskDialog.Show("Linked Models Toggle",
                        "Please run this tool in a normal project view (plan/section/3D), not a template.");
                    return Result.Failed;
                }

                // Revit Links category
                ElementId linksCategoryId = new ElementId(BuiltInCategory.OST_RvtLinks);

                // Current state in THIS view
                bool isCurrentlyHidden = view.GetCategoryHidden(linksCategoryId);

                using (Transaction t = new Transaction(doc, "Toggle All Revit Links"))
                {
                    t.Start();
                    view.SetCategoryHidden(linksCategoryId, !isCurrentlyHidden);
                    t.Commit();
                }

                // Silent success
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AJ Tools - Error",
                    "An error occurred while toggling Revit links:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
