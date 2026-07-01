#region Metadata
/*
 * Tool Name     : Find Element by Element ID (Linked Element Search)
 * File Name     : CmdLinkedElementSearch.cs
 * Purpose       : Searches for an element by its Element ID in the host model or any loaded linked model
 *                 and zooms the active view to it.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-07
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI (LinkedSearchWindow)
 *
 * Input         : Active project view - an Element ID typed in the window; loaded links are searched too.
 * Output        : The matching element highlighted and zoomed to. Read-only tool, no transaction.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Runs in a project view (not sheets, drafting views, or templates); reads linked models, never modifies them.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.1 (2025-12-10) - Host and linked search with zoom-to-result.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Search behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
