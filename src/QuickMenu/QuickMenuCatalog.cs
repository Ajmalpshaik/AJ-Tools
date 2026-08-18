#region Metadata
/*
 * Tool Name     : AJ Quick Menu (tool catalog)
 * File Name     : QuickMenuCatalog.cs
 * Purpose       : Reads the LIVE AJ Tools / AJ Annotation ribbon and turns every push button on it
 *                 into a QuickToolEntry the wheel can show and launch. Nothing is hard-coded: add a
 *                 new button to either ribbon manager and it appears in the Quick Menu list by
 *                 itself, with its real label, tooltip and icon.
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
 * Dependencies  : Autodesk Revit UI API (UIApplication.GetRibbonPanels, RibbonPanel.GetItems,
 *                 PulldownButton.GetItems, PushButton.ClassName/ItemText/ToolTip/LargeImage),
 *                 System.Reflection (for the control id - see Notes)
 *
 * Input         : The running UIApplication.
 * Output        : A cached read-only list of QuickToolEntry. No model changes, ever.
 *
 * Notes         :
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
 * - The catalog is built once per Revit session and cached. Refresh() exists for the customise
 *   window, which should always show what is on the ribbon right now.
 * - The Quick Menu's own two buttons are left out on purpose - a wheel slot that opens the wheel
 *   would be a loop.
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
using System.Reflection;
using Autodesk.Revit.UI;

namespace AJTools.Services.QuickMenu
{
    /// <summary>Every AJ Tools ribbon button, read from the live ribbon, ready for the wheel.</summary>
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

            return entries;
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

            entries.Add(new QuickToolEntry(
                key,
                displayName,
                SafeToolTip(pushButton),
                tabName,
                panelName,
                groupName,
                itemName,
                groupItemName,
                TryGetControlId(pushButton),
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
                MethodInfo getRibbonItem = item.GetType().GetMethod(
                    "getRibbonItem",
                    BindingFlags.NonPublic | BindingFlags.Instance);

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
