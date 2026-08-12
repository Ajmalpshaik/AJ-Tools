#region Metadata
/*
 * Tool Name     : Web Panel
 * File Name     : CmdToggleWebPanel.cs
 * Purpose       : Ribbon button that starts or stops the AJ Tools Web Panel - a local HTTP server
 *                 that puts AJ Tools buttons in a browser and runs them on the live model. Starting
 *                 it also opens the panel in the default browser.
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
 * Dependencies  : WebPanelService (via App.WebPanel), BridgeStatusToast, IconLoader
 *
 * Input         : None - a straight toggle.
 * Output        : Server started/stopped; browser opened on start; a status toast either way.
 *
 * Notes         :
 * - Nothing listens on any port until this button is clicked. The server is built at startup (so its
 *   ExternalEvent lands on Revit's UI thread) but stays dormant until then.
 * - The browser is launched with ProcessStartInfo.UseShellExecute = true, which is REQUIRED for a URL
 *   to open at all on .NET (Revit 2025+): the plain Process.Start(string) overload defaults
 *   UseShellExecute to false there and throws on anything that is not a real executable path. On .NET
 *   Framework (2020-2024) the same explicit form behaves identically, so one code path covers both.
 * - Failing to open the browser is NOT a failure of the command - the server is up and the address is
 *   in the toast, so the user can paste it themselves.
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Diagnostics;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.AiShell.Helpers;
using AJTools.Utils;

namespace AJTools.WebPanel.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdToggleWebPanel : IExternalCommand
    {
        // Placeholder art, shared with the AJ AI button until the Web Panel gets its own icon pair.
        private const string OnIconFile = "AJ_AI_ON.png";
        private const string OffIconFile = "AJ_AI_OFF.png";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var panel = AJTools.App.App.WebPanel;
            if (panel == null)
            {
                message = "The Web Panel is not available. Please restart Revit.";
                return Result.Failed;
            }

            try
            {
                if (panel.IsRunning)
                {
                    panel.Stop();
                    SetButtonIcon(running: false);
                    BridgeStatusToast.Show("Web Panel: Stopped", connected: false);
                    return Result.Succeeded;
                }

                string error;
                if (!panel.Start(out error))
                {
                    message = error;
                    return Result.Failed;
                }

                SetButtonIcon(running: true);
                BridgeStatusToast.Show("Web Panel: " + panel.Url, connected: true);
                OpenInBrowser(panel.Url);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static void OpenInBrowser(string url)
        {
            try
            {
                // UseShellExecute = true is what makes a URL (rather than an .exe path) valid here.
                // Required on .NET 8; harmless and identical in behaviour on .NET Framework.
                var startInfo = new ProcessStartInfo(url) { UseShellExecute = true };
                Process.Start(startInfo);
            }
            catch
            {
                // The server is running regardless, and the toast already shows the address - a
                // browser that refuses to launch must not report the whole command as failed.
            }
        }

        private static void SetButtonIcon(bool running)
        {
            var button = AJTools.App.App.WebPanelButton;
            if (button == null) return;

            var iconLoader = new IconLoader(Assembly.GetExecutingAssembly().Location);
            string fileName = running ? OnIconFile : OffIconFile;

            var largeIcon = iconLoader.LoadLarge(fileName);
            if (largeIcon != null) button.LargeImage = largeIcon;

            var smallIcon = iconLoader.LoadSmall(fileName);
            if (smallIcon != null) button.Image = smallIcon;
        }
    }
}
