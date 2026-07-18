#region Metadata
/*
 * Tool Name     : AJ AI (ribbon toggle)
 * File Name     : ToggleAiBridgeCommand.cs
 * Purpose       : Standalone "AI Assistant" ribbon button ("AJ AI") that connects/disconnects the
 *                 AJ AI Bridge (McpBridgeService) without opening the "C#" chat panel. Moved
 *                 out of the panel's own Connect/Disconnect control on Ajmal's request (2026-07-18)
 *                 so the bridge can be controlled independently of the coding/chat window.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-07-18
 * Last Updated  : 2026-07-18
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : AJTools.App.App.AiBridge / AiBridgeButton (shared state set in App.OnStartup via
 *                 AiShellPaneProvider + RibbonManager), BridgeStatusToast, IconLoader
 *
 * Input         : Ribbon button click.
 * Output        : Starts/stops the named-pipe bridge; swaps the button's own icon (AJ_AI_ON.png /
 *                 AJ_AI_OFF.png) to reflect the new state; brief on-screen toast as well.
 *
 * Notes         :
 * - Reuses the SAME McpBridgeService instance the C# pane owns (via the static App.AiBridge
 *   reference set at startup) - does not create a second bridge/pipe.
 * - A plain Revit PushButton has no built-in persistent on/off visual state by itself, so this command
 *   updates the captured PushButton's LargeImage/Image directly after each toggle (App.AiBridgeButton,
 *   set once by RibbonManager's afterCreate callback when the ribbon is built) - the two icon files
 *   ARE the on/off state indicator here, not just decoration. The BridgeStatusToast is a secondary,
 *   momentary confirmation on top of that persistent icon change.
 * - A genuine start failure (e.g. AppData permission issue) still goes through Revit's own
 *   Result.Failed + message mechanism, same as every other command in this project - not the toast.
 *
 * Changelog     :
 * v1.2.0 (2026-07-18) - Icon files renamed .jpg -> .png (AJ_AI_ON.png / AJ_AI_OFF.png): Ajmal's
 *                       original JPG exports had a solid background box instead of transparency; he
 *                       re-exported as PNG the same day, fixing that.
 * v1.1.0 (2026-07-18) - Renamed from "AJ AI Bridge" to "AJ AI" to match the ribbon button's new label
 *                       (the interactive chat panel took over "AJ AI Bridge"'s old spot as "C# with
 *                       AI" instead). Added dynamic icon swap (AJ_AI_ON.jpg / AJ_AI_OFF.jpg, sourced
 *                       from Ajmal) on the button itself after each connect/disconnect, replacing the
 *                       single static chain-link icon used before.
 * v1.0.0 (2026-07-18) - Initial release: split the AJ AI Bridge connect/disconnect control out of the
 *                       AJ AI chat panel into its own ribbon button.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.AiShell.Helpers;
using AJTools.Utils;

namespace AJTools.AiShell.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ToggleAiBridgeCommand : IExternalCommand
    {
        private const string OnIconFile = "AJ_AI_ON.png";
        private const string OffIconFile = "AJ_AI_OFF.png";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var bridge = AJTools.App.App.AiBridge;
            if (bridge == null)
            {
                message = "AJ AI is not available. Please restart Revit.";
                return Result.Failed;
            }

            try
            {
                if (bridge.IsRunning)
                {
                    bridge.Stop();
                    SetButtonIcon(connected: false);
                    BridgeStatusToast.Show("AJ AI: Disconnected", connected: false);
                    return Result.Succeeded;
                }

                if (!bridge.Start(out string error))
                {
                    message = error;
                    return Result.Failed;
                }

                SetButtonIcon(connected: true);
                BridgeStatusToast.Show("AJ AI: Connected", connected: true);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static void SetButtonIcon(bool connected)
        {
            var button = AJTools.App.App.AiBridgeButton;
            if (button == null) return;

            var iconLoader = new IconLoader(Assembly.GetExecutingAssembly().Location);
            string fileName = connected ? OnIconFile : OffIconFile;

            var largeIcon = iconLoader.LoadLarge(fileName);
            if (largeIcon != null) button.LargeImage = largeIcon;

            var smallIcon = iconLoader.LoadSmall(fileName);
            if (smallIcon != null) button.Image = smallIcon;
        }
    }
}
