#region Metadata
/*
 * Tool Name     : AJ Quick Menu (tool entry)
 * File Name     : QuickToolEntry.cs
 * Purpose       : One launchable thing as the Quick Menu sees it - either an AJ Tools ribbon button
 *                 or one of Revit's own built-in commands - with the name, icon and tooltip to draw
 *                 on the wheel plus everything needed to start it again.
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
 * Dependencies  : System.Windows.Media (ImageSource - a ribbon button's own icon is reused as is),
 *                 Autodesk Revit UI API (PostableCommand - the identity of a built-in Revit command)
 *
 * Input         : Built by QuickMenuCatalog - never constructed by hand.
 * Output        : Plain read-only data. Touches no model and no UI.
 *
 * Notes         :
 * - TWO KINDS OF ENTRY, told apart by Source:
 *     AjTools - one of Ajmal's own ribbon buttons. Key is the external command CLASS name
 *               (e.g. AJTools.Commands.CmdUnhideAll), so renaming a button's LABEL never loses the
 *               saved slot layout. ControlId is Revit's own command id read off the live ribbon.
 *     Revit   - one of Revit's built-in commands. Key is "revit:" + the PostableCommand name
 *               (e.g. revit:Undo). The colon cannot occur in a C# class name, so the two kinds of
 *               key can never collide in the saved file.
 * - Revit commands carry no icon - Revit does not hand its ribbon artwork to add-ins - so the wheel
 *   simply draws the name on its own for those slots.
 * - AVAILABILITY IS CARRIED, NOT DECIDED, HERE. AvailabilityClassName is the same class Revit itself
 *   consults to decide whether to grey the ribbon button out. It is read off the live button by
 *   QuickMenuCatalog and asked the question by QuickMenuAvailability - this file only carries the
 *   name, so the wheel can grey a slot for exactly the same reason the panel greys the button.
 *   Empty means the button is never greyed out. Revit's own commands carry none: Revit decides
 *   those for itself when the command is posted.
 *
 * Changelog     :
 * v1.2.0 (2026-08-20) - Carries the button's AvailabilityClassName, so the wheel can grey a slot out
 *                       for the same reason the ribbon panel greys the button.
 * v1.1.0 (2026-08-19) - Added Source and PostableCommandValue so Revit's own commands can sit in a
 *                       slot beside AJ Tools' buttons. Construction moved to two named factories.
 * v1.0.0 (2026-08-18) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Windows.Media;
using Autodesk.Revit.UI;

namespace AJTools.Services.QuickMenu
{
    /// <summary>Where a Quick Menu entry came from - and therefore how it gets launched.</summary>
    internal enum QuickToolSource
    {
        /// <summary>A button on the AJ Tools or AJ Annotation ribbon tab.</summary>
        AjTools = 0,

        /// <summary>One of Revit's own built-in commands.</summary>
        Revit = 1
    }

    /// <summary>One launchable tool or command, as shown on the Quick Menu wheel.</summary>
    internal sealed class QuickToolEntry
    {
        /// <summary>Prefix that marks a saved key as one of Revit's own commands.</summary>
        internal const string RevitKeyPrefix = "revit:";

        private QuickToolEntry(
            QuickToolSource source,
            string key,
            string displayName,
            string tooltip,
            string tabName,
            string panelName,
            string groupName,
            string itemName,
            string groupItemName,
            string controlId,
            string availabilityClassName,
            PostableCommand? postableCommandValue,
            ImageSource icon)
        {
            Source = source;
            Key = key;
            DisplayName = displayName;
            Tooltip = tooltip;
            TabName = tabName;
            PanelName = panelName;
            GroupName = groupName;
            ItemName = itemName;
            GroupItemName = groupItemName;
            ControlId = controlId;
            AvailabilityClassName = availabilityClassName;
            PostableCommandValue = postableCommandValue;
            Icon = icon;
        }

        /// <summary>Builds an entry for one of Ajmal's own ribbon buttons.</summary>
        internal static QuickToolEntry ForRibbonButton(
            string key,
            string displayName,
            string tooltip,
            string tabName,
            string panelName,
            string groupName,
            string itemName,
            string groupItemName,
            string controlId,
            string availabilityClassName,
            ImageSource icon)
        {
            return new QuickToolEntry(
                QuickToolSource.AjTools,
                key,
                displayName,
                tooltip,
                tabName,
                panelName,
                groupName,
                itemName,
                groupItemName,
                controlId,
                availabilityClassName,
                null,
                icon);
        }

        /// <summary>Builds an entry for one of Revit's own built-in commands.</summary>
        internal static QuickToolEntry ForRevitCommand(
            string commandName,
            string displayName,
            PostableCommand commandValue)
        {
            return new QuickToolEntry(
                QuickToolSource.Revit,
                RevitKeyPrefix + commandName,
                displayName,
                "Revit's own \"" + displayName + "\" command, started exactly as if you had clicked it " +
                "on Revit's ribbon.",
                "Revit",
                "Revit command",
                null,
                commandName,
                null,
                null,
                null,
                commandValue,
                null);
        }

        /// <summary>Whether this is an AJ Tools button or one of Revit's own commands.</summary>
        public QuickToolSource Source { get; }

        /// <summary>Stable identity saved in the slot file - a command class name, or "revit:Name".</summary>
        public string Key { get; }

        /// <summary>Label with line breaks flattened, e.g. "Unhide All".</summary>
        public string DisplayName { get; }

        /// <summary>Tooltip shown in the Quick Menu hub while hovering.</summary>
        public string Tooltip { get; }

        /// <summary>"AJ Tools", "AJ Annotation", or "Revit" for a built-in command.</summary>
        public string TabName { get; }

        /// <summary>Ribbon panel label, e.g. "View"; "Revit command" for a built-in command.</summary>
        public string PanelName { get; }

        /// <summary>Parent split/pulldown button label, or null.</summary>
        public string GroupName { get; }

        /// <summary>Internal ribbon item name, or the PostableCommand name for a Revit command.</summary>
        public string ItemName { get; }

        /// <summary>Internal name of the parent split/pulldown button, or null.</summary>
        public string GroupItemName { get; }

        /// <summary>Revit's own command id string for an AJ Tools button. Null otherwise.</summary>
        public string ControlId { get; }

        /// <summary>
        /// The class Revit consults to decide whether this button is greyed out on the ribbon.
        /// Empty when the button is always enabled, and always empty for a Revit command.
        /// </summary>
        public string AvailabilityClassName { get; }

        /// <summary>The built-in command to post, for a Revit entry. Null for an AJ Tools button.</summary>
        public PostableCommand? PostableCommandValue { get; }

        /// <summary>The button's own ribbon icon. Always null for a Revit command.</summary>
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
