// Tool Name: Toggle Revit Links
// Description: Toggles the visibility of all Revit link instances in the active view.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Toggles link category visibility in the active view.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(
        Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdToggleRevitLinks : IExternalCommand
    {
        /// <summary>
        /// Toggles link category visibility in the active view.
        /// </summary>
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
                    TaskDialog.Show("Toggle Revit Links",
                        "No active project open.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                View view = doc.ActiveView;

                if (view == null || view.IsTemplate)
                {
                    TaskDialog.Show("Toggle Revit Links",
                        "Run the tool in a normal project view.");
                    return Result.Failed;
                }

                // Rvt Links category
                ElementId linksCategoryId =
                    new ElementId(BuiltInCategory.OST_RvtLinks);

                bool isHidden = view.GetCategoryHidden(linksCategoryId);

                using (Transaction t = new Transaction(doc, "Toggle Revit Links"))
                {
                    t.Start();
                    view.SetCategoryHidden(linksCategoryId, !isHidden);
                    t.Commit();
                }

                // No final popup as requested
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("AJ Tools - Error",
                    "Error while toggling Revit Links:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
