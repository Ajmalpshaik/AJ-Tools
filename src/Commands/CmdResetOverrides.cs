// Tool Name: Reset Overrides
// Description: Clears per-element graphic overrides in the active view.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Clears per-element graphic overrides in the active view.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(
        Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdResetOverrides : IExternalCommand
    {
        /// <summary>
        /// Executes the reset overrides workflow.
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
                    TaskDialog.Show("Reset Overrides", "Open a project view before running this command.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                View view = doc.ActiveView;

                if (view == null || view.IsTemplate)
                {
                    TaskDialog.Show("Reset Overrides", "Please run this tool inside a normal project view.");
                    return Result.Failed;
                }

                ICollection<ElementId> elementIds = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                if (elementIds.Count == 0)
                {
                    return Result.Succeeded;
                }

                // Reset per-element overrides for all elements in the active view.
                OverrideGraphicSettings resetSettings = new OverrideGraphicSettings();

                using (Transaction t = new Transaction(doc, "AJ Tools - Reset Overrides"))
                {
                    t.Start();

                    foreach (ElementId id in elementIds)
                    {
                        try
                        {
                            view.SetElementOverrides(id, resetSettings);
                        }
                        catch
                        {
                            // Ignore elements that cannot accept overrides.
                        }
                    }

                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Reset Overrides - Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
