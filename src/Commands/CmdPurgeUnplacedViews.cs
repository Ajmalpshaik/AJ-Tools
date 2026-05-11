// ==================================================
// Tool Name    : Purge Unplaced 3D Views and Sections
// Purpose      : Convert Python shell purge workflow into AJ Tools C# Revit add-in.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-11
// Last Updated : 2026-05-11
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Converted from Interactive Python Shell script.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI.Purge;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPurgeUnplacedViews : IExternalCommand
    {
        private const string ToolTitle = "Purge Unplaced 3D Views and Sections";
        private const string ProjectOnlyMessage = "This tool works only in an opened Revit project file.";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDoc = commandData?.Application?.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document == null)
                {
                    TaskDialog.Show(ToolTitle, ProjectOnlyMessage);
                    return Result.Cancelled;
                }

                Document doc = uiDoc.Document;
                if (doc.IsFamilyDocument)
                {
                    TaskDialog.Show(ToolTitle, ProjectOnlyMessage);
                    return Result.Cancelled;
                }

                ElementId activeViewId = uiDoc.ActiveView != null
                    ? uiDoc.ActiveView.Id
                    : ElementId.InvalidElementId;

                var window = new PurgeUnplacedViewsWindow(doc, activeViewId);
                window.ShowDialog();

                return window.OperationWasRun ? Result.Succeeded : Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show(ToolTitle, "Tool failed:\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
