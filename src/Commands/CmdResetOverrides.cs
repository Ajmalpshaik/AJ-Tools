using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    [Autodesk.Revit.Attributes.Transaction(
        Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdResetOverrides : IExternalCommand
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

                // We'll attempt to reset overrides for each element; skip those that throw.
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
                            // Skip elements that cannot accept overrides (e.g., some imported categories).
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
