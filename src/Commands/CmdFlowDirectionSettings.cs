// Tool Name: Flow Direction Settings
// Description: Stores the flow direction annotation family and spacing for reuse.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-21
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.FlowDirection

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using AJTools.Models;
using AJTools.Services.FlowDirection;
using AJTools.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens the settings dialog to store flow direction annotation preferences.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdFlowDirectionSettings : IExternalCommand
    {
        /// <summary>
        /// Executes the settings workflow.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
            {
                DialogHelper.ShowError("Flow Direction", message);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                DialogHelper.ShowError("Flow Direction", message);
                return Result.Cancelled;
            }

            var settingsTracker = new FlowDirectionSettingsTracker(doc);
            var settingsWindow = new FlowDirectionSettingsWindow(doc, settingsTracker.LastState);
            var helper = new WindowInteropHelper(settingsWindow)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            bool? dialogResult = settingsWindow.ShowDialog();
            if (dialogResult != true)
                return Result.Cancelled;

            if (settingsWindow.SelectedSymbol == null || settingsWindow.SpacingInternal <= 1e-6)
            {
                DialogHelper.ShowError("Flow Direction", "Please select a valid annotation family and spacing.");
                return Result.Cancelled;
            }

            settingsTracker.Save(new FlowDirectionSettingsState
            {
                SymbolId = settingsWindow.SelectedSymbol.Id,
                SpacingInternal = settingsWindow.SpacingInternal
            });

            DialogHelper.ShowInfo("Flow Direction", "Settings saved. Use 'Flow Direction' -> 'Place' to apply.");
            return Result.Succeeded;
        }
    }
}
