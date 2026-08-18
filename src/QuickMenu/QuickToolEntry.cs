#region Metadata
/*
 * Tool Name     : AJ Quick Menu (tool entry)
 * File Name     : QuickToolEntry.cs
 * Purpose       : One AJ Tools ribbon button as the Quick Menu sees it - the name, icon and tooltip
 *                 to draw on the wheel, plus everything needed to launch it again (the Revit
 *                 control id, and the tab/panel/button names it can be rebuilt from).
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
 * Dependencies  : System.Windows.Media (ImageSource - the ribbon button's own icon is reused as is)
 *
 * Input         : Built by QuickMenuCatalog from the live ribbon - never constructed by hand.
 * Output        : Plain read-only data. Touches no model and no UI.
 *
 * Notes         :
 * - Key is the external command CLASS name (e.g. AJTools.Commands.CmdUnhideAll). That is what the
 *   saved slot file stores, so renaming a button's LABEL never loses Ajmal's slot layout.
 * - ControlId is Revit's own command id string ("CustomCtrl_%CustomCtrl_%AJ Tools%View%cmd...").
 *   It can be null on an unusual ribbon item; QuickMenuLauncher then rebuilds it from the names.
 *
 * Changelog     :
 * v1.0.0 (2026-08-18) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Windows.Media;

namespace AJTools.Services.QuickMenu
{
    /// <summary>One launchable AJ Tools ribbon button, as shown on the Quick Menu wheel.</summary>
    internal sealed class QuickToolEntry
    {
        public QuickToolEntry(
            string key,
            string displayName,
            string tooltip,
            string tabName,
            string panelName,
            string groupName,
            string itemName,
            string groupItemName,
            string controlId,
            ImageSource icon)
        {
            Key = key;
            DisplayName = displayName;
            Tooltip = tooltip;
            TabName = tabName;
            PanelName = panelName;
            GroupName = groupName;
            ItemName = itemName;
            GroupItemName = groupItemName;
            ControlId = controlId;
            Icon = icon;
        }

        /// <summary>Stable identity used by the saved slot file - the command class full name.</summary>
        public string Key { get; }

        /// <summary>Button label with line breaks flattened, e.g. "Unhide All".</summary>
        public string DisplayName { get; }

        /// <summary>The ribbon tooltip, shown in the Quick Menu hub while hovering.</summary>
        public string Tooltip { get; }

        /// <summary>"AJ Tools" or "AJ Annotation".</summary>
        public string TabName { get; }

        /// <summary>Ribbon panel label, e.g. "View".</summary>
        public string PanelName { get; }

        /// <summary>Parent split/pulldown button label, or null for a top-level button.</summary>
        public string GroupName { get; }

        /// <summary>Internal ribbon item name (PushButtonData's first argument).</summary>
        public string ItemName { get; }

        /// <summary>Internal name of the parent split/pulldown button, or null.</summary>
        public string GroupItemName { get; }

        /// <summary>Revit's own command id string, read from the live ribbon. May be null.</summary>
        public string ControlId { get; }

        /// <summary>The button's own ribbon icon, reused as is - no separate Quick Menu art.</summary>
        public ImageSource Icon { get; }

        /// <summary>"View - Unhide All" style label for the customise window's tool list.</summary>
        public string ListLabel
        {
            get
            {
                string where = string.IsNullOrEmpty(GroupName)
                    ? PanelName
                    : PanelName + " / " + GroupName;
                return where + "  -  " + DisplayName;
            }
        }
    }
}
