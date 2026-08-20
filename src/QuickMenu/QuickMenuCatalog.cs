#region Metadata
/*
 * Tool Name     : AJ Quick Menu (tool catalog)
 * File Name     : QuickMenuCatalog.cs
 * Purpose       : Builds the list of everything the Quick Menu can put in a slot. Two sources:
 *                 (1) the LIVE AJ Tools / AJ Annotation ribbon - every push button on it, and
 *                 (2) Revit's own built-in commands. Nothing is hard-coded either side: add a new
 *                 button to a ribbon manager and it appears by itself, with its real label, tooltip
 *                 and icon; Revit's own list comes straight from the running Revit version.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-08-18
 * Last Updated  : 2026-08-20
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit UI API (UIApplication.GetRibbonPanels, RibbonPanel.GetItems,
 *                 PulldownButton.GetItems, PushButton.ClassName/ItemText/ToolTip/LargeImage,
 *                 PostableCommand), System.Reflection (for the control id - see Notes)
 *
 * Input         : The running UIApplication.
 * Output        : A cached read-only list of QuickToolEntry. No model changes, ever.
 *
 * Notes         :
 * - REVIT'S OWN COMMANDS ARE READ FROM THE RUNNING VERSION, NOT LISTED BY HAND. Revit publishes its
 *   built-in commands as the PostableCommand enum, and that enum is different in every release - a
 *   command written into the code by name would stop the whole project compiling on the versions
 *   that never had it. So the names are walked with Enum.GetNames(typeof(PostableCommand)) at run
 *   time: only the enum's TYPE name is written in the code, never a member. One source file then
 *   builds correctly for Revit 2020 through 2027 and each one offers exactly the commands it has.
 * - Revit hands add-ins no icon for its own commands, so those entries carry none and the wheel
 *   draws the name on its own. The name is the enum member split into words ("WallArchitectural"
 *   becomes "Wall Architectural"), which is what Ajmal searches the customise list by.
 * - HOW THE CONTROL ID IS FOUND. Revit gives every ribbon button an internal command id string
 *   ("CustomCtrl_%CustomCtrl_%AJ Tools%View%cmdCmdUnhideAll") and that string is the ONLY way to
 *   launch an add-in command programmatically (UIApplication.PostCommand). It is not exposed on the
 *   Revit API RibbonItem, but every RibbonItem carries a non-public instance method getRibbonItem()
 *   that hands back the underlying Autodesk.Windows ribbon item, whose Id property IS that string.
 *   This is the same route pyRevit has used in production since Revit 2019, and it means the id is
 *   READ from the live ribbon rather than guessed - so a renamed panel or button can never silently
 *   break the Quick Menu. Reflection is used (rather than referencing AdWindows.dll) so no new
 *   package reference is added to a project that builds eight Revit versions.
 *   Everything is wrapped: if the method or property ever disappears, ControlId comes back null and
 *   QuickMenuLauncher rebuilds the string from the tab/panel/button names instead.
 * - AVAILABILITY IS READ OFF THE BUTTON TOO. Revit greys a ribbon button out by consulting the
 *   class named in PushButton.AvailabilityClassName. That property is public and readable and is
 *   identical in Revit 2020 and 2027 (verified from the installed RevitAPIUI.dll metadata, not from
 *   the shipped XML docs - those are wrong about neighbouring members). Capturing it here is what
 *   lets the wheel grey a slot out for exactly the same reason the panel greys the button, instead
 *   of posting a command Revit will silently refuse.
 * - The catalog is built once per Revit session and cached. Refresh() exists for the customise
 *   window, which should always show what is on the ribbon right now.
 * - The Quick Menu's own two buttons are left out on purpose - a wheel slot that opens the wheel
 *   would be a loop.
 *
 * Changelog     :
 * v1.2.0 (2026-08-20) - Reads each button's AvailabilityClassName, so the wheel can mirror the
 *                       ribbon's greyed-out state. Caches the getRibbonItem lookup per button type.
 * v1.1.0 (2026-08-19) - Added Revit's own built-in commands as a second source, so a wheel slot can
 *                       hold a Revit command as well as an AJ Tools button.
 * v1.0.0 (2026-08-18) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Autodesk.Revit.UI;

namespace AJTools.Services.QuickMenu
{
    /// <summary>Every AJ Tools ribbon button plus every Revit built-in command, ready for the wheel.</summary>
    internal static class QuickMenuCatalog
    {
        /// <summary>The two AJ Tools ribbon tabs the Quick Menu offers tools from.</summary>
        private static readonly string[] TabNames = { "AJ Tools", "AJ Annotation" };

        /// <summary>Command classes that must never appear as a slot (they open the wheel itself).</summary>
        private static readonly string[] ExcludedClassNames =
        {
            "AJTools.Commands.QuickMenu.CmdQuickMenu",
            "AJTools.Commands.QuickMenu.CmdQuickMenuSettings"
        };

        private static List<QuickToolEntry> _entries;

        /// <summary>
        /// getRibbonItem() is looked up by reflection once per ribbon-item type, not once per
        /// button - the lookup is the expensive half and every PushButton shares one type.
        /// </summary>
        private static readonly Dictionary<Type, MethodInfo> _getRibbonItemCache =
            new Dictionary<Type, MethodInfo>();

        /// <summary>
        /// All launchable AJ Tools buttons, built once per session and cached.
        /// </summary>
        internal static IList<QuickToolEntry> GetEntries(UIApplication uiapp)
        {
            if (_entries == null)
            {
                _entries = Build(uiapp);
            }

            return _entries;
        }

        /// <summary>Rebuilds the list from the ribbon as it stands right now.</summary>
        internal static IList<QuickToolEntry> Refresh(UIApplication uiapp)
        {
            _entries = Build(uiapp);
            return _entries;
        }

        /// <summary>Finds one entry by its saved key (command class name), or null.</summary>
        internal static QuickToolEntry Find(UIApplication uiapp, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            foreach (QuickToolEntry entry in GetEntries(uiapp))
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static List<QuickToolEntry> Build(UIApplication uiapp)
        {
            var entries = new List<QuickToolEntry>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (uiapp == null)
            {
                return entries;
            }

            foreach (string tabName in TabNames)
            {
                IList<RibbonPanel> panels;
                try
                {
                    panels = uiapp.GetRibbonPanels(tabName);
                }
                catch (Exception)
                {
                    // Tab not present in this session - skip it rather than fail the whole catalog.
                    continue;
                }

                if (panels == null)
                {
                    continue;
                }

                foreach (RibbonPanel panel in panels)
                {
                    AddPanel(entries, seenKeys, tabName, panel);
                }
            }

            AddRevitCommands(entries, seenKeys);

            return entries;
        }

        /// <summary>
        /// Adds every built-in command the running Revit version publishes. See the Notes block:
        /// the enum is walked by name at run time so one source file builds on 2020 through 2027.
        /// </summary>
        private static void AddRevitCommands(List<QuickToolEntry> entries, HashSet<string> seenKeys)
        {
            string[] commandNames;
            try
            {
                commandNames = Enum.GetNames(typeof(PostableCommand));
            }
            catch (Exception)
            {
                // No built-in list available - the AJ Tools buttons on their own are still usable.
                return;
            }

            if (commandNames == null)
            {
                return;
            }

            foreach (string commandName in commandNames)
            {
                if (string.IsNullOrEmpty(commandName))
                {
                    continue;
                }

                if (!seenKeys.Add(QuickToolEntry.RevitKeyPrefix + commandName))
                {
                    continue;
                }

                PostableCommand commandValue;
                try
                {
                    commandValue = (PostableCommand)Enum.Parse(typeof(PostableCommand), commandName);
                }
                catch (Exception)
                {
                    continue;
                }

                entries.Add(QuickToolEntry.ForRevitCommand(
                    commandName,
                    SplitIntoWords(commandName),
                    commandValue));
            }
        }

        /// <summary>"WallArchitectural" -> "Wall Architectural", so the list reads like English.</summary>
        private static string SplitIntoWords(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length + 8);

            for (int i = 0; i < name.Length; i++)
            {
                char current = name[i];

                if (i > 0 && char.IsUpper(current))
                {
                    char previous = name[i - 1];
                    bool afterLowerOrDigit = char.IsLower(previous) || char.IsDigit(previous);
                    bool endOfRunOfCapitals = char.IsUpper(previous) &&
                                              i + 1 < name.Length &&
                                              char.IsLower(name[i + 1]);

                    if (afterLowerOrDigit || endOfRunOfCapitals)
                    {
                        builder.Append(' ');
                    }
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static void AddPanel(
            List<QuickToolEntry> entries,
            HashSet<string> seenKeys,
            string tabName,
            RibbonPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            IList<RibbonItem> items;
            try
            {
                items = panel.GetItems();
            }
            catch (Exception)
            {
                return;
            }

            if (items == null)
            {
                return;
            }

            foreach (RibbonItem item in items)
            {
                var pushButton = item as PushButton;
                if (pushButton != null)
                {
                    Add(entries, seenKeys, tabName, panel.Name, null, null, pushButton);
                    continue;
                }

                // SplitButton derives from PulldownButton, so this one branch covers both.
                var pulldownButton = item as PulldownButton;
                if (pulldownButton == null)
                {
                    continue;
                }

                IList<PushButton> childButtons;
                try
                {
                    childButtons = pulldownButton.GetItems();
                }
                catch (Exception)
                {
                    continue;
                }

                if (childButtons == null)
                {
                    continue;
                }

                string groupName = Flatten(SafeItemText(pulldownButton));
                string groupItemName = SafeName(pulldownButton);

                foreach (PushButton childButton in childButtons)
                {
                    Add(entries, seenKeys, tabName, panel.Name, groupName, groupItemName, childButton);
                }
            }
        }

        private static void Add(
            List<QuickToolEntry> entries,
            HashSet<string> seenKeys,
            string tabName,
            string panelName,
            string groupName,
            string groupItemName,
            PushButton pushButton)
        {
            if (pushButton == null)
            {
                return;
            }

            string itemName = SafeName(pushButton);
            string className = SafeClassName(pushButton);
            string key = string.IsNullOrEmpty(className)
                ? tabName + "|" + panelName + "|" + itemName
                : className;

            if (string.IsNullOrEmpty(key) || !seenKeys.Add(key))
            {
                return;
            }

            foreach (string excluded in ExcludedClassNames)
            {
                if (string.Equals(className, excluded, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            string displayName = Flatten(SafeItemText(pushButton));
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = itemName;
            }

            entries.Add(QuickToolEntry.ForRibbonButton(
                key,
                displayName,
                SafeToolTip(pushButton),
                tabName,
                panelName,
                groupName,
                itemName,
                groupItemName,
                TryGetControlId(pushButton),
                SafeAvailabilityClassName(pushButton),
                SafeIcon(pushButton)));
        }

        /// <summary>
        /// Revit's own command id for this ribbon item, read from the underlying Autodesk.Windows
        /// item (see the Notes block above). Null when it cannot be read - never throws.
        /// </summary>
        private static string TryGetControlId(RibbonItem item)
        {
            try
            {
                MethodInfo getRibbonItem = GetRibbonItemMethod(item.GetType());

                if (getRibbonItem == null)
                {
                    return null;
                }

                object adWindowsItem = getRibbonItem.Invoke(item, null);
                if (adWindowsItem == null)
                {
                    return null;
                }

                PropertyInfo idProperty = adWindowsItem.GetType().GetProperty("Id");
                if (idProperty == null)
                {
                    return null;
                }

                return idProperty.GetValue(adWindowsItem, null) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The cached getRibbonItem() lookup for one ribbon-item type. Null when absent.</summary>
        private static MethodInfo GetRibbonItemMethod(Type itemType)
        {
            MethodInfo cached;
            if (_getRibbonItemCache.TryGetValue(itemType, out cached))
            {
                return cached;
            }

            MethodInfo found = itemType.GetMethod(
                "getRibbonItem",
                BindingFlags.NonPublic | BindingFlags.Instance);

            _getRibbonItemCache[itemType] = found;
            return found;
        }

        private static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Trim();
        }

        private static string SafeName(RibbonItem item)
        {
            try
            {
                return item.Name ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string SafeItemText(RibbonItem item)
        {
            try
            {
                return item.ItemText ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string SafeToolTip(RibbonItem item)
        {
            try
            {
                return item.ToolTip ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string SafeClassName(PushButton pushButton)
        {
            try
            {
                return pushButton.ClassName ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// The class Revit uses to grey this button out, or empty when it is never greyed.
        /// </summary>
        private static string SafeAvailabilityClassName(PushButton pushButton)
        {
            try
            {
                return pushButton.AvailabilityClassName ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static System.Windows.Media.ImageSource SafeIcon(PushButton pushButton)
        {
            try
            {
                return pushButton.LargeImage ?? pushButton.Image;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
