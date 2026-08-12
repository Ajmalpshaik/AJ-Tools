#region Metadata
/*
 * Tool Name     : AJ Connect (ribbon command)
 * File Name     : CmdOpenConnect.cs
 * Purpose       : The one button a colleague ever presses. Opens the AJ Connect window, where they
 *                 connect or disconnect and open the tools page.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-12
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : ConnectWindow, ConnectorServer via ConnectorApp.Server
 *
 * Input         : None.
 * Output        : Shows the AJ Connect window; the ribbon icon is refreshed to match the state on close.
 *
 * Notes         :
 * - WindowInteropHelper Owner is set to Revit's main window before showing. Without it the window can
 *   drop behind Revit, and because it sets ShowInTaskbar="False" there would then be no way to get it
 *   back short of killing Revit.
 * - Modal on purpose. The window is a short interaction - connect, open the page, close - and a modal
 *   dialog needs none of the lifetime and threading care a modeless one does in a Revit add-in.
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Initial release. Replaces CmdToggleConnector, which toggled the server
 *                       straight from the ribbon with no window.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJToolsConnector.UI;

namespace AJToolsConnector
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOpenConnect : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var server = ConnectorApp.Server;
            if (server == null)
            {
                message = "AJ Connect is not available. Please restart Revit.";
                return Result.Failed;
            }

            try
            {
                var window = new ConnectWindow(server);
                new WindowInteropHelper(window) { Owner = commandData.Application.MainWindowHandle };
                window.ShowDialog();

                // The window may have connected or disconnected - make the ribbon agree.
                ConnectorApp.ApplyIcon(server.IsRunning);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
