#region Metadata
/*
 * Tool Name     : AJ Quick Menu
 * File Name     : CmdQuickMenu.cs
 * Purpose       : Ribbon entry for the quick tool wheel - the video-game style ring that opens
 *                 around the mouse pointer, holding Ajmal's own favourite AJ Tools buttons. Point
 *                 at one, click, and Revit runs that tool exactly as if its ribbon button had been
 *                 clicked.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-08-18
 * Last Updated  : 2026-08-20
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : QuickMenuCatalog / QuickMenuConfig / QuickMenuLauncher /
 *                 QuickMenuAvailability (Services.QuickMenu),
 *                 QuickMenuWindow (UI.QuickMenu), AJTools.Utils (DialogHelper)
 *
 * Input         : Nothing selected. Reads the saved wheel layout and the live ribbon.
 * Output        : The chosen tool runs. This command itself opens no transaction and changes
 *                 nothing in the model - whatever the chosen tool does is that tool's own work and
 *                 its own undo entry.
 *
 * Notes         :
 * - HOW TO GET IT UNDER THE POINTER. Clicking this ribbon button works, but then the wheel opens up
 *   by the ribbon where the pointer already is. To use it the way a game does - pointer out in the
 *   model, tap a key, wheel appears right there - give this button a Revit keyboard shortcut:
 *   File > Options > User Interface > Keyboard Shortcuts, search "Quick Menu", and assign something
 *   easy like QQ. Revit owns that shortcut list, so nothing has to be hooked or intercepted here.
 * - The wheel is shown with ShowDialog() on purpose. Revit can only be asked to run another command
 *   from inside a live command context, so this Execute() has to still be running when the choice
 *   comes back - see QuickMenuLauncher for the full reasoning.
 * - EVERY SLOT IS ASKED BEFORE THE WHEEL OPENS. Each tool is put the same question Revit puts to a
 *   ribbon button - "could this be clicked right now?" - and the answer is handed to the wheel so an
 *   unavailable tool is drawn greyed out rather than silently refusing when picked. The whole wheel
 *   is asked in ONE pass (QuickMenuAvailability.Evaluate) rather than slot by slot, because the
 *   selection has to be walked to answer and doing that per slot would make opening the wheel slow
 *   on a big selection - the very thing this version set out to fix.
 * - Pressing S on the wheel opens the customise window instead of running a tool.
 *
 * Changelog     :
 * v1.1.0 (2026-08-20) - Works out each slot's availability before opening the wheel, so a tool the
 *                       ribbon would grey out is shown greyed out here too.
 * v1.0.0 (2026-08-18) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.QuickMenu;
using AJTools.UI.QuickMenu;
using AJTools.Utils;

namespace AJTools.Commands.QuickMenu
{
    /// <summary>Opens the quick tool wheel at the mouse pointer and runs whatever is chosen.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickMenu : IExternalCommand
    {
        private const string ToolTitle = "AJ Quick Menu";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData != null ? commandData.Application : null;
            if (uiapp == null)
            {
                message = "Revit did not hand the Quick Menu an application to work with.";
                return Result.Failed;
            }

            try
            {
                QuickMenuConfig config = QuickMenuConfig.Load();
                IList<QuickToolEntry> tools = QuickMenuCatalog.GetEntries(uiapp);

                if (tools.Count == 0)
                {
                    DialogHelper.ShowError(
                        ToolTitle,
                        "No AJ Tools buttons could be read from the ribbon, so the wheel has nothing " +
                        "to show. Restart Revit and try again.");
                    return Result.Cancelled;
                }

                var slots = new List<QuickToolEntry>(config.Slots.Count);
                foreach (string key in config.Slots)
                {
                    slots.Add(QuickMenuCatalog.Find(uiapp, key));
                }

                IList<bool> available = QuickMenuAvailability.Evaluate(uiapp, slots);

                var wheel = new QuickMenuWindow(slots, available, config.Diameter);
                new WindowInteropHelper(wheel)
                {
                    Owner = uiapp.MainWindowHandle
                };

                wheel.ShowDialog();

                if (wheel.OpenSettingsRequested)
                {
                    return CmdQuickMenuSettings.Show(uiapp);
                }

                QuickToolEntry chosen = wheel.SelectedEntry;
                if (chosen == null)
                {
                    return Result.Cancelled;
                }

                string error;
                if (!QuickMenuLauncher.TryRun(uiapp, chosen, out error))
                {
                    DialogHelper.ShowError(ToolTitle, error);
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(ToolTitle, "The Quick Menu could not open:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
