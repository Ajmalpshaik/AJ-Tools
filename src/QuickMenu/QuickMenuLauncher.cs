#region Metadata
/*
 * Tool Name     : AJ Quick Menu (tool launcher)
 * File Name     : QuickMenuLauncher.cs
 * Purpose       : Runs the tool Ajmal picked on the wheel, exactly as if he had clicked its ribbon
 *                 button - same command, same dialogs, same undo entry.
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
 * Dependencies  : Autodesk Revit UI API (RevitCommandId.LookupCommandId, UIApplication.PostCommand)
 *
 * Input         : One QuickToolEntry chosen on the wheel.
 * Output        : That tool runs. This file itself never opens a transaction or touches the model.
 *
 * Notes         :
 * - WHY PostCommand AND NOT A DIRECT CALL. An IExternalCommand can only be executed by Revit: its
 *   Execute() needs an ExternalCommandData, and that class cannot be constructed by an add-in. The
 *   supported route is UIApplication.PostCommand(RevitCommandId), which queues the command to run
 *   the moment the current one finishes - which is exactly why CmdQuickMenu shows the wheel with
 *   ShowDialog() and posts afterwards, still inside its own Execute().
 * - Because the tool is POSTED rather than called, it runs in its own clean Revit command context.
 *   Ajmal's own undo entry, availability rules (greyed-out buttons) and dialogs all behave the same
 *   as clicking the ribbon.
 * - The id string is normally read straight off the live ribbon (QuickMenuCatalog). The two
 *   rebuilt-from-names candidates below are a belt-and-braces fallback for the case where that read
 *   ever comes back empty; Revit composes these ids as
 *   "CustomCtrl_%CustomCtrl_%<tab>%<panel>%<button>", with one extra CustomCtrl_% level for a
 *   button that lives inside a split/pulldown.
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
using Autodesk.Revit.UI;

namespace AJTools.Services.QuickMenu
{
    /// <summary>Posts the chosen AJ Tools command so Revit runs it like a normal ribbon click.</summary>
    internal static class QuickMenuLauncher
    {
        /// <summary>
        /// Queues <paramref name="entry"/> to run. Must be called from inside a command's Execute,
        /// while Revit is in a valid API context.
        /// </summary>
        /// <returns>True if the tool was handed to Revit; false with a plain-language reason.</returns>
        internal static bool TryRun(UIApplication uiapp, QuickToolEntry entry, out string error)
        {
            error = string.Empty;

            if (uiapp == null || entry == null)
            {
                error = "No tool was chosen.";
                return false;
            }

            RevitCommandId commandId = ResolveCommandId(entry);
            if (commandId == null)
            {
                error = "Revit could not find the \"" + entry.DisplayName + "\" button on the ribbon, " +
                        "so the Quick Menu could not start it. Open the Quick Menu settings and pick " +
                        "the tool again from the list.";
                return false;
            }

            try
            {
                uiapp.PostCommand(commandId);
                return true;
            }
            catch (Exception ex)
            {
                // Revit refuses a second posted command while one is already queued, and refuses any
                // post from outside a command context. Both surface here as a plain message.
                error = "Revit would not start \"" + entry.DisplayName + "\" right now (" + ex.Message +
                        "). Finish whatever command is running and try again.";
                return false;
            }
        }

        private static RevitCommandId ResolveCommandId(QuickToolEntry entry)
        {
            foreach (string candidate in BuildCandidateIds(entry))
            {
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                try
                {
                    RevitCommandId commandId = RevitCommandId.LookupCommandId(candidate);
                    if (commandId != null)
                    {
                        return commandId;
                    }
                }
                catch (Exception)
                {
                    // An unknown id string just means "not this one" - try the next candidate.
                }
            }

            return null;
        }

        private static IEnumerable<string> BuildCandidateIds(QuickToolEntry entry)
        {
            // 1. The id read straight off the live ribbon - right in every normal case.
            yield return entry.ControlId;

            if (string.IsNullOrEmpty(entry.TabName) ||
                string.IsNullOrEmpty(entry.PanelName) ||
                string.IsNullOrEmpty(entry.ItemName))
            {
                yield break;
            }

            // 2. Rebuilt for a button sitting inside a split or pulldown button.
            if (!string.IsNullOrEmpty(entry.GroupItemName))
            {
                yield return "CustomCtrl_%CustomCtrl_%CustomCtrl_%" +
                             entry.TabName + "%" + entry.PanelName + "%" +
                             entry.GroupItemName + "%" + entry.ItemName;
            }

            // 3. Rebuilt for a plain top-level (or stacked) button.
            yield return "CustomCtrl_%CustomCtrl_%" +
                         entry.TabName + "%" + entry.PanelName + "%" + entry.ItemName;
        }
    }
}
