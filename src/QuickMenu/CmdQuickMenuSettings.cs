#region Metadata
/*
 * Tool Name     : AJ Quick Menu Settings
 * File Name     : CmdQuickMenuSettings.cs
 * Purpose       : Ribbon entry for customising the quick wheel - which AJ Tools button sits in each
 *                 slot, how many slots, and how big the wheel opens.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-18
 * Last Updated  : 2026-08-18
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : QuickMenuCatalog, QuickMenuConfig, QuickMenuSettingsWindow,
 *                 AJTools.Utils (DialogHelper)
 *
 * Input         : Nothing selected, no document required - this only edits a settings file.
 * Output        : %APPDATA%\AJTools\quickmenu-slots.txt. No model changes.
 *
 * Notes         :
 * - The tool list is refreshed from the live ribbon every time the window opens, so a button added
 *   in a newer AJ Tools release shows up without any change here.
 * - Also reachable by pressing S on the wheel itself.
 *
 * Changelog     :
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
    /// <summary>Opens the Quick Menu customise window.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickMenuSettings : IExternalCommand
    {
        internal const string ToolTitle = "AJ Quick Menu";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData != null ? commandData.Application : null;
            if (uiapp == null)
            {
                message = "Revit did not hand the Quick Menu an application to work with.";
                return Result.Failed;
            }

            return Show(uiapp);
        }

        /// <summary>Shows the customise window. Shared with the wheel's own "S" shortcut.</summary>
        internal static Result Show(UIApplication uiapp)
        {
            try
            {
                IList<QuickToolEntry> tools = QuickMenuCatalog.Refresh(uiapp);
                if (tools.Count == 0)
                {
                    DialogHelper.ShowError(
                        ToolTitle,
                        "No AJ Tools buttons could be read from the ribbon, so there is nothing to put " +
                        "on the wheel yet. Restart Revit and try again.");
                    return Result.Cancelled;
                }

                var window = new QuickMenuSettingsWindow(tools, QuickMenuConfig.Load());
                new WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(ToolTitle, "The Quick Menu settings could not open:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
