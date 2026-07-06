#region Metadata
/*
 * Tool Name     : AJ Tools Ribbon Manager
 * File Name     : RibbonManager.cs
 * Purpose       : Builds the main "AJ Tools" ribbon tab - its panels (View, Graphics, Datums, Modify, MEP,
 *                 Coordination, Data, Manage, Family, AI, About) and every button, split, and pulldown.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.4.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-04
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
 * v1.4.1 (2026-07-04) - Moved the Opening split button from the MEP panel into its
 *                       own Opening panel for future opening tools.
 * v1.4.0 (2026-07-03) - Added Opening split button in the MEP panel with Settings and
 *                       Create Openings commands.
 * v1.2.0 (2026-05-07) - Reorganized ribbon panels; added HVAC schematic registration.
 * v1.3.0 (2026-07-01) - Refactor/audit: standardized metadata block. Ribbon layout unchanged.
 * v1.3.1 (2026-07-01) - Full audit fixes: wired CmdPurgeUnusedFamilyParametersAvailability into the
 *                       Purge Family Parameters button (was defined but never assigned); renamed the
 *                       "Aj tool" panel label to "About" for consistent casing.
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
            AddTopLevelTool(panel, AddFilterProTool());
            AddTopLevelTool(panel, AddColorizeTool());
            AddTopLevelTool(panel, AddSectionMarkVisibilityTool());
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
            AddTopLevelTool(panel, AddTransferViewTemplatesTool());
            AddTopLevelTool(panel, AddPurgeFamilyParametersTool());
        }

        private void BuildFamilyPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddConvertSharedParametersTool());
        }

        private void BuildAiPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddAiTool());
        }

        private void BuildAboutPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddAboutTool());
        }

        private TopLevelToolSpec AddAiTool()
        {
            return CreatePushToolSpec(
                "AJ AI",
                "Open the AI-powered Gemini C# Shell for Revit.",
                typeof(AJTools.GeminiShell.Commands.ShowGeminiShellCommand),
                "AJ_AI.png",
                "AJ_AI.png");
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

        private TopLevelToolSpec AddPinElementsTool()
        {
            return CreatePushToolSpec(
                "Pin / Unpin\nElements",
                "Pin/unpin separated Sheet groups (Title Blocks, Placed Views, Legends, Schedules) with Active Sheet Only or All Sheets mode, and Model groups (Duct, Pipe, Cable Tray, Generic Models, Mechanical Equipment, Plumbing Fixtures, Electrical Equipment).",
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
                "Colorize elements by category or by parameter value directly in the active view (or selected views) — no view filter is created.",
                typeof(CmdColorize),
                "Colorize.png",
                "Colorize.png",
                pushButton =>
                {
                    pushButton.LongDescription = "Pick categories (and optionally a parameter and values, using the same category/parameter/value engine as Filter Pro), choose graphics options, then Shuffle Colors applies the overrides directly to matched elements in the active view or selected views — click it again anytime to re-shuffle.";
                    pushButton.AvailabilityClassName = typeof(CmdColorizeAvailability).FullName;
                });
        }

        private TopLevelToolSpec AddSectionMarkVisibilityTool()
        {
            return CreatePushToolSpec(
                "Section Mark\nVisibility",
                "Automatically manage section visibility based on Sheet Number filters or sheet placement status.",
                typeof(CmdSectionMarkVisibility),
                "SectionMarkVisibility.png",
                "SectionMarkVisibility.png",
                pushButton => pushButton.AvailabilityClassName = typeof(CmdPlanViewAvailability).FullName);
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

        private TopLevelToolSpec AddTransferViewTemplatesTool()
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
                "Reassign supported MEP elements from one level to another without moving them physically.",
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
            return CreatePushToolSpec(
                "Connect MEP\nElements",
                "Connect two same-category MEP elements (Pipe, Duct, Cable Tray) with routing and angle settings.",
                typeof(SmartConnectCommand),
                "SmartConnect.png",
                "SmartConnect.png");
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
            return CreateSplitToolSpec(
                "Opening",
                "Create direct wall, floor/slab, and beam openings from selected pipes, ducts, cable trays, and conduits.",
                "MEP Openings.png",
                "MEP Openings.png",
                CreateSplitChildTool(
                    "Opening\nSettings",
                    "Set opening shape, cutout buffer, insulation, and merge distance rules.",
                    typeof(CmdMepOpeningSettings),
                    "MEP Openings.png",
                    "MEP Openings.png"),
                CreateSplitChildTool(
                    "Create\nOpenings",
                    "Create and merge direct openings from the selected MEP elements.",
                    typeof(CmdCreateMepOpenings),
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

        private TopLevelToolSpec AddPurgeFamilyParametersTool()
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
            RibbonPanel existingPanel = null;
            foreach (var p in _app.GetRibbonPanels(TabName))
            {
                if (p.Name == panelName)
                {
                    existingPanel = p;
                    break;
                }
            }

            return existingPanel ?? _app.CreateRibbonPanel(TabName, panelName);
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
