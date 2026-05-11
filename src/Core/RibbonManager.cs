// ==================================================
// Tool Name    : Ribbon Manager
// Purpose      : Builds the AJ Tools ribbon tab, panels, and tool registration.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.2.0
// Created      : 2025-12-10
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Revit startup application context and AJ Tools command registrations.
// Output       : AJ Tools ribbon layout with registered buttons, split buttons, and pulldowns.
// Notes        : Keeps Revit 2020 ribbon registration centralized and aligned with packaged icon resources.
// Changelog    : v1.1.0 - Reorganized ribbon panels, added HVAC schematic registration, and standardized metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
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
            Dimensions,
            Annotation,
            Tags,
            Modify,
            Mep,
            Coordination,
            Data,
            Manage,
            Family,
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
                [PanelKey.Dimensions] = "Dimensions",
                [PanelKey.Annotation] = "Annotation",
                [PanelKey.Tags] = "Tags",
                [PanelKey.Family] = "Family",
                [PanelKey.Modify] = "Modify",
                [PanelKey.Mep] = "MEP",
                [PanelKey.Coordination] = "Coordination",
                [PanelKey.Data] = "Data",
                [PanelKey.Manage] = "Manage",
                [PanelKey.About] = "Aj tool"
            };

            _panelOrder = new List<PanelKey>
            {
                PanelKey.View,
                PanelKey.Graphics,
                PanelKey.Datums,
                PanelKey.Dimensions,
                PanelKey.Annotation,
                PanelKey.Tags,
                PanelKey.Modify,
                PanelKey.Mep,
                PanelKey.Coordination,
                PanelKey.Data,
                PanelKey.Manage,
                PanelKey.Family,
                PanelKey.About
            };

            // To rename a panel, update _panelNames.
            // To move a tool group to another panel, change the PanelKey used here.
            _toolLayout = new List<ToolPlacement>
            {
                new ToolPlacement(PanelKey.View, BuildViewPanel),
                new ToolPlacement(PanelKey.Graphics, BuildGraphicsPanel),
                new ToolPlacement(PanelKey.Datums, BuildDatumsPanel),
                new ToolPlacement(PanelKey.Dimensions, BuildDimensionsPanel),
                new ToolPlacement(PanelKey.Annotation, BuildAnnotationPanel),
                new ToolPlacement(PanelKey.Tags, BuildTagsPanel),
                new ToolPlacement(PanelKey.Modify, BuildModifyPanel),
                new ToolPlacement(PanelKey.Mep, BuildMepPanel),
                new ToolPlacement(PanelKey.Coordination, BuildCoordinationPanel),
                new ToolPlacement(PanelKey.Data, BuildDataPanel),
                new ToolPlacement(PanelKey.Manage, BuildManagePanel),
                new ToolPlacement(PanelKey.Family, BuildFamilyPanel),
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
        }

        private void BuildGraphicsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddApplyGraphicsTools(), AddMatchGraphicsTools(), AddResetGraphicsTools());
        }

        private void BuildDatumsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddResetDatumsTools(), AddLevelExtentsTools(), AddFlipDatumBubblesTool());
        }

        private void BuildDimensionsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddAutoDimensionsTools(), AddDimensionByLineTools(), AddCopyDimensionTextTool());
        }

        private void BuildAnnotationPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddDuctFlowTools(), AddRevisionCloudsTool(), AddTextTools());
        }

        private void BuildTagsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddSmartMepTagTools(), AddArrangeTagsTools(), AddLShapeLeadersTool());
        }

        private void BuildModifyPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddMatchElevationTool(), AddReassignLevelTool(), AddPinElementsTool());
        }

        private void BuildMepPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddSmartConnectTool(), AddCeilingMagnetTool(), AddHvacSchematicTool());
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
            AddStackedTools(panel, AddConvertSharedParametersTool(), AddCenterAnnotationsTool());
        }

        private void BuildAboutPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddAboutTool());
        }

        private TopLevelToolSpec AddToggleLinksTool()
        {
            return CreatePushToolSpec(
                "Toggle\nLink",
                "Toggle visibility of all Revit Links in the active view.",
                typeof(CmdToggleRevitLinks),
                "Toggle Links.png",
                "Toggle Links.png");
        }

        private TopLevelToolSpec AddUnhideAllTool()
        {
            return CreatePushToolSpec(
                "Unhide\nAll",
                "Unhide all elements in the active view (Temporary Hide/Isolate + hidden items).",
                typeof(CmdUnhideAll),
                "Unhide All.png",
                "Unhide All.png");
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
                    "copy.png"),
                CreateSplitChildTool(
                    "Match Element Graphics",
                    "Copy element-level graphics from one source element to selected target elements.",
                    typeof(CmdMatchElementGraphics),
                    "copy.png",
                    "copy.png"));
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
                    "Reset Overrides.png"),
                CreateSplitChildTool(
                    "Reset Category Graphics in View",
                    "Reset category graphics overrides for model categories found from all elements in the active view.",
                    typeof(CmdResetCategoryGraphicsAllElements),
                    "Reset Overrides.png",
                    "Reset Overrides.png"),
                CreateSplitChildTool(
                    "Reset Element Graphics by Selection",
                    "Reset element-level graphics overrides for selected elements in the active view.",
                    typeof(CmdClearSelectedElementGraphics),
                    "Reset Overrides.png",
                    "Reset Overrides.png"),
                CreateSplitChildTool(
                    "Reset Element Graphics in View",
                    "Reset all element-level graphics overrides in the active view.",
                    typeof(CmdResetOverrides),
                    "Reset Overrides.png",
                    "Reset Overrides.png"));
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

        private TopLevelToolSpec AddAutoDimensionsTools()
        {
            return CreatePulldownToolSpec(
                "Automatic\nDimension",
                "Dimension grids and levels automatically.",
                "Dimensions.png",
                "Dimensions.png",
                CreateSplitChildTool(
                    "Automatic Grid\nDimensions",
                    "Create horizontal/vertical grid dimension strings in plan views.",
                    typeof(CmdAutoDimensionsGrids),
                    "Dimensions.png",
                    "Dimensions.png"),
                CreateSplitChildTool(
                    "Automatic Level\nDimensions",
                    "Create level dimension strings in section or elevation views.",
                    typeof(CmdAutoDimensionsLevels),
                    "Dimensions.png",
                    "Dimensions.png"),
                CreateSplitChildTool(
                    "Automatic Grid /\nLevel Dimensions",
                    "Plan views: dimension grids. Sections/Elevations: dimension levels and grids.",
                    typeof(CmdAutoDimensions),
                    "Dimensions.png",
                    "Dimensions.png"));
        }

        private TopLevelToolSpec AddDimensionByLineTools()
        {
            return CreatePulldownToolSpec(
                "Quick\nDimension",
                "Quick parallel dimensions and grid/level dimensions along a picked line.",
                "Dimensions by Line.png",
                "Dimensions by Line.png",
                CreateSplitChildTool(
                    "Quick Parallel Dimension\nby Centerline",
                    "Quickly create a dimension string for selected parallel elements using center line references.",
                    typeof(CmdQuickParallelCenterLineDimension),
                    "Dimensions by Line.png",
                    "Dimensions by Line.png"),
                CreateSplitChildTool(
                    "Quick Parallel Dimension\nby Face / Edge",
                    "Quickly create dimensions using both side faces/edges for each selected parallel element (for ducts/pipes this captures both sides).",
                    typeof(CmdQuickParallelFaceEdgeDimension),
                    "Dimensions by Line.png",
                    "Dimensions by Line.png"),
                CreateSplitChildTool(
                    "Create Grid Dimensions\nby Picked Line",
                    "Create a dimension string across intersecting grids using a picked line (plan, section, or elevation).",
                    typeof(CmdDimensionGridsByLine),
                    "Dimensions by Line.png",
                    "Dimensions by Line.png"),
                CreateSplitChildTool(
                    "Create Level Dimensions\nby Picked Line",
                    "Create a dimension string across levels within the picked vertical range.",
                    typeof(CmdDimensionLevelsByLine),
                    "Dimensions by Line.png",
                    "Dimensions by Line.png"));
        }

        private TopLevelToolSpec AddCopyDimensionTextTool()
        {
            return CreatePushToolSpec(
                "Copy Dimension\nText",
                "Copy Above/Below/Prefix/Suffix text from one dimension to others.",
                typeof(CmdCopyDimensionText),
                "Copy Dim Text.png",
                "Copy Dim Text.png");
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
                    "Crop View by\nVisible Elements",
                    "Fit crop per target view using only elements currently visible in that view.",
                    typeof(CmdViewCropByActiveViewElements),
                    "view crop 3d extents.png",
                    "view crop 3d extents.png"),
                CreateSplitChildTool(
                    "Crop View by\nAll Model Elements",
                    "Fit crop per target view using project-wide model extents projected to that view.",
                    typeof(CmdViewCropByAllModelElements),
                    "view crop 3d extents.png",
                    "view crop 3d extents.png"),
                CreateSplitChildTool(
                    "Set Annotation Crop\nby View Crop",
                    "Enable annotation crop in selected views and set equal offsets on all sides using each view's active crop box.",
                    typeof(CmdSetAnnotationCropByViewCrop),
                    "view crop 3d extents.png",
                    "view crop 3d extents.png"));
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

        private TopLevelToolSpec AddRevisionCloudsTool()
        {
            return CreatePulldownToolSpec(
                "Revision\nClouds",
                "Create orthogonal stepped revision cloud boundaries and configure settings.",
                "Cloud By Elements.png",
                "Cloud By Elements.png",
                CreateSplitChildTool(
                    "Revision Clouds\nby Elements",
                    "Create orthogonal stepped revision cloud boundaries aligned to dominant selected-element angle. Keeps running until Esc.",
                    typeof(CmdRevisionCloudByElements),
                    "Cloud By Elements.png",
                    "Cloud By Elements.png"),
                CreateSplitChildTool(
                    "Revision Cloud\nSettings",
                    "Configure offset distance for Cloud By Elements.",
                    typeof(CmdRevisionCloudByElementsSettings),
                    "settings.png",
                    "settings.png"));
        }

        private TopLevelToolSpec AddCenterAnnotationsTool()
        {
            return CreatePushToolSpec(
                "Center\nAnnotation",
                "Center selected annotations in the active annotation family view.",
                typeof(CmdResetTextPosition),
                "Reset Position.png",
                "Reset Position.png");
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

        private TopLevelToolSpec AddDuctFlowTools()
        {
            return CreatePulldownToolSpec(
                "Duct Flow\nAnnotations",
                "Duct flow annotation tools.",
                "Flowdirectioncreate.png",
                "Flowdirectioncreate.png",
                CreateSplitChildTool(
                    "Duct Flow\nAnnotations",
                    "Place duct flow annotations along horizontal ducts.",
                    typeof(CmdFlowDirectionAnnotations),
                    "Flowdirectioncreate.png",
                    "Flowdirectioncreate.png"),
                CreateSplitChildTool(
                    "Duct Flow Annotation\nSettings",
                    "Choose the annotation family and spacing used for duct flow placement.",
                    typeof(CmdFlowDirectionSettings),
                    "settings.png",
                    "settings.png"));
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
                    "Purge Unplaced\n3D + Sections",
                    "Preview and delete selected unplaced 3D and section views in the active project.",
                    typeof(CmdPurgeUnplacedViews),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnplacedViewsAvailability).FullName),
                CreateSplitChildTool(
                    "Purge Family Parameters",
                    "Scan family parameters, classify unused candidates safely, and remove selected parameters in the active family document.",
                    typeof(CmdPurgeUnusedFamilyParameters),
                    "Remove.png",
                    "Remove.png"));
        }

        private TopLevelToolSpec AddSmartMepTagTools()
        {
            return CreatePulldownToolSpec(
                "Smart MEP\nTags",
                "Smart MEP tagging tools.",
                "Smart MEP TAG.png",
                "Smart MEP TAG.png",
                CreateSplitChildTool(
                    "Smart MEP\nTags",
                    "Analyse the active view and intelligently tag MEP elements (ducts, pipes, equipment, accessories, cable trays) with clash-free placement.",
                    typeof(CmdSmartMepTag),
                    "Smart MEP TAG.png",
                    "Smart MEP TAG.png"),
                CreateSplitChildTool(
                    "Smart MEP Tagging\nSettings",
                    "Configure category-wise enable/disable for Smart MEP Tag.",
                    typeof(CmdSmartMepTagSettings),
                    "settings.png",
                    "settings.png"));
        }

        private TopLevelToolSpec AddLShapeLeadersTool()
        {
            return CreatePushToolSpec(
                "L-Shape\nLeader",
                "Force tags to use a right-angle leader. Run again on the same tag to flip the elbow side. Preselect tags or pick tags (Tab cycles) until Esc.",
                typeof(CmdForceTagLeaderLShape),
                "L-ShapeLeader.png",
                "L-ShapeLeader.png");
        }

        private TopLevelToolSpec AddArrangeTagsTools()
        {
            return CreatePulldownToolSpec(
                "Rearrange\nTags",
                "Arrange tag tools.",
                "Arrange Tag.png",
                "Arrange Tag.png",
                CreateSplitChildTool(
                    "Rearrange\nTags",
                    "Rearrange selected tags into a clean vertical stack. The nearest T1-to-L1 tag position is placed first, then remaining tags stack above or below based on T1 relative to L1.",
                    typeof(CmdIntelligentTagArranger),
                    "Arrange Tag.png",
                    "Arrange Tag.png"),
                CreateSplitChildTool(
                    "Arrange Tag\nSettings",
                    "Set default vertical spacing for Arrange Tags (tag_spacing_mm).",
                    typeof(CmdIntelligentTagArrangerSettings),
                    "settings.png",
                    "settings.png"));
        }

        private TopLevelToolSpec AddTextTools()
        {
            return CreateSplitToolSpec(
                "Copy / Swap\nText Notes",
                "Copy or swap text values between text notes.",
                "copyswaptext.png",
                "copyswaptext.png",
                CreateSplitChildTool(
                    "Copy Text\nNotes",
                    "Copy the text value from one text note to others (click targets until ESC).",
                    typeof(CmdCopyText),
                    "copyswaptext.png",
                    "copyswaptext.png"),
                CreateSplitChildTool(
                    "Swap Text\nNotes",
                    "Swap the text values between two picked text notes (one-time).",
                    typeof(CmdSwapText),
                    "copyswaptext.png",
                    "copyswaptext.png"));
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
