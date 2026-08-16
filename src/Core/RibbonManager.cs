#region Metadata
/*
 * Tool Name     : AJ Tools Ribbon Manager
 * File Name     : RibbonManager.cs
 * Purpose       : Builds the main "AJ Tools" ribbon tab - its panels (View, Graphics, Datums, Modify, MEP,
 *                 Coordination, Data, Manage, Family, AI, About) and every button, split, and pulldown.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.13.6
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Commands, AJTools.Utils (IconLoader)
 *
 * Input         : UIControlledApplication (Revit startup).
 * Output        : The AJ Tools ribbon tab with all panels and buttons registered.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Centralizes ribbon registration and icon loading.
 * - To rename a panel, edit _panelNames; to move a tool group, change its PanelKey in _toolLayout.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.13.6 (2026-07-29) - Game Mode audit pass: tooltip now covers the SELECTOR weapon and
 *                       professional mode (N) - both shipped in suite 1.38.0 but never reached the
 *                       tooltip; stale folder note by AddGameModeTool corrected (the tool lives in
 *                       src/GameMode since the 1.34.1 restructure). No behaviour change.
 * v1.13.5 (2026-07-29) - Game Mode tooltip: measuring removed (feature deleted per Ajmal), key
 *                       settings + graphics reset mentioned.
 * v1.13.4 (2026-07-29) - Game Mode tooltip: section-box mention removed (feature deleted per
 *                       Ajmal), saved-positions list mentioned.
 * v1.13.3 (2026-07-29) - Game Mode tooltip updated for the v1.34.0 "add all" round (teleport,
 *                       positions, photo, cleaner weapon, section box, holster).
 * v1.13.2 (2026-07-28) - Game Mode tooltip now also mentions the laser hold-and-release
 *                       face-to-face measuring (v1.32.0 feature).
 * v1.13.1 (2026-07-28) - Game Mode tooltip updated for the v1.31.0 weapon rework (hold-to-fire,
 *                       right-click gun/laser switch, laser identifies elements; L key removed).
 * v1.13.0 (2026-07-28) - New "Game" panel (between AI Assistant and About) with the single "Game Mode"
 *                       button (CmdGameMode) - the first-person walkthrough game. Self-contained: the
 *                       panel entry + AddGameModeTool here are the only ribbon touch points.
 * v1.12.0 (2026-07-21) - Correction on top of v1.11.0/v1.10.0 below: Ajmal watched the "last-used-first"
 *                       sync behavior live and didn't want it - "Create Openings" and "Run Pinned" should
 *                       stay the PERMANENT default face, never swap to "Opening Settings"/"Saved Scripts"
 *                       just because one of those ran. Both split buttons now set
 *                       IsSynchronizedWithCurrentItem = false instead of true - this is what actually
 *                       keeps the top face fixed on the first-added child forever (per RevitAPIUI.xml:
 *                       "if false, the first listed PushButton... executes this PushButton when clicked"
 *                       - items after it are reachable only via the dropdown). The App.MepOpeningSplitButton
 *                       / CreateOpeningsButton / OpeningSettingsButton / RunPinnedSplitButton /
 *                       RunPinnedButton / SavedScriptsButton statics and the per-child afterCreate capture
 *                       hooks are gone with it - nothing needs to set CurrentButton anymore (doing so
 *                       while IsSynchronizedWithCurrentItem is false actually throws).
 * v1.11.0 (2026-07-21) - AI Assistant panel: "Run Pinned" and "Saved Scripts" combined into one split
 *                       button, same pattern as the Opening split button (v1.10.0 below) - Run Pinned
 *                       is the default face (added first), Saved Scripts lives in the dropdown, and
 *                       the top face tracks whichever of the two was actually run last via new
 *                       App.RunPinnedSplitButton / RunPinnedButton / SavedScriptsButton statics.
 * v1.10.0 (2026-07-21) - View panel decluttered: Filter Pro, Colorize, and Highlight Selection moved
 *                       from 3 separate top-level buttons into one small stacked group (matching the
 *                       View Crop/Unhide/Toggle Links stack); Section Mark Visibility moved off this
 *                       tab entirely, onto the AJ Annotation tab's Tags panel (AnnotationRibbonManager.cs).
 *                       Opening split button (Opening panel): "Create Openings" is now the default face
 *                       (added first) and CreateSplitToolSpec gained an optional configureSplitButton
 *                       hook so it captures itself + its two child buttons into new App.MepOpeningSplitButton
 *                       / CreateOpeningsButton / OpeningSettingsButton statics - CmdCreateMepOpenings and
 *                       CmdMepOpeningSettings each set SplitButton.CurrentButton to themselves as the
 *                       first thing they do, so the ribbon's top face always reflects whichever of the
 *                       two was actually run last (native SplitButton.IsSynchronizedWithCurrentItem
 *                       behavior, set explicitly rather than relying on its Revit-side default).
 * v1.9.0 (2026-07-21) - Added "Saved Scripts" to the AI Assistant panel (ShowSavedScriptsCommand) -
 *                       standalone browse/pin/run window for every .cs file in the Scripts Folder,
 *                       moved out of the C# pane's "Saved Scripts History" expander per Ajmal's
 *                       request so it works whether or not the C# pane is open, same as Run Pinned.
 * v1.8.0 (2026-07-21) - Added "Run Pinned" to the AI Assistant panel (RunPinnedScriptCommand) - runs
 *                       whichever saved script is pinned from the C# pane's Saved Scripts History.
 *                       Statically-compiled, no runtime code generation - see that command's own
 *                       notes for why this is the safe alternative to RevitPythonShell's IL-emission
 *                       based "deploy script as ribbon button" feature.
 * v1.7.1 (2026-07-20) - Smart Selection tooltip/description text updated for the v1.1.0 single-shot
 *                       box-select behavior (see CmdSmartSelection.cs) - no more "Finish" step.
 * v1.7.0 (2026-07-20) - Added the Smart Selection tool (Modify panel): pick one reference element,
 *                       then window/crossing/click-select more - only elements sharing that
 *                       element's category are added to the selection.
 * v1.6.1 (2026-07-18) - Two small same-day fixes on top of v1.6.0 below: (1) Ajmal's AJ AI ON/OFF
 *                       source images had a solid (non-transparent) background from the original JPG
 *                       export - he re-exported as PNG, so the icon files are now AJ_AI_ON.png /
 *                       AJ_AI_OFF.png (renamed from .jpg to match the actual format). (2) Shortened
 *                       the chat button's label from "C# with AI" to just "C#" per Ajmal's request.
 * v1.6.0 (2026-07-18) - Rebranded the AI Assistant panel's two buttons per Ajmal-supplied art
 *                       (Y:\Ajmal Ps\icon): the chat/code-generation button is now "C# with AI"
 *                       (CSharp_with_AI.png, was "AJ AI"), and the bridge toggle is now just "AJ AI"
 *                       (was "AJ AI Bridge") - swapped identities from the v1.5.0 entry below since
 *                       the "AJ AI" name moved to the bridge button. The AJ AI button now starts on
 *                       AJ_AI_OFF.jpg and captures its own PushButton into App.AiBridgeButton so
 *                       ToggleAiBridgeCommand can swap it to AJ_AI_ON.jpg / back after each click.
 * v1.5.0 (2026-07-18) - Added a second button to the AI Assistant panel: "AJ AI Bridge"
 *                       (ToggleAiBridgeCommand), a standalone connect/disconnect toggle for the live-
 *                       Revit MCP bridge that previously only lived inside the AJ AI chat panel. Own
 *                       dedicated icon (AJ_AI_Bridge.png, a chain-link glyph) rather than reusing the
 *                       AJ AI sparkle icon, so the two buttons stay visually distinct at a glance.
 * v1.2.0 (2026-05-07) - Reorganized ribbon panels; added HVAC schematic registration.
 * v1.3.0 (2026-07-01) - Refactor/audit: standardized metadata block. Ribbon layout unchanged.
 * v1.3.1 (2026-07-01) - Full audit fixes: wired CmdPurgeUnusedFamilyParametersAvailability into the
 *                       Purge Family Parameters button (was defined but never assigned); renamed the
 *                       "Aj tool" panel label to "About" for consistent casing.
 * v1.4.0 (2026-07-13) - Wired the Colorize tool (View panel, next to Filter Pro) - it previously
 *                       existed only in the stale pre-multiversion "AJ Tools\" tree and was never
 *                       part of this live project, so it could never appear on the ribbon.
 * v1.4.1 (2026-07-13) - Updated the Pin / Unpin Elements tooltip to mention the new Grids and Levels
 *                       groups (actual collection logic lives in PinElementsService).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using AJTools.Commands;
using AJTools.Commands.GraphicsTools;
using AJTools.Utils;

namespace AJTools.App
{
    /// <summary>
    /// Builds the AJ Tools ribbon tab, panels, and buttons when Revit starts.
    /// </summary>
    internal class RibbonManager
    {
        private enum PanelKey
        {
            View,
            Graphics,
            Datums,
            Modify,
            Mep,
            Opening,
            Coordination,
            Data,
            Manage,
            Family,
            Ai,
            Game,
            About
        }

        private readonly struct ToolPlacement
        {
            public ToolPlacement(PanelKey panelKey, Action<RibbonPanel> buildTools)
            {
                PanelKey = panelKey;
                BuildTools = buildTools;
            }

            public PanelKey PanelKey { get; }
            public Action<RibbonPanel> BuildTools { get; }
        }

        private readonly UIControlledApplication _app;
        private readonly string _assemblyPath;
        private readonly IconLoader _iconLoader;
        private readonly IReadOnlyDictionary<PanelKey, string> _panelNames;
        private readonly IReadOnlyList<PanelKey> _panelOrder;
        private readonly IReadOnlyList<ToolPlacement> _toolLayout;

        private const string TabName = "AJ Tools";

        /// <summary>
        /// Initializes a new RibbonManager bound to the current Revit application.
        /// </summary>
        public RibbonManager(UIControlledApplication app)
        {
            _app = app;
            _assemblyPath = Assembly.GetExecutingAssembly().Location;
            _iconLoader = new IconLoader(_assemblyPath);

            _panelNames = new Dictionary<PanelKey, string>
            {
                [PanelKey.View] = "View",
                [PanelKey.Graphics] = "Graphics",
                [PanelKey.Datums] = "Datums",
                [PanelKey.Family] = "Family",
                [PanelKey.Modify] = "Modify",
                [PanelKey.Mep] = "MEP",
                [PanelKey.Opening] = "Opening",
                [PanelKey.Coordination] = "Coordination",
                [PanelKey.Data] = "Data",
                [PanelKey.Manage] = "Manage",
                [PanelKey.Ai] = "AI Assistant",
                [PanelKey.Game] = "Game",
                [PanelKey.About] = "About"
            };

            _panelOrder = new List<PanelKey>
            {
                PanelKey.View,
                PanelKey.Graphics,
                PanelKey.Datums,
                PanelKey.Modify,
                PanelKey.Mep,
                PanelKey.Opening,
                PanelKey.Coordination,
                PanelKey.Data,
                PanelKey.Manage,
                PanelKey.Family,
                PanelKey.Ai,
                PanelKey.Game,
                PanelKey.About
            };

            // To rename a panel, update _panelNames.
            // To move a tool group to another panel, change the PanelKey used here.
            _toolLayout = new List<ToolPlacement>
            {
                new ToolPlacement(PanelKey.View, BuildViewPanel),
                new ToolPlacement(PanelKey.Graphics, BuildGraphicsPanel),
                new ToolPlacement(PanelKey.Datums, BuildDatumsPanel),
                new ToolPlacement(PanelKey.Modify, BuildModifyPanel),
                new ToolPlacement(PanelKey.Mep, BuildMepPanel),
                new ToolPlacement(PanelKey.Opening, BuildOpeningPanel),
                new ToolPlacement(PanelKey.Coordination, BuildCoordinationPanel),
                new ToolPlacement(PanelKey.Data, BuildDataPanel),
                new ToolPlacement(PanelKey.Manage, BuildManagePanel),
                new ToolPlacement(PanelKey.Family, BuildFamilyPanel),
                new ToolPlacement(PanelKey.Ai, BuildAiPanel),
                new ToolPlacement(PanelKey.Game, BuildGamePanel),
                new ToolPlacement(PanelKey.About, BuildAboutPanel)
            };
        }

        /// <summary>
        /// Creates the AJ Tools tab with grouped panels and buttons.
        /// </summary>
        public void CreateRibbon()
        {
            try
            {
                _app.CreateRibbonTab(TabName);
            }
            catch (Exception)
            {
                // Tab already exists - safe to continue building panels.
            }

            var panels = CreatePanels();
            AddToolsToPanels(panels);
        }

        private Dictionary<PanelKey, RibbonPanel> CreatePanels()
        {
            var panels = new Dictionary<PanelKey, RibbonPanel>();
            foreach (var panelKey in _panelOrder)
            {
                panels[panelKey] = GetOrCreatePanel(_panelNames[panelKey]);
            }

            return panels;
        }

        private sealed class TopLevelToolSpec
        {
            public TopLevelToolSpec(RibbonItemData data, Action<RibbonItem> configureItem)
            {
                Data = data;
                ConfigureItem = configureItem ?? (_ => { });
            }

            public RibbonItemData Data { get; }
            public Action<RibbonItem> ConfigureItem { get; }
        }

        private sealed class SplitChildToolSpec
        {
            public SplitChildToolSpec(
                string text,
                string tooltip,
                Type command,
                string largeIconFileName,
                string smallIconFileName,
                Action<PushButton> afterCreate)
            {
                Text = text;
                Tooltip = tooltip;
                Command = command;
                LargeIconFileName = largeIconFileName;
                SmallIconFileName = smallIconFileName;
                AfterCreate = afterCreate;
            }

            public string Text { get; }
            public string Tooltip { get; }
            public Type Command { get; }
            public string LargeIconFileName { get; }
            public string SmallIconFileName { get; }
            public Action<PushButton> AfterCreate { get; }
        }

        private void AddToolsToPanels(IReadOnlyDictionary<PanelKey, RibbonPanel> panels)
        {
            foreach (var placement in _toolLayout)
            {
                placement.BuildTools(panels[placement.PanelKey]);
            }
        }

        private void BuildViewPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddViewCropTools(), AddUnhideAllTool(), AddToggleLinksTool());
            AddStackedTools(panel, AddFilterProTool(), AddColorizeTool(), AddHighlightSelectionTool());
        }

        private void BuildGraphicsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddApplyGraphicsTools(), AddMatchGraphicsTools(), AddResetGraphicsTools());
        }

        private void BuildDatumsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddResetDatumsTools(), AddLevelExtentsTools(), AddFlipDatumBubblesTool());
        }

        private void BuildModifyPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddMatchElevationTool(), AddReassignLevelTool(), AddPinElementsTool());
            AddTopLevelTool(panel, AddSmartSelectionTool());
        }

        private void BuildMepPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddSmartConnectTool(), AddCeilingMagnetTool(), AddHvacSchematicTool());
            AddTopLevelTool(panel, AddPipeSizingTool());
        }

        private void BuildOpeningPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddMepOpeningsTool());
        }

        private void BuildCoordinationPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddFindElementTools(), AddWorkset3DViewsTool(), AddSetLinkWorksetTool());
        }

        private void BuildDataPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddLocationDataTool(), AddDuctStandardsTool());
        }

        private void BuildManagePanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddTransferTools());
            AddTopLevelTool(panel, AddPurgeTools());
        }

        private void BuildFamilyPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddConvertSharedParametersTool());
        }

        private void BuildAiPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddAiTool());
            AddTopLevelTool(panel, AddAiBridgeTool());
            AddTopLevelTool(panel, AddRunPinnedTool());
        }

        private void BuildGamePanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddGameModeTool());
        }

        private void BuildAboutPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddAboutTool());
        }

        private TopLevelToolSpec AddGameModeTool()
        {
            // The whole Game Mode tool is self-contained (src/GameMode + the Game* images in
            // Resources + this one entry) so it can be removed cleanly if it is ever unwanted.
            return CreatePushToolSpec(
                "Game\nMode",
                "Walk inside the model like a video game, in a real Revit perspective view " +
                "(\"AJ Game View\") - so all Visibility/Graphics, filters and section boxes still apply. " +
                "WASD to walk, mouse to look, Shift to run fast, Space to jump (jump through windows), " +
                "E to go through doors, F to fly, G for ghost mode (through walls). " +
                "Hold left-click for automatic gunfire with impact sparks; right-click cycles the " +
                "weapons - LASER (live distance in mm + element identity), CLEANER (temporarily hides " +
                "what you shoot, U restores), SNAG MARKER (paints it red + a report on exit; " +
                "J resets all colors in the game view) and SELECTOR (shoots elements into the live " +
                "Revit selection - it stays after exit). More: T teleports with a jump arc, B + 1-9 " +
                "save and revisit positions (O tours them all), K saves a clean photo, N hides the " +
                "gun for meetings (professional mode), and every shortcut key can be changed - " +
                "pause (Esc) and press S for Key Settings. Esc pauses (Revit stays usable), Esc " +
                "again exits - or click this button again to stop the game.",
                typeof(AJTools.Commands.GameMode.CmdGameMode),
                "GameMode.png",
                "GameMode.png");
        }

        private TopLevelToolSpec AddAiTool()
        {
            return CreatePushToolSpec(
                "C#",
                "Open the interactive AI-powered C# shell for Revit.",
                typeof(AJTools.AiShell.Commands.ShowAiShellCommand),
                "CSharp_with_AI.png",
                "CSharp_with_AI.png");
        }

        private TopLevelToolSpec AddAiBridgeTool()
        {
            // Starts on the OFF icon - the bridge never auto-connects on startup. ToggleAiBridgeCommand
            // swaps this same button's icon to AJ_AI_ON.png / AJ_AI_OFF.png after each click, via the
            // PushButton reference captured below into App.AiBridgeButton.
            return CreatePushToolSpec(
                "AJ AI",
                "Connect or disconnect AJ AI - lets an external AI agent (via MCP) run C# against this " +
                "live Revit document, without opening the C# panel. Click again to disconnect.",
                typeof(AJTools.AiShell.Commands.ToggleAiBridgeCommand),
                "AJ_AI_OFF.png",
                "AJ_AI_OFF.png",
                pushButton => App.AiBridgeButton = pushButton);
        }

        private TopLevelToolSpec AddRunPinnedTool()
        {
            // "Run Pinned" is the permanent default face - a single click always runs it directly, same
            // one-click statically-compiled equivalent of RevitPythonShell's "deploy script as ribbon
            // button" it always was (see RunPinnedScriptCommand for why). "Saved Scripts" (browse/pin
            // from the configured Scripts Folder) only lives in the dropdown - it never takes over the
            // main face. IsSynchronizedWithCurrentItem = false is what keeps the top face fixed on
            // "Run Pinned" (the first-added child) no matter which child was actually run last.
            return CreateSplitToolSpec(
                "Run\nPinned",
                "Run the saved C# script currently pinned in \"Saved Scripts\". Open Saved Scripts and click \"📌 Pin\" on one to choose which.",
                "CSharp_with_AI.png",
                "CSharp_with_AI.png",
                splitButton => splitButton.IsSynchronizedWithCurrentItem = false,
                CreateSplitChildTool(
                    "Run\nPinned",
                    "Run the saved C# script currently pinned in \"Saved Scripts\". Open Saved Scripts and click \"📌 Pin\" on one to choose which.",
                    typeof(AJTools.AiShell.Commands.RunPinnedScriptCommand),
                    "CSharp_with_AI.png",
                    "CSharp_with_AI.png"),
                CreateSplitChildTool(
                    "Saved\nScripts",
                    "Browse every saved C# script in your Scripts Folder. Pin one to \"Run Pinned\", or run it directly from here.",
                    typeof(AJTools.AiShell.Commands.ShowSavedScriptsCommand),
                    "CSharp_with_AI.png",
                    "CSharp_with_AI.png"));
        }

        private TopLevelToolSpec AddToggleLinksTool()
        {
            return CreatePushToolSpec(
                "Toggle\nLink",
                "Toggle visibility of all Revit Links in the active view.",
                typeof(CmdToggleRevitLinks),
                "Toggle Links.png",
                "Toggle Links.png",
                pushButton => pushButton.AvailabilityClassName = typeof(CmdRevitLinkToggleAvailability).FullName);
        }

        private TopLevelToolSpec AddUnhideAllTool()
        {
            return CreatePushToolSpec(
                "Unhide\nAll",
                "Unhide all elements in the active view (Temporary Hide/Isolate + hidden items).",
                typeof(CmdUnhideAll),
                "Unhide All.png",
                "Unhide All.png",
                pushButton => pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName);
        }

        private TopLevelToolSpec AddSmartSelectionTool()
        {
            return CreatePushToolSpec(
                "Smart\nSelection",
                "Pick one reference element, then one window or crossing-select - only elements of the same category are added.",
                typeof(CmdSmartSelection),
                "cursor.png",
                "cursor.png",
                pushButton =>
                {
                    pushButton.LongDescription = "Pick a reference element (e.g. one duct), then window-select or crossing-select once across the view - only elements sharing that category are added; everything else caught in the box is skipped automatically. The matched elements stay selected for whatever you do next.";
                });
        }

        private TopLevelToolSpec AddPinElementsTool()
        {
            return CreatePushToolSpec(
                "Pin / Unpin\nElements",
                "Pin/unpin separated Sheet groups (Title Blocks, Placed Views, Legends, Schedules) with Active Sheet Only or All Sheets mode, and Model groups (Duct, Pipe, Cable Tray, Generic Models, Mechanical Equipment, Plumbing Fixtures, Electrical Equipment, Grids, Levels).",
                typeof(CmdPinElements),
                "apply.png",
                "apply.png");
        }

        private TopLevelToolSpec AddFilterProTool()
        {
            return CreatePushToolSpec(
                "Filter\nPro",
                "Create parameter filters quickly (category, parameter, values) and apply them to the active view.",
                typeof(CmdFilterPro),
                "FilterPro.png",
                "FilterPro.png",
                pushButton => pushButton.AvailabilityClassName = typeof(CmdFilterProAvailability).FullName);
        }

        private TopLevelToolSpec AddColorizeTool()
        {
            return CreatePushToolSpec(
                "Colorize",
                "Colorize elements by category or by parameter value directly in the active view (or selected views) - no view filter is created.",
                typeof(CmdColorize),
                "Colorize.png",
                "Colorize.png",
                pushButton =>
                {
                    pushButton.LongDescription = "Pick categories (and optionally a parameter and values, using the same category/parameter/value engine as Filter Pro), choose graphics options, then Shuffle Colors applies the overrides directly to matched elements in the active view or selected views - click it again anytime to re-shuffle.";
                    pushButton.AvailabilityClassName = typeof(CmdColorizeAvailability).FullName;
                });
        }

        private TopLevelToolSpec AddHighlightSelectionTool()
        {
            return CreatePushToolSpec(
                "Highlight\nSelection",
                "Color the selected elements red and every other element in the active view gray, for instant visual identification.",
                typeof(CmdHighlightSelection),
                "Highlight Selection.png",
                "Highlight Selection.png",
                pushButton =>
                {
                    pushButton.LongDescription = "Select elements first (or click with nothing selected to pick them), then click Highlight Selection - the selection turns red and everything else in the active view turns gray. Use the Reset Graphics tools (Graphics panel) afterward to clear the colors.";
                    pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName;
                });
        }

        private TopLevelToolSpec AddApplyGraphicsTools()
        {
            return CreatePushToolSpec(
                "Apply\nGraphics",
                "Apply the same graphics override settings to selected elements or selected categories in the active view.",
                typeof(CmdApplyGraphics),
                "apply.png",
                "apply.png",
                pushButton =>
                {
                    pushButton.LongDescription = "Choose Element mode or Category mode from one shared Apply Graphics window, then apply the same override settings in the active view.";
                    pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName;
                });
        }

        private TopLevelToolSpec AddMatchGraphicsTools()
        {
            return CreatePulldownToolSpec(
                "Match\nGraphics",
                "Match category or element graphics from a picked source.",
                "copy.png",
                "copy.png",
                CreateSplitChildTool(
                    "Match Category Graphics",
                    "Copy category graphics from one source category and apply them to selected target categories.",
                    typeof(CmdMatchCategoryGraphics),
                    "copy.png",
                    "copy.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName),
                CreateSplitChildTool(
                    "Match Element Graphics",
                    "Copy element-level graphics from one source element to selected target elements.",
                    typeof(CmdMatchElementGraphics),
                    "copy.png",
                    "copy.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName));
        }

        private TopLevelToolSpec AddResetGraphicsTools()
        {
            return CreatePulldownToolSpec(
                "Reset\nGraphics",
                "Reset category and element graphics overrides in the active view.",
                "Reset Overrides.png",
                "Reset Overrides.png",
                CreateSplitChildTool(
                    "Reset Category Graphics by Selection",
                    "Reset category graphics overrides using selected model elements in the active view.",
                    typeof(CmdResetCategoryGraphics),
                    "Reset Overrides.png",
                    "Reset Overrides.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName),
                CreateSplitChildTool(
                    "Reset Category Graphics in View",
                    "Reset category graphics overrides for all overridable categories in the active view.",
                    typeof(CmdResetCategoryGraphicsAllElements),
                    "Reset Overrides.png",
                    "Reset Overrides.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName),
                CreateSplitChildTool(
                    "Reset Element Graphics by Selection",
                    "Reset element-level graphics overrides for selected elements in the active view.",
                    typeof(CmdClearSelectedElementGraphics),
                    "Reset Overrides.png",
                    "Reset Overrides.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName),
                CreateSplitChildTool(
                    "Reset Element Graphics in View",
                    "Reset all element-level graphics overrides in the active view.",
                    typeof(CmdResetOverrides),
                    "Reset Overrides.png",
                    "Reset Overrides.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdGraphicalViewAvailability).FullName));
        }

        private TopLevelToolSpec AddWorkset3DViewsTool()
        {
            return CreatePulldownToolSpec(
                "3D\nViews",
                "3D view creation tools.",
                "3D Views.png",
                "3D Views.png",
                CreateSplitChildTool(
                    "Create 3D Views\nby Workset",
                    "Create one 3D view per user workset and isolate each workset in its matching view.",
                    typeof(Cmd3DViewsAsPerWorkset),
                    "3D Views.png",
                    "3D Views.png"));
        }

        private TopLevelToolSpec AddTransferTools()
        {
            return CreatePulldownToolSpec(
                "Transfer",
                "Transfer tools.",
                "Transfer View Template.png",
                "Transfer View Template.png",
                CreateSplitChildTool(
                    "Transfer View Templates",
                    "Transfer selected view templates between open project documents, with optional override.",
                    typeof(CmdTransferViewTemplates),
                    "Transfer View Template.png",
                    "Transfer View Template.png"),
                CreateSplitChildTool(
                    "Transfer Schedules",
                    "Transfer selected schedules between open project documents, with optional override that keeps sheet placements.",
                    typeof(CmdTransferSchedules),
                    "Transfer View Template.png",
                    "Transfer View Template.png"),
                CreateSplitChildTool(
                    "Transfer Legends",
                    "Transfer selected legends between open project documents, with optional override that keeps sheet placements.",
                    typeof(CmdTransferLegends),
                    "Transfer View Template.png",
                    "Transfer View Template.png"),
                CreateSplitChildTool(
                    "Transfer Drafting Views",
                    "Transfer selected drafting views between open project documents, with optional override that keeps sheet placements.",
                    typeof(CmdTransferDraftingViews),
                    "Transfer View Template.png",
                    "Transfer View Template.png"));
        }

        private TopLevelToolSpec AddFindElementTools()
        {
            return CreatePulldownToolSpec(
                "Element\nID",
                "Element ID tools for current and linked models.",
                "linkedID.png",
                "linkedID.png",
                CreateSplitChildTool(
                    "Get Element ID\nfrom Selection",
                    "Pick any element (model or linked) and view its Element ID with source info.",
                    typeof(CmdLinkedElementIdViewer),
                    "Linked ID of Selection.png",
                    "Linked ID of Selection.png"),
                CreateSplitChildTool(
                    "Find Element\nby Element ID",
                    "Search by Element ID in current or linked models and zoom to it.",
                    typeof(CmdLinkedElementSearch),
                    "View by Linked ID.png",
                    "View by Linked ID.png"));
        }

        private TopLevelToolSpec AddSetLinkWorksetTool()
        {
            return CreatePushToolSpec(
                "Link\nWorkset",
                "Assign selected Revit links and CAD imports to a workset.",
                typeof(CmdSetLinkWorkset),
                "Set Link Workset.png",
                "Set Link Workset.png");
        }

        private TopLevelToolSpec AddResetDatumsTools()
        {
            return CreateSplitToolSpec(
                "Reset Grid / Level\nExtents to 3D",
                "Reset grid or level datum extents back to 3D.",
                "Resetto3DExtents.png",
                "Resetto3DExtents.png",
                CreateSplitChildTool(
                    "Reset Grid\nExtents to 3D",
                    "Reset all visible grids to 3D extents in this view.",
                    typeof(CmdResetDatumsGrids),
                    "Resetto3DExtents.png",
                    "Resetto3DExtents.png"),
                CreateSplitChildTool(
                    "Reset Level\nExtents to 3D",
                    "Reset all visible levels to 3D extents in this view.",
                    typeof(CmdResetDatumsLevels),
                    "Resetto3DExtents.png",
                    "Resetto3DExtents.png"),
                CreateSplitChildTool(
                    "Reset Grid / Level\nExtents to 3D",
                    "Reset both grids and levels visible in this view.",
                    typeof(CmdResetDatums),
                    "Resetto3DExtents.png",
                    "Resetto3DExtents.png"));
        }

        private TopLevelToolSpec AddLevelExtentsTools()
        {
            return CreateSplitToolSpec(
                "Modify Level\nExtents",
                "Match or maximize level 3D extents.",
                "Level Extents.png",
                "Level Extents.png",
                CreateSplitChildTool(
                    "Match Level\nExtents",
                    "Select one source level, then pick target levels one-by-one to match extents (Esc to finish).",
                    typeof(CmdExtendLevelsBySelected),
                    "Level Extents.png",
                    "Level Extents.png"),
                CreateSplitChildTool(
                    "Maximize Level Extents\nto Section Box",
                    "Maximize all level 3D extents to the active 3D view's section box.",
                    typeof(CmdMaximizeLevelsBySectionBox),
                    "Level Extents.png",
                    "Level Extents.png"));
        }

        private TopLevelToolSpec AddViewCropTools()
        {
            return CreatePulldownToolSpec(
                "View\nCrop",
                "View crop and annotation crop tools.",
                "view crop 3d extents.png",
                "view crop 3d extents.png",
                CreateSplitChildTool(
                    "Crop View\nby Elements",
                    "Auto-fit the crop region of plan views. Pick 'Visible elements' or 'All model elements' in the settings dialog.",
                    typeof(CmdViewCropByAllModelElements),
                    "view crop 3d extents.png",
                    "view crop 3d extents.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPlanViewAvailability).FullName),
                CreateSplitChildTool(
                    "Set Annotation Crop\nby View Crop",
                    "Enable annotation crop in selected views and set equal offsets on all sides using each view's active crop box.",
                    typeof(CmdSetAnnotationCropByViewCrop),
                    "view crop 3d extents.png",
                    "view crop 3d extents.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPlanViewAvailability).FullName));
        }

        private TopLevelToolSpec AddFlipDatumBubblesTool()
        {
            return CreatePushToolSpec(
                "Flip Grid /\nLevel Bubbles",
                "Toggle which datum end shows the bubble for grids or levels, one item at a time.",
                typeof(CmdFlipGridBubble),
                "GridbubbleFlip.png",
                "GridbubbleFlip.png");
        }

        private TopLevelToolSpec AddConvertSharedParametersTool()
        {
            return CreatePushToolSpec(
                "Shared to\nFamily",
                "Convert selected shared parameters in the active family into normal family parameters.",
                typeof(SharedParamToFamilyParamCommand),
                "Share To Family.png",
                "Share To Family.png");
        }

        private TopLevelToolSpec AddMatchElevationTool()
        {
            return CreateSplitToolSpec(
                "Match MEP Element\nElevation",
                "Match center, top, or bottom elevation from a source MEP element to others.",
                "Match Elevation.png",
                "Match Elevation.png",
                CreateSplitChildTool(
                    "Match Center\nElevation",
                    "Match center elevation from a source MEP element to selected targets.",
                    typeof(CmdMatchElevation),
                    "Match Elevation.png",
                    "Match Elevation.png"),
                CreateSplitChildTool(
                    "Match Top\nElevation",
                    "Match top elevation from a source MEP element to selected targets.",
                    typeof(CmdMatchElevationTop),
                    "Match Elevation.png",
                    "Match Elevation.png"),
                CreateSplitChildTool(
                    "Match Bottom\nElevation",
                    "Match bottom elevation from a source MEP element to selected targets.",
                    typeof(CmdMatchElevationBottom),
                    "Match Elevation.png",
                    "Match Elevation.png"));
        }

        private TopLevelToolSpec AddReassignLevelTool()
        {
            return CreatePushToolSpec(
                "Reassign\nReference Level",
                "Reassign supported MEP elements from one level to another without moving them physically - whole project, or just your current selection.",
                typeof(CmdReassignLevel),
                "Reassign Level.png",
                "Reassign Level.png");
        }

        private TopLevelToolSpec AddLocationDataTool()
        {
            return CreatePushToolSpec(
                "Assign\nLocation",
                "Assign Room, Level, Coordinates, Altitude, and HVAC Zone data to selected categories.",
                typeof(CmdLocationDataAssigner),
                "Location Data.png",
                "Location Data.png");
        }

        private TopLevelToolSpec AddSmartConnectTool()
        {
            // "Connect MEP Elements" is the permanent default face - a single click always runs it
            // directly. "Connect MEP Elements Settings" only lives in the dropdown - it never takes
            // over the main face. IsSynchronizedWithCurrentItem = false is what keeps the top face
            // fixed on "Connect MEP Elements" (the first-added child) no matter which child was
            // actually run last - same pattern as the Opening panel's "Create Openings" tool.
            return CreateSplitToolSpec(
                "Connect MEP\nElements",
                "Connect MEP elements with a routed run, using your saved settings - no dialog. Select exactly two elements first to connect them directly, or click and pick pairs until Esc.",
                "SmartConnect.png",
                "SmartConnect.png",
                splitButton => splitButton.IsSynchronizedWithCurrentItem = false,
                CreateSplitChildTool(
                    "Connect MEP\nElements",
                    "Connect MEP elements (Pipe, Duct, Cable Tray, Conduit, Flex, and MEP equipment) with a routed run, using your saved settings. Select exactly two elements first to connect them directly, or click and pick pairs until Esc.",
                    typeof(SmartConnectCommand),
                    "SmartConnect.png",
                    "SmartConnect.png"),
                CreateSplitChildTool(
                    "Connect MEP Elements\nSettings",
                    "Choose how the run is routed and at what angle, which categories may be picked, which element may be trimmed, and what is copied onto the new pieces.",
                    typeof(CmdSmartConnectSettings),
                    "settings.png",
                    "settings.png"));
        }

        private TopLevelToolSpec AddCeilingMagnetTool()
        {
            return CreatePushToolSpec(
                "Elements to\nCeiling Grid",
                "Pick ceiling, pick grid intersection, then snap point-based elements to nearest tile centers.",
                typeof(CmdCeilingMagnet),
                "cursor.png",
                "cursor.png");
        }

        private TopLevelToolSpec AddHvacSchematicTool()
        {
            return CreatePushToolSpec(
                "HVAC\nSchematic",
                "Convert selected ducts, air terminals, and mechanical equipment into a connector-based HVAC schematic inside a new Drafting View.",
                typeof(HvacSchematicCommand),
                "Flowdirectioncreate.png",
                "Flowdirectioncreate.png");
        }

        private TopLevelToolSpec AddPipeSizingTool()
        {
            return CreatePushToolSpec(
                "Pipe Sizing",
                "Calculate domestic water pipe sizing from fixture units, system type, pipe material, and velocity limit.",
                typeof(CmdPipeSizing),
                "Pipe Sizing.png",
                "Pipe Sizing.png");
        }

        private TopLevelToolSpec AddMepOpeningsTool()
        {
            // "Create Openings" is the permanent default face - a single click always runs it directly.
            // "Opening Settings" only lives in the dropdown - it never takes over the main face.
            // IsSynchronizedWithCurrentItem = false is what keeps the top face fixed on "Create Openings"
            // (the first-added child) no matter which child was actually run last.
            return CreateSplitToolSpec(
                "Opening",
                "Create direct wall, floor/slab, and beam openings from selected pipes, ducts, cable trays, and conduits.",
                "MEP Openings.png",
                "MEP Openings.png",
                splitButton => splitButton.IsSynchronizedWithCurrentItem = false,
                CreateSplitChildTool(
                    "Create\nOpenings",
                    "Create and merge direct openings from the selected MEP elements.",
                    typeof(CmdCreateMepOpenings),
                    "MEP Openings.png",
                    "MEP Openings.png"),
                CreateSplitChildTool(
                    "Opening\nSettings",
                    "Set opening shape, cutout buffer, insulation, and merge distance rules.",
                    typeof(CmdMepOpeningSettings),
                    "MEP Openings.png",
                    "MEP Openings.png"));
        }

        private TopLevelToolSpec AddDuctStandardsTool()
        {
            return CreatePushToolSpec(
                "Duct\nStandard",
                "Calculate and write duct sheet thickness, gauge, weight, and area based on SMACNA-style rules.",
                typeof(CmdDuctStandardsManager),
                "Duct Standards.png",
                "Duct Standards.png");
        }

        private TopLevelToolSpec AddPurgeTools()
        {
            return CreatePulldownToolSpec(
                "Purge",
                "Purge tools.",
                "Remove.png",
                "Remove.png",
                CreateSplitChildTool(
                    "Purge Unplaced\n3D Views",
                    "Preview and delete selected unplaced 3D views in the active project.",
                    typeof(CmdPurgeUnplaced3DViews),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Unplaced\nSections",
                    "Preview and delete selected unplaced section views in the active project.",
                    typeof(CmdPurgeUnplacedSections),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Unplaced\nSchedules",
                    "Preview and delete selected schedules that are not placed on any sheet in the active project.",
                    typeof(CmdPurgeUnplacedSchedules),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Unplaced\nLegends",
                    "Preview and delete selected legends that are not placed on any sheet in the active project.",
                    typeof(CmdPurgeUnplacedLegends),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Unplaced\nDrafting Views",
                    "Preview and delete selected drafting views that are not placed on any sheet in the active project.",
                    typeof(CmdPurgeUnplacedDraftingViews),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Unused\nView Templates",
                    "Preview and delete selected view templates that are not assigned to any view in the active project.",
                    typeof(CmdPurgeUnusedViewTemplates),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Unused\nFilters",
                    "Preview and delete selected view/selection filters that are not applied on any view or view template in the active project.",
                    typeof(CmdPurgeUnusedFilters),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Unused\nGroups",
                    "Preview and delete selected Model Group and Detail Group types with zero placed instances in the active project.",
                    typeof(CmdPurgeUnusedGroups),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Family Parameters",
                    "Scan family parameters, classify unused candidates safely, and remove selected parameters in the active family document.",
                    typeof(CmdPurgeUnusedFamilyParameters),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnusedFamilyParametersAvailability).FullName));
        }

        private TopLevelToolSpec AddAboutTool()
        {
            return CreatePushToolSpec(
                "About",
                "Open the AJ Tools About window.",
                typeof(AboutCommand),
                "About.png",
                "About.png",
                pushButton =>
                {
                    pushButton.LongDescription = "Shows AJ Tools version, platform details, developer information, update notes, and repository links.";
                });
        }

        private void AddTopLevelTool(RibbonPanel panel, TopLevelToolSpec toolSpec)
        {
            if (panel == null || toolSpec == null)
            {
                return;
            }

            var createdItem = panel.AddItem(toolSpec.Data);
            toolSpec.ConfigureItem(createdItem);
        }

        private void AddStackedTools(RibbonPanel panel, TopLevelToolSpec first, TopLevelToolSpec second)
        {
            AddStackedToolsCore(panel, new[] { first, second });
        }

        private void AddStackedTools(RibbonPanel panel, TopLevelToolSpec first, TopLevelToolSpec second, TopLevelToolSpec third)
        {
            AddStackedToolsCore(panel, new[] { first, second, third });
        }

        private void AddStackedToolsCore(RibbonPanel panel, TopLevelToolSpec[] toolSpecs)
        {
            if (panel == null || toolSpecs == null || toolSpecs.Length < 2 || toolSpecs.Length > 3)
            {
                return;
            }

            IList<RibbonItem> createdItems;

            try
            {
                createdItems = toolSpecs.Length == 2
                    ? panel.AddStackedItems(toolSpecs[0].Data, toolSpecs[1].Data)
                    : panel.AddStackedItems(toolSpecs[0].Data, toolSpecs[1].Data, toolSpecs[2].Data);
            }
            catch (Exception)
            {
                // Fallback keeps ribbon startup safe across Revit versions that may reject
                // a specific stacked type combination (for example split buttons).
                createdItems = new List<RibbonItem>(toolSpecs.Length);
                foreach (var spec in toolSpecs)
                {
                    createdItems.Add(panel.AddItem(spec.Data));
                }
            }

            for (var i = 0; i < toolSpecs.Length && i < createdItems.Count; i++)
            {
                toolSpecs[i].ConfigureItem(createdItems[i]);
            }
        }

        private TopLevelToolSpec CreatePushToolSpec(
            string text,
            string tooltip,
            Type command,
            string largeIconFileName,
            string smallIconFileName,
            Action<PushButton> afterCreate = null)
        {
            return new TopLevelToolSpec(
                CreatePushButtonData(text, command, tooltip, largeIconFileName, smallIconFileName),
                item =>
                {
                    var pushButton = item as PushButton;
                    if (pushButton != null)
                    {
                        afterCreate?.Invoke(pushButton);
                    }
                });
        }

        private TopLevelToolSpec CreateSplitToolSpec(
            string text,
            string tooltip,
            string largeIconFileName,
            string smallIconFileName,
            params SplitChildToolSpec[] childTools)
        {
            return CreateSplitToolSpec(text, tooltip, largeIconFileName, smallIconFileName, null, childTools);
        }

        /// <summary>
        /// Overload that also hands back the created SplitButton itself via <paramref name="configureSplitButton"/>,
        /// for the rare split button that needs to set up its own state (e.g. IsSynchronizedWithCurrentItem)
        /// beyond just adding its child buttons.
        /// </summary>
        private TopLevelToolSpec CreateSplitToolSpec(
            string text,
            string tooltip,
            string largeIconFileName,
            string smallIconFileName,
            Action<SplitButton> configureSplitButton,
            params SplitChildToolSpec[] childTools)
        {
            return new TopLevelToolSpec(
                CreateSplitButtonData(text, tooltip, largeIconFileName, smallIconFileName),
                item =>
                {
                    var splitButton = item as SplitButton;
                    if (splitButton == null || childTools == null)
                    {
                        return;
                    }

                    foreach (var childTool in childTools)
                    {
                        var childButton = CreatePushButton(
                            splitButton,
                            childTool.Text,
                            childTool.Tooltip,
                            childTool.Command,
                            childTool.LargeIconFileName,
                            childTool.SmallIconFileName);

                        if (childButton != null)
                        {
                            childTool.AfterCreate?.Invoke(childButton);
                        }
                    }

                    configureSplitButton?.Invoke(splitButton);
                });
        }

        private TopLevelToolSpec CreatePulldownToolSpec(
            string text,
            string tooltip,
            string largeIconFileName,
            string smallIconFileName,
            params SplitChildToolSpec[] childTools)
        {
            return new TopLevelToolSpec(
                CreatePulldownButtonData(text, tooltip, largeIconFileName, smallIconFileName),
                item =>
                {
                    var pulldownButton = item as PulldownButton;
                    if (pulldownButton == null || childTools == null)
                    {
                        return;
                    }

                    foreach (var childTool in childTools)
                    {
                        var childButton = CreatePushButton(
                            pulldownButton,
                            childTool.Text,
                            childTool.Tooltip,
                            childTool.Command,
                            childTool.LargeIconFileName,
                            childTool.SmallIconFileName);

                        if (childButton != null)
                        {
                            childTool.AfterCreate?.Invoke(childButton);
                        }
                    }
                });
        }

        private static SplitChildToolSpec CreateSplitChildTool(
            string text,
            string tooltip,
            Type command,
            string largeIconFileName,
            string smallIconFileName,
            Action<PushButton> afterCreate = null)
        {
            return new SplitChildToolSpec(
                text,
                tooltip,
                command,
                largeIconFileName,
                smallIconFileName,
                afterCreate);
        }

        /// <summary>
        /// Finds an existing panel on the AJ Tools tab or creates it if missing.
        /// </summary>
        private RibbonPanel GetOrCreatePanel(string panelName)
        {
            return RibbonPanelHelper.GetOrCreatePanel(_app, TabName, panelName);
        }

        /// <summary>
        /// Adds a push button to a split button menu.
        /// </summary>
        private PushButton CreatePushButton(
            SplitButton splitButton,
            string text,
            string tooltip,
            Type command,
            string largeIconFileName,
            string smallIconFileName)
        {
            if (splitButton == null)
            {
                return null;
            }

            return splitButton.AddPushButton(
                CreatePushButtonData(
                    text,
                    command,
                    tooltip,
                    largeIconFileName,
                    smallIconFileName));
        }

        /// <summary>
        /// Adds a push button to a pulldown button menu.
        /// </summary>
        private PushButton CreatePushButton(
            PulldownButton pulldownButton,
            string text,
            string tooltip,
            Type command,
            string largeIconFileName,
            string smallIconFileName)
        {
            if (pulldownButton == null)
            {
                return null;
            }

            return pulldownButton.AddPushButton(
                CreatePushButtonData(
                    text,
                    command,
                    tooltip,
                    largeIconFileName,
                    smallIconFileName));
        }

        /// <summary>
        /// Creates button data pointing at the given external command type.
        /// </summary>
        private PushButtonData CreatePushButtonData(
            string text,
            Type command,
            string tooltip,
            string largeIconFileName,
            string smallIconFileName)
        {
            var pushButtonData = new PushButtonData($"cmd{command.Name}", text, _assemblyPath, command.FullName)
            {
                ToolTip = tooltip
            };

            var largeIcon = _iconLoader.LoadLarge(largeIconFileName);
            if (largeIcon != null)
            {
                pushButtonData.LargeImage = largeIcon;
            }

            var smallIcon = _iconLoader.LoadSmall(smallIconFileName);
            if (smallIcon != null)
            {
                pushButtonData.Image = smallIcon;
            }

            return pushButtonData;
        }

        /// <summary>
        /// Creates split button data with tooltip and icons.
        /// </summary>
        private SplitButtonData CreateSplitButtonData(
            string text,
            string tooltip,
            string largeIconFileName,
            string smallIconFileName)
        {
            var splitButtonData = new SplitButtonData(CreateSplitButtonName(text), text)
            {
                ToolTip = tooltip
            };

            var largeIcon = _iconLoader.LoadLarge(largeIconFileName);
            if (largeIcon != null)
            {
                splitButtonData.LargeImage = largeIcon;
            }

            var smallIcon = _iconLoader.LoadSmall(smallIconFileName);
            if (smallIcon != null)
            {
                splitButtonData.Image = smallIcon;
            }

            return splitButtonData;
        }

        /// <summary>
        /// Creates pulldown button data with tooltip and icons.
        /// </summary>
        private PulldownButtonData CreatePulldownButtonData(
            string text,
            string tooltip,
            string largeIconFileName,
            string smallIconFileName)
        {
            var pulldownButtonData = new PulldownButtonData(CreatePulldownButtonName(text), text)
            {
                ToolTip = tooltip
            };

            var largeIcon = _iconLoader.LoadLarge(largeIconFileName);
            if (largeIcon != null)
            {
                pulldownButtonData.LargeImage = largeIcon;
            }

            var smallIcon = _iconLoader.LoadSmall(smallIconFileName);
            if (smallIcon != null)
            {
                pulldownButtonData.Image = smallIcon;
            }

            return pulldownButtonData;
        }

        private static string CreateSplitButtonName(string text)
        {
            var normalizedText = (text ?? string.Empty)
                .Replace("\n", string.Empty)
                .Replace(" ", string.Empty);
            return $"split_{normalizedText}";
        }

        private static string CreatePulldownButtonName(string text)
        {
            var normalizedText = (text ?? string.Empty)
                .Replace("\n", string.Empty)
                .Replace(" ", string.Empty);
            return $"pulldown_{normalizedText}";
        }
    }
}
