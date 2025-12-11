// Tool Name: Unhide All
// Description: Unhides all elements in the active view (temporary hide and hidden items).
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Unhides all elements in the active view (temporary hide and hidden items).
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdUnhideAll : IExternalCommand
    {
        private const string Title = "Unhide All Elements in Active View";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                if (uiDoc == null)
                    return Result.Failed;

                Document doc = uiDoc.Document;
                View view = doc.ActiveView;

                if (view == null || view.IsTemplate)
                    return Result.Failed;

                // Same logic as your pyRevit script: collect all elements in the model
                var allElementIds = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                using (Transaction t = new Transaction(doc, Title))
                {
                    t.Start();

                    // Unhide all elements in the active view
                    view.UnhideElements(allElementIds);

                    // Try to reset Temporary Hide/Isolate (ignore if not active / not supported)
                    try
                    {
                        view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    }
                    catch
                    {
                        // No action needed
                    }

                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch
            {
                return Result.Failed;
            }
        }
    }
}
