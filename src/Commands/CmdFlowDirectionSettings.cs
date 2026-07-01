#region Metadata
/*
 * Tool Name     : Duct Flow Annotation Settings
 * File Name     : CmdFlowDirectionSettings.cs
 * Purpose       : Settings dialog that stores the annotation family and spacing used by Duct Flow Annotations.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-21
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.FlowDirection, AJTools.Models, AJTools.UI, AJTools.Utils
 *
 * Input         : Active project - annotation family and spacing chosen in the window.
 * Output        : Saved duct flow annotation settings (no model change).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Settings-only tool; does not modify the model.
 * - Window is owned by the Revit main window; cancel closes silently.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-21) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Settings behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
    /// Opens the settings dialog to store duct flow annotation preferences.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdFlowDirectionSettings : IExternalCommand
    {
        /// <summary>
        /// Executes the settings workflow.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError("Duct Flow", message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                {
                    DialogHelper.ShowError("Duct Flow", message);
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
                    DialogHelper.ShowError("Duct Flow", "Please select a valid annotation family and spacing.");
                    return Result.Cancelled;
                }

                settingsTracker.Save(new FlowDirectionSettingsState
                {
                    SymbolId = settingsWindow.SelectedSymbol.Id,
                    SpacingInternal = settingsWindow.SpacingInternal
                });

                DialogHelper.ShowInfo("Duct Flow", "Settings saved. Use 'Duct Flow' -> 'Place' to apply.");
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
