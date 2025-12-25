// Tool Name: Set Link Workset
// Description: Assigns selected Revit links and CAD imports to a chosen or new workset.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-23
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using AJTools.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens the Set Link Workset dialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSetLinkWorkset : IExternalCommand
    {
        private const string Title = "Set Link Workset";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
            {
                DialogHelper.ShowError(Title, message);
                return Result.Cancelled;
            }

            var window = new SetLinkWorksetWindow(uidoc.Document);
            var helper = new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
