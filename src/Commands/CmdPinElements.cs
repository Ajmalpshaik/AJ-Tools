// Tool Name: Pin Elements
// Description: Pins or unpins fixed element groups selected from a dialog.
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2026-04-18
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.UI, AJTools.Services.PinTools

using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens a fixed-group selector and applies pin or unpin.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdPinElements : IExternalCommand
    {
        private const string Title = "Pin Elements";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
            {
                DialogHelper.ShowError(Title, message);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                DialogHelper.ShowError(Title, message);
                return Result.Cancelled;
            }

            var window = new PinElementsWindow(doc);
            new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true && !window.HasExecutedOperation)
                return Result.Cancelled;
            return Result.Succeeded;
        }
    }
}
