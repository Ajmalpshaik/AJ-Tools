#region Metadata
/*
 * Tool Name     : Connect MEP Elements - Settings
 * File Name     : CmdSmartConnectSettings.cs
 * Purpose       : Settings dialog for Connect MEP Elements. Stores how the tool routes, what it is
 *                 allowed to pick, which element may be trimmed, how batches behave, and what is
 *                 copied onto the pieces it creates.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models, AJTools.Services.SmartConnect, AJTools.UI
 *
 * Input         : Routing, picking, trimming, batch and quality options chosen in the window.
 * Output        : Saved Connect MEP Elements settings (no model change).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Settings-only tool; does not modify the model.
 * - Cancel closes silently and leaves the saved settings untouched.
 * - The window is owned by the Revit main window so it cannot drop behind Revit.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release. Splits the settings out of the run command, so the ribbon
 *                       button connects straight away instead of opening a dialog every time.
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
using AJTools.Models;
using AJTools.Services.SmartConnect;
using AJTools.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens the Connect MEP Elements settings window and saves the result.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSmartConnectSettings : IExternalCommand
    {
        private const string ToolTitle = "Connect MEP Elements - Settings";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var settingsService = new SmartConnectSettingsService();
                SmartConnectSettings currentSettings = settingsService.Load();

                var settingsWindow = new SmartConnectWindow(currentSettings);
                new WindowInteropHelper(settingsWindow)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

                settingsWindow.ShowDialog();

                if (!settingsWindow.Confirmed)
                {
                    return Result.Cancelled;
                }

                settingsService.Save(settingsWindow.Settings);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show(ToolTitle, "Could not open the settings:" + Environment.NewLine + ex.Message);
                return Result.Failed;
            }
        }
    }
}
