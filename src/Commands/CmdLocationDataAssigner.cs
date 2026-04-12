// Tool Name: Location Data Assigner Command
// Description: Launches a dialog to assign room, level, coordinate, altitude, and HVAC data.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-09
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.UI

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;
using AJTools.Utils;
using System.Windows.Interop;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens the Location Data Assigner dialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdLocationDataAssigner : IExternalCommand
    {
        private const string Title = "Location Data Assigner";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
            {
                DialogHelper.ShowError(Title, message);
                return Result.Cancelled;
            }

            if (!ValidationHelper.ValidateEditableDocument(uidoc.Document, out message))
            {
                DialogHelper.ShowError(Title, message);
                return Result.Cancelled;
            }

            var window = new LocationDataAssignerWindow(uidoc.Document);
            var helper = new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
