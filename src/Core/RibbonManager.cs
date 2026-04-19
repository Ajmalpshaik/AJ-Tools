// Tool Name: Ribbon Manager
// Description: Builds the AJ Tools ribbon UI and registers push buttons and split buttons.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI, System.Reflection, System.Windows.Media.Imaging, AJTools.Commands, AJTools.Utils
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
            Graphics,
            Links,
            Dimensions,
            Family,
            Mep,
            DataStandards,
            Annotations,
            Info
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
                [PanelKey.Graphics] = "Graphics",
                [PanelKey.Links] = "Links",
                [PanelKey.Dimensions] = "Dimensions",
                [PanelKey.Family] = "Family",
                [PanelKey.Mep] = "MEP",
                [PanelKey.DataStandards] = "Data & Standards",
                [PanelKey.Annotations] = "Annotations",
                [PanelKey.Info] = "Info"
            };

            _panelOrder = new List<PanelKey>
            {
                PanelKey.Graphics,
                PanelKey.Links,
                PanelKey.Dimensions,
                PanelKey.Family,
                PanelKey.Mep,
                PanelKey.DataStandards,
                PanelKey.Annotations,
                PanelKey.Info
            };

            // To rename a panel, update _panelNames.
            // To move a tool group to another panel, change the PanelKey used here.
            _toolLayout = new List<ToolPlacement>
            {
                new ToolPlacement(PanelKey.Graphics, BuildGraphicsPanel),
                new ToolPlacement(PanelKey.Links, BuildLinksPanel),
                new ToolPlacement(PanelKey.Dimensions, BuildDimensionsPanel),
                new ToolPlacement(PanelKey.Family, BuildFamilyPanel),
                new ToolPlacement(PanelKey.Mep, BuildMepPanel),
                new ToolPlacement(PanelKey.DataStandards, BuildDataStandardsPanel),
                new ToolPlacement(PanelKey.Annotations, BuildAnnotationsPanel),
                new ToolPlacement(PanelKey.Info, BuildInfoPanel)
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

        private void BuildGraphicsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddToggleLinksTool(), AddUnhideAllTool(), AddPinElementsTool());
            AddStackedTools(panel, AddApplyGraphicsTools(), AddMatchGraphicsTools(), AddResetGraphicsTools());
            AddTopLevelTool(panel, Add3DViewsTools());
            AddTopLevelTool(panel, AddFilterProTool());
            AddTopLevelTool(panel, AddTransferViewTemplatesTool());
        }

        private void BuildLinksPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddElementIdTools(), AddSetLinkWorksetTool());
        }

        private void BuildDimensionsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddAutoDimensionsTools(), AddDimensionByLineTools());
            AddTopLevelTool(panel, AddCopyDimensionTextTool());
        }

        private void BuildFamilyPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddSharedToFamilyTool());
        }

        private void BuildMepPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddMatchElevationTool(), AddReassignLevelTool());
            AddStackedTools(panel, AddSmartConnectTool(), AddCeilingMagnetTool());
        }

        private void BuildDataStandardsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddLocationDataTool(), AddDuctStandardsTool(), AddPurgeTools());
        }

        private void BuildAnnotationsPanel(RibbonPanel panel)
        {
            AddStackedTools(panel, AddSmartMepTagTools(), AddDuctFlowTools(), AddRevisionCloudByElementsTool());
            AddTopLevelTool(panel, AddResetTextTool());
            AddStackedTools(panel, AddResetDatumsTools(), AddExtendLevelsTool());
            AddTopLevelTool(panel, AddFlipGridBubbleTool());
            AddTopLevelTool(panel, AddLShapeLeaderTool());
            AddStackedTools(panel, AddViewCrop3DExtentsTools(), AddArrangeTagsTools(), AddCopySwapTextTools());
        }

        private void BuildInfoPanel(RibbonPanel panel)
        {
            AddTopLevelTool(panel, AddAboutTool());
        }

        private TopLevelToolSpec AddToggleLinksTool()
        {
            return CreatePushToolSpec(
                "Toggle\nLinks",
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
                "Pin",
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
            return CreateSplitToolSpec(
                "Apply\nGraphics",
                "Apply category or element graphics overrides in the active view.",
                "apply.png",
                "apply.png",
                CreateSplitChildTool(
                    "Category\nGraphics",
                    "Apply graphics overrides to unique model categories from selected elements in the active view.",
                    typeof(CmdCategoryGraphics),
                    "apply.png",
                    "apply.png"),
                CreateSplitChildTool(
                    "Element\nGraphics",
                    "Apply element-level graphics overrides to selected elements in the active view.",
                    typeof(CmdElementGraphics),
                    "apply.png",
                    "apply.png"));
        }

        private TopLevelToolSpec AddMatchGraphicsTools()
        {
            return CreateSplitToolSpec(
                "Match\nGraphics",
                "Match category or element graphics from a picked source.",
                "copy.png",
                "copy.png",
                CreateSplitChildTool(
                    "Match Category\nGraphics",
                    "Copy category graphics from one source category and apply them to selected target categories.",
                    typeof(CmdMatchCategoryGraphics),
                    "copy.png",
                    "copy.png"),
                CreateSplitChildTool(
                    "Match Element\nGraphics",
                    "Copy element-level graphics from one source element to selected target elements.",
                    typeof(CmdMatchElementGraphics),
                    "copy.png",
                    "copy.png"));
        }

        private TopLevelToolSpec AddResetGraphicsTools()
        {
            return CreateSplitToolSpec(
                "Reset\nGraphics",
                "Reset category and element graphics overrides in the active view.",
                "Reset Overrides.png",
                "Reset Overrides.png",
                CreateSplitChildTool(
                    "Reset Category\nBy Selection",
                    "Reset category graphics overrides using selected model elements in the active view.",
                    typeof(CmdResetCategoryGraphics),
                    "Reset Overrides.png",
                    "Reset Overrides.png"),
                CreateSplitChildTool(
                    "Reset Category\nAll Elements",
                    "Reset category graphics overrides for model categories found from all elements in the active view.",
                    typeof(CmdResetCategoryGraphicsAllElements),
                    "Reset Overrides.png",
                    "Reset Overrides.png"),
                CreateSplitChildTool(
                    "Reset Element\nBy Selection",
                    "Reset element-level graphics overrides for selected elements in the active view.",
                    typeof(CmdClearSelectedElementGraphics),
                    "Reset Overrides.png",
                    "Reset Overrides.png"),
                CreateSplitChildTool(
                    "Reset Element\nAll Elements",
                    "Reset all element-level graphics overrides in the active view.",
                    typeof(CmdResetOverrides),
                    "Reset Overrides.png",
                    "Reset Overrides.png"));
        }

        private TopLevelToolSpec Add3DViewsTools()
        {
            return CreateSplitToolSpec(
                "3D\nViews",
                "3D view tools.",
                "3D Views.png",
                "3D Views.png",
                CreateSplitChildTool(
                    "Create 3D View As Per Workset",
                    "Create one 3D view per user workset and isolate each workset in its matching view.",
                    typeof(Cmd3DViewsAsPerWorkset),
                    "3D Views.png",
                    "3D Views.png"));
        }

        private TopLevelToolSpec AddTransferViewTemplatesTool()
        {
            return CreatePushToolSpec(
                "Transfer\nTemplates",
                "Transfer selected view templates between open project documents, with optional override.",
                typeof(CmdTransferViewTemplates),
                "Transfer View Template.png",
                "Transfer View Template.png");
        }

        private TopLevelToolSpec AddElementIdTools()
        {
            return CreateSplitToolSpec(
                "Element\nID",
                "Element ID tools for current and linked models.",
                "linkedID.png",
                "linkedID.png",
                CreateSplitChildTool(
                    "Linked ID of\nSelection",
                    "Pick any element (model or linked) and view its Element ID with source info.",
                    typeof(CmdLinkedElementIdViewer),
                    "Linked ID of Selection.png",
                    "Linked ID of Selection.png"),
                CreateSplitChildTool(
                    "View by\nLinked ID",
                    "Search by Element ID in current or linked models and zoom to it.",
                    typeof(CmdLinkedElementSearch),
                    "View by Linked ID.png",
                    "View by Linked ID.png"));
        }

        private TopLevelToolSpec AddSetLinkWorksetTool()
        {
            return CreatePushToolSpec(
                "Set Link\nWorkset",
                "Assign selected Revit links and CAD imports to a workset.",
                typeof(CmdSetLinkWorkset),
                "Set Link Workset.png",
                "Set Link Workset.png");
        }

        private TopLevelToolSpec AddAutoDimensionsTools()
        {
            return CreateSplitToolSpec(
                "Auto\nDims",
                "Dimension grids and levels automatically.",
                "Dimensions.png",
                "Dimensions.png",
                CreateSplitChildTool(
                    "Grids Only",
                    "Create horizontal/vertical grid dimension strings in plan views.",
                    typeof(CmdAutoDimensionsGrids),
                    "Dimensions.png",
                    "Dimensions.png"),
                CreateSplitChildTool(
                    "Levels Only",
                    "Create level dimension strings in section or elevation views.",
                    typeof(CmdAutoDimensionsLevels),
                    "Dimensions.png",
                    "Dimensions.png"),
                CreateSplitChildTool(
                    "Grids + Levels",
                    "Plan views: dimension grids. Sections/Elevations: dimension levels and grids.",
                    typeof(CmdAutoDimensions),
                    "Dimensions.png",
                    "Dimensions.png"));
        }

        private TopLevelToolSpec AddDimensionByLineTools()
        {
            return CreateSplitToolSpec(
                "Dim By\nLine",
                "Pick two points to place grid or level dimensions along a custom line.",
                "Dimensions by Line.png",
                "Dimensions by Line.png",
                CreateSplitChildTool(
                    "Quick\nCenter Line",
                    "Quickly create a dimension string for selected parallel elements using center line references.",
                    typeof(CmdQuickParallelCenterLineDimension),
                    "Dimensions by Line.png",
                    "Dimensions by Line.png"),
                CreateSplitChildTool(
                    "Quick\nFace/Edge",
                    "Quickly create dimensions using both side faces/edges for each selected parallel element (for ducts/pipes this captures both sides).",
                    typeof(CmdQuickParallelFaceEdgeDimension),
                    "Dimensions by Line.png",
                    "Dimensions by Line.png"),
                CreateSplitChildTool(
                    "Dim By Line\nGrid Only",
                    "Create a dimension string across intersecting grids using a picked line (plan, section, or elevation).",
                    typeof(CmdDimensionGridsByLine),
                    "Dimensions by Line.png",
                    "Dimensions by Line.png"),
                CreateSplitChildTool(
                    "Dim By Line\nLevel Only",
                    "Create a dimension string across levels within the picked vertical range.",
                    typeof(CmdDimensionLevelsByLine),
                    "Dimensions by Line.png",
                    "Dimensions by Line.png"));
        }

        private TopLevelToolSpec AddCopyDimensionTextTool()
        {
            return CreatePushToolSpec(
                "Copy Dim\nText",
                "Copy Above/Below/Prefix/Suffix text from one dimension to others.",
                typeof(CmdCopyDimensionText),
                "Copy Dim Text.png",
                "Copy Dim Text.png");
        }

        private TopLevelToolSpec AddResetDatumsTools()
        {
            return CreateSplitToolSpec(
                "Reset to\n3D Extents",
                "Reset grid or level datum extents back to 3D.",
                "Resetto3DExtents.png",
                "Resetto3DExtents.png",
                CreateSplitChildTool(
                    "Grids Only",
                    "Reset all visible grids to 3D extents in this view.",
                    typeof(CmdResetDatumsGrids),
                    "Resetto3DExtents.png",
                    "Resetto3DExtents.png"),
                CreateSplitChildTool(
                    "Levels Only",
                    "Reset all visible levels to 3D extents in this view.",
                    typeof(CmdResetDatumsLevels),
                    "Resetto3DExtents.png",
                    "Resetto3DExtents.png"),
                CreateSplitChildTool(
                    "Grids + Levels",
                    "Reset both grids and levels visible in this view.",
                    typeof(CmdResetDatums),
                    "Resetto3DExtents.png",
                    "Resetto3DExtents.png"));
        }

        private TopLevelToolSpec AddExtendLevelsTool()
        {
            return CreateSplitToolSpec(
                "Level\nExtents",
                "Match or maximize level 3D extents.",
                "Level Extents.png",
                "Level Extents.png",
                CreateSplitChildTool(
                    "Match Level Extents",
                    "Select one source level, then pick target levels one-by-one to match extents (Esc to finish).",
                    typeof(CmdExtendLevelsBySelected),
                    "Level Extents.png",
                    "Level Extents.png"),
                CreateSplitChildTool(
                    "Maximize by Section Box",
                    "Maximize all level 3D extents to the active 3D view's section box.",
                    typeof(CmdMaximizeLevelsBySectionBox),
                    "Level Extents.png",
                    "Level Extents.png"));
        }

        private TopLevelToolSpec AddViewCrop3DExtentsTools()
        {
            return CreateSplitToolSpec(
                "View Crop\n3D Extents",
                "Resize view crop by projected model extents.",
                "view crop 3d extents.png",
                "view crop 3d extents.png",
                CreateSplitChildTool(
                    "View Crop by Active View Elements",
                    "Fit crop per target view using only elements currently visible in that view.",
                    typeof(CmdViewCropByActiveViewElements),
                    "view crop 3d extents.png",
                    "view crop 3d extents.png"),
                CreateSplitChildTool(
                    "View Crop by All Model Elements",
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

        private TopLevelToolSpec AddFlipGridBubbleTool()
        {
            return CreatePushToolSpec(
                "Flip Grid\nBubble",
                "Toggle which grid end shows the bubble, one grid at a time.",
                typeof(CmdFlipGridBubble),
                "GridbubbleFlip.png",
                "GridbubbleFlip.png");
        }

        private TopLevelToolSpec AddSharedToFamilyTool()
        {
            return CreatePushToolSpec(
                "Shared\nTo Family",
                "Convert selected shared parameters in the active family into normal family parameters.",
                typeof(SharedParamToFamilyParamCommand),
                "Share To Family.png",
                "Share To Family.png");
        }

        private TopLevelToolSpec AddRevisionCloudByElementsTool()
        {
            return CreateSplitToolSpec(
                "Cloud By\nElements",
                "Create orthogonal stepped revision cloud boundaries and configure settings.",
                "Cloud By Elements.png",
                "Cloud By Elements.png",
                CreateSplitChildTool(
                    "Place",
                    "Create orthogonal stepped revision cloud boundaries aligned to dominant selected-element angle. Keeps running until Esc.",
                    typeof(CmdRevisionCloudByElements),
                    "Cloud By Elements.png",
                    "Cloud By Elements.png"),
                CreateSplitChildTool(
                    "Settings",
                    "Configure offset distance for Cloud By Elements.",
                    typeof(CmdRevisionCloudByElementsSettings),
                    "settings.png",
                    "settings.png"));
        }

        private TopLevelToolSpec AddResetTextTool()
        {
            return CreatePushToolSpec(
                "Reset\nText",
                "Reset selected text notes/tags back to their default text offset.",
                typeof(CmdResetTextPosition),
                "Reset Position.png",
                "Reset Position.png");
        }

        private TopLevelToolSpec AddMatchElevationTool()
        {
            return CreateSplitToolSpec(
                "Match\nElevation",
                "Match center, top, or bottom elevation from a source MEP element to others.",
                "Match Elevation.png",
                "Match Elevation.png",
                CreateSplitChildTool(
                    "By\nCenter",
                    "Match center elevation from a source MEP element to selected targets.",
                    typeof(CmdMatchElevation),
                    "Match Elevation.png",
                    "Match Elevation.png"),
                CreateSplitChildTool(
                    "By\nTop",
                    "Match top elevation from a source MEP element to selected targets.",
                    typeof(CmdMatchElevationTop),
                    "Match Elevation.png",
                    "Match Elevation.png"),
                CreateSplitChildTool(
                    "By\nBottom",
                    "Match bottom elevation from a source MEP element to selected targets.",
                    typeof(CmdMatchElevationBottom),
                    "Match Elevation.png",
                    "Match Elevation.png"));
        }

        private TopLevelToolSpec AddReassignLevelTool()
        {
            return CreatePushToolSpec(
                "Reassign\nLevel",
                "Reassign supported MEP elements from one level to another without moving them physically.",
                typeof(CmdReassignLevel),
                "Reassign Level.png",
                "Reassign Level.png");
        }

        private TopLevelToolSpec AddLocationDataTool()
        {
            return CreatePushToolSpec(
                "Location\nData",
                "Assign Room, Level, Coordinates, Altitude, and HVAC Zone data to selected categories.",
                typeof(CmdLocationDataAssigner),
                "Location Data.png",
                "Location Data.png");
        }

        private TopLevelToolSpec AddSmartConnectTool()
        {
            return CreatePushToolSpec(
                "Smart\nConnect",
                "Connect two same-category MEP elements (Pipe, Duct, Cable Tray) with routing and angle settings.",
                typeof(SmartConnectCommand),
                "SmartConnect.png",
                "SmartConnect.png");
        }

        private TopLevelToolSpec AddCeilingMagnetTool()
        {
            return CreatePushToolSpec(
                "Ceiling\nMagnet",
                "Pick ceiling, pick grid intersection, then snap point-based elements to nearest tile centers.",
                typeof(CmdCeilingMagnet),
                "cursor.png",
                "cursor.png");
        }

        private TopLevelToolSpec AddDuctFlowTools()
        {
            return CreateSplitToolSpec(
                "Duct\nFlow",
                "Duct flow annotation tools.",
                "Flowdirectioncreate.png",
                "Flowdirectioncreate.png",
                CreateSplitChildTool(
                    "Place",
                    "Place duct flow annotations along horizontal ducts.",
                    typeof(CmdFlowDirectionAnnotations),
                    "Flowdirectioncreate.png",
                    "Flowdirectioncreate.png"),
                CreateSplitChildTool(
                    "Settings",
                    "Choose the annotation family and spacing used for duct flow placement.",
                    typeof(CmdFlowDirectionSettings),
                    "settings.png",
                    "settings.png"));
        }

        private TopLevelToolSpec AddDuctStandardsTool()
        {
            return CreatePushToolSpec(
                "Duct\nStandards",
                "Calculate and write duct sheet thickness, gauge, weight, and area based on SMACNA-style rules.",
                typeof(CmdDuctStandardsManager),
                "Duct Standards.png",
                "Duct Standards.png");
        }

        private TopLevelToolSpec AddPurgeTools()
        {
            return CreateSplitToolSpec(
                "Purge",
                "Project cleanup and purge tools.",
                "Remove.png",
                "Remove.png",
                CreateSplitChildTool(
                    "Purge Unused\nFamily Parameters",
                    "Scan family parameters, classify unused candidates safely, and remove selected parameters in the active family document.",
                    typeof(CmdPurgeUnusedFamilyParameters),
                    "Remove.png",
                    "Remove.png",
                    pushButton => pushButton.AvailabilityClassName = typeof(CmdPurgeUnusedFamilyParametersAvailability).FullName));
        }

        private TopLevelToolSpec AddSmartMepTagTools()
        {
            return CreateSplitToolSpec(
                "Smart MEP\nTag",
                "Smart MEP tagging tools.",
                "Smart MEP TAG.png",
                "Smart MEP TAG.png",
                CreateSplitChildTool(
                    "Place",
                    "Analyse the active view and intelligently tag MEP elements (ducts, pipes, equipment, accessories, cable trays) with clash-free placement.",
                    typeof(CmdSmartMepTag),
                    "Smart MEP TAG.png",
                    "Smart MEP TAG.png"),
                CreateSplitChildTool(
                    "Settings",
                    "Configure category-wise enable/disable for Smart MEP Tag.",
                    typeof(CmdSmartMepTagSettings),
                    "settings.png",
                    "settings.png"));
        }

        private TopLevelToolSpec AddLShapeLeaderTool()
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
            return CreateSplitToolSpec(
                "Arrange\nTags",
                "Arrange tag tools.",
                "Arrange Tag.png",
                "Arrange Tag.png",
                CreateSplitChildTool(
                    "Place",
                    "Rearrange selected tags into a clean vertical stack. The nearest T1-to-L1 tag position is placed first, then remaining tags stack above or below based on T1 relative to L1.",
                    typeof(CmdIntelligentTagArranger),
                    "Arrange Tag.png",
                    "Arrange Tag.png"),
                CreateSplitChildTool(
                    "Settings",
                    "Set default vertical spacing for Arrange Tags (tag_spacing_mm).",
                    typeof(CmdIntelligentTagArrangerSettings),
                    "settings.png",
                    "settings.png"));
        }

        private TopLevelToolSpec AddCopySwapTextTools()
        {
            return CreateSplitToolSpec(
                "Copy Swap\nText",
                "Copy or swap text values between text notes.",
                "copyswaptext.png",
                "copyswaptext.png",
                CreateSplitChildTool(
                    "Copy Text",
                    "Copy the text value from one text note to others (click targets until ESC).",
                    typeof(CmdCopyText),
                    "copyswaptext.png",
                    "copyswaptext.png"),
                CreateSplitChildTool(
                    "Swap Text",
                    "Swap the text values between two picked text notes (one-time).",
                    typeof(CmdSwapText),
                    "copyswaptext.png",
                    "copyswaptext.png"));
        }

        private TopLevelToolSpec AddAboutTool()
        {
            return CreatePushToolSpec(
                "About\nAJ Tools",
                "Open the AJ Tools About window.",
                typeof(AboutCommand),
                "information.png",
                "information.png",
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

        private static string CreateSplitButtonName(string text)
        {
            var normalizedText = (text ?? string.Empty)
                .Replace("\n", string.Empty)
                .Replace(" ", string.Empty);
            return $"split_{normalizedText}";
        }
    }
}
