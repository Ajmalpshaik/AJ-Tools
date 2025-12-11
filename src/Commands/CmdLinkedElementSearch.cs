// Tool Name: Linked Element Search
// Description: Searches by Element ID in the host or linked models and zooms to the result.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdLinkedElementSearch : IExternalCommand
    {
        private const string Title = "Linked Element Search";

        /// <summary>
        /// Opens the Linked Element Search dialog to locate elements by ID in host or linked documents.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;

                if (uiDoc == null)
                {
                    TaskDialog.Show(Title, "Please open a project before running this tool.");
                    return Result.Failed;
                }

                Document doc = uiDoc.Document;
                View activeView = doc.ActiveView;

                if (activeView == null || activeView.IsTemplate)
                {
                    TaskDialog.Show(Title, "Run this command in a project view (not a template).");
                    return Result.Failed;
                }

                if (activeView.ViewType == ViewType.DrawingSheet ||
                    activeView.ViewType == ViewType.DraftingView)
                {
                    TaskDialog.Show(Title, "Linked element search is not available on sheets or drafting views.");
                    return Result.Failed;
                }

                IList<RevitLinkInstance> linkInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(l => l != null && l.GetLinkDocument() != null)
                    .ToList();

                var searchWindow = new LinkedSearchWindow(uiDoc, doc, activeView, linkInstances);
                searchWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(Title, "An error occurred:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
