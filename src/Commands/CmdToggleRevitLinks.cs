// Tool Name: Toggle Revit Links
// Description: Toggles the visibility of all Revit link instances in the active view.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace AJTools.Commands
{
    /// <summary>
    /// Toggles link category visibility in the active view.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdToggleRevitLinks : IExternalCommand
    {
        /// <summary>
        /// Toggles link category visibility in the active view.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null)
                {
                    TaskDialog.Show("Linked Models Toggle", "No active document. Please open a project and try again.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                View view = doc.ActiveView;

                if (view == null || view.IsTemplate)
                {
                    TaskDialog.Show("Linked Models Toggle", "Please run this tool in a normal project view (plan/section/3D), not a template.");
                    return Result.Failed;
                }

                ElementId linksCategoryId = new ElementId(BuiltInCategory.OST_RvtLinks);
                bool isCurrentlyHidden = view.GetCategoryHidden(linksCategoryId);

                using (Transaction t = new Transaction(doc, "Toggle All Revit Links"))
                {
                    t.Start();
                    view.SetCategoryHidden(linksCategoryId, !isCurrentlyHidden);
                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AJ Tools - Error", "An error occurred while toggling Revit links:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
