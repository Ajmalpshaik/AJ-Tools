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
using System.Windows.Media.Imaging;
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
                new ToolPlacement(PanelKey.Graphics, AddToggleLinksTool),
                new ToolPlacement(PanelKey.Graphics, AddUnhideAllTool),
                new ToolPlacement(PanelKey.Graphics, AddFilterProTool),
                new ToolPlacement(PanelKey.Graphics, AddApplyGraphicsTools),
                new ToolPlacement(PanelKey.Graphics, AddMatchGraphicsTools),
                new ToolPlacement(PanelKey.Graphics, AddResetGraphicsTools),
                new ToolPlacement(PanelKey.Graphics, Add3DViewsTools),

                new ToolPlacement(PanelKey.Links, AddElementIdTools),
                new ToolPlacement(PanelKey.Links, AddSetLinkWorksetTool),

                new ToolPlacement(PanelKey.Dimensions, AddAutoDimensionsTools),
                new ToolPlacement(PanelKey.Dimensions, AddDimensionByLineTools),
                new ToolPlacement(PanelKey.Dimensions, AddCopyDimensionTextTool),

                new ToolPlacement(PanelKey.Family, AddSharedToFamilyTool),

                new ToolPlacement(PanelKey.Mep, AddMatchElevationTool),
                new ToolPlacement(PanelKey.Mep, AddSmartConnectTool),
                new ToolPlacement(PanelKey.Mep, AddCeilingMagnetTool),

                new ToolPlacement(PanelKey.DataStandards, AddLocationDataTool),
                new ToolPlacement(PanelKey.DataStandards, AddDuctStandardsTool),

                new ToolPlacement(PanelKey.Annotations, AddSmartMepTagTools),
                new ToolPlacement(PanelKey.Annotations, AddDuctFlowTools),
                new ToolPlacement(PanelKey.Annotations, AddRevisionCloudByElementsTool),
                new ToolPlacement(PanelKey.Annotations, AddResetTextTool),
                new ToolPlacement(PanelKey.Annotations, AddResetDatumsTools),
                new ToolPlacement(PanelKey.Annotations, AddExtendLevelsTool),
                new ToolPlacement(PanelKey.Annotations, AddFlipGridBubbleTool),
                new ToolPlacement(PanelKey.Annotations, AddViewCrop3DExtentsTools),
                new ToolPlacement(PanelKey.Annotations, AddLShapeLeaderTool),
                new ToolPlacement(PanelKey.Annotations, AddArrangeTagsTools),
                new ToolPlacement(PanelKey.Annotations, AddCopySwapTextTools),

                new ToolPlacement(PanelKey.Info, AddAboutTool)
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

        private void AddToolsToPanels(IReadOnlyDictionary<PanelKey, RibbonPanel> panels)
        {
            foreach (var placement in _toolLayout)
            {
                placement.BuildTools(panels[placement.PanelKey]);
            }
        }

        private void AddToggleLinksTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Toggle\nLinks",
                "Toggle visibility of all Revit Links in the active view.",
                typeof(CmdToggleRevitLinks),
                "Toggle Links.png",
                "Toggle Links.png");
        }

        private void AddUnhideAllTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Unhide\nAll",
                "Unhide all elements in the active view (Temporary Hide/Isolate + hidden items).",
                typeof(CmdUnhideAll),
                "Unhide All.png",
                "Unhide All.png");
        }

        private void AddFilterProTool(RibbonPanel panel)
        {
            var filterProButton = CreatePushButton(
                panel,
                "Filter\nPro",
                "Create parameter filters quickly (category, parameter, values) and apply them to the active view.",
                typeof(CmdFilterPro),
                "FilterPro.png",
                "FilterPro.png");
            filterProButton.AvailabilityClassName = typeof(CmdFilterProAvailability).FullName;
        }

        private void AddApplyGraphicsTools(RibbonPanel panel)
        {
            var applyGraphicsPulldown = CreatePulldownButton(
                panel,
                "Apply\nGraphics",
                "Apply category or element graphics overrides in the active view.",
                "apply.png",
                "apply.png");

            CreatePushButton(
                applyGraphicsPulldown,
                "Category\nGraphics",
                "Apply graphics overrides to unique model categories from selected elements in the active view.",
                typeof(CmdCategoryGraphics),
                "apply.png",
                "apply.png");
            CreatePushButton(
                applyGraphicsPulldown,
                "Element\nGraphics",
                "Apply element-level graphics overrides to selected elements in the active view.",
                typeof(CmdElementGraphics),
                "apply.png",
                "apply.png");
        }

        private void AddMatchGraphicsTools(RibbonPanel panel)
        {
            var matchGraphicsPulldown = CreatePulldownButton(
                panel,
                "Match\nGraphics",
                "Match category or element graphics from a picked source.",
                "copy.png",
                "copy.png");
            CreatePushButton(
                matchGraphicsPulldown,
                "Match Category\nGraphics",
                "Copy category graphics from one source category and apply them to selected target categories.",
                typeof(CmdMatchCategoryGraphics),
                "copy.png",
                "copy.png");
            CreatePushButton(
                matchGraphicsPulldown,
                "Match Element\nGraphics",
                "Copy element-level graphics from one source element to selected target elements.",
                typeof(CmdMatchElementGraphics),
                "copy.png",
                "copy.png");
        }

        private void AddResetGraphicsTools(RibbonPanel panel)
        {
            var resetGraphicsPulldown = CreatePulldownButton(
                panel,
                "Reset\nGraphics",
                "Reset category and element graphics overrides in the active view.",
                "Reset Overrides.png",
                "Reset Overrides.png");
            CreatePushButton(
                resetGraphicsPulldown,
                "Reset Category\nBy Selection",
                "Reset category graphics overrides using selected model elements in the active view.",
                typeof(CmdResetCategoryGraphics),
                "Reset Overrides.png",
                "Reset Overrides.png");
            CreatePushButton(
                resetGraphicsPulldown,
                "Reset Category\nAll Elements",
                "Reset category graphics overrides for model categories found from all elements in the active view.",
                typeof(CmdResetCategoryGraphicsAllElements),
                "Reset Overrides.png",
                "Reset Overrides.png");
            CreatePushButton(
                resetGraphicsPulldown,
                "Reset Element\nBy Selection",
                "Reset element-level graphics overrides for selected elements in the active view.",
                typeof(CmdClearSelectedElementGraphics),
                "Reset Overrides.png",
                "Reset Overrides.png");
            CreatePushButton(
                resetGraphicsPulldown,
                "Reset Element\nAll Elements",
                "Reset all element-level graphics overrides in the active view.",
                typeof(CmdResetOverrides),
                "Reset Overrides.png",
                "Reset Overrides.png");
        }

        private void Add3DViewsTools(RibbonPanel panel)
        {
            var threeDViewsPulldown = CreatePulldownButton(
                panel,
                "3D\nViews",
                "3D view tools.",
                "3D Views.png",
                "3D Views.png");
            CreatePushButton(
                threeDViewsPulldown,
                "Create 3D View As Per Workset",
                "Create one 3D view per user workset and isolate each workset in its matching view.",
                typeof(Cmd3DViewsAsPerWorkset),
                "3D Views.png",
                "3D Views.png");
        }

        private void AddElementIdTools(RibbonPanel panel)
        {
            var pulldownButton = CreatePulldownButton(
                panel,
                "Element\nID",
                "Element ID tools for current and linked models.",
                "linkedID.png",
                "linkedID.png");
            CreatePushButton(
                pulldownButton,
                "Linked ID of\nSelection",
                "Pick any element (model or linked) and view its Element ID with source info.",
                typeof(CmdLinkedElementIdViewer),
                "Linked ID of Selection.png",
                "Linked ID of Selection.png");
            CreatePushButton(
                pulldownButton,
                "View by\nLinked ID",
                "Search by Element ID in current or linked models and zoom to it.",
                typeof(CmdLinkedElementSearch),
                "View by Linked ID.png",
                "View by Linked ID.png");
        }

        private void AddSetLinkWorksetTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Set Link\nWorkset",
                "Assign selected Revit links and CAD imports to a workset.",
                typeof(CmdSetLinkWorkset),
                "Set Link Workset.png",
                "Set Link Workset.png");
        }

        private void AddAutoDimensionsTools(RibbonPanel panel)
        {
            var autoDimsPulldown = CreatePulldownButton(
                panel,
                "Auto\nDims",
                "Dimension grids and levels automatically.",
                "Dimensions.png",
                "Dimensions.png");
            CreatePushButton(
                autoDimsPulldown,
                "Grids Only",
                "Create horizontal/vertical grid dimension strings in plan views.",
                typeof(CmdAutoDimensionsGrids),
                "Dimensions.png",
                "Dimensions.png");
            CreatePushButton(
                autoDimsPulldown,
                "Levels Only",
                "Create level dimension strings in section or elevation views.",
                typeof(CmdAutoDimensionsLevels),
                "Dimensions.png",
                "Dimensions.png");
            CreatePushButton(
                autoDimsPulldown,
                "Grids + Levels",
                "Plan views: dimension grids. Sections/Elevations: dimension levels and grids.",
                typeof(CmdAutoDimensions),
                "Dimensions.png",
                "Dimensions.png");
        }

        private void AddDimensionByLineTools(RibbonPanel panel)
        {
            var dimsByLinePulldown = CreatePulldownButton(
                panel,
                "Dim By\nLine",
                "Pick two points to place grid or level dimensions along a custom line.",
                "Dimensions by Line.png",
                "Dimensions by Line.png");
            CreatePushButton(
                dimsByLinePulldown,
                "Quick\nCenter Line",
                "Quickly create a dimension string for selected parallel elements using center line references.",
                typeof(CmdQuickParallelCenterLineDimension),
                "Dimensions by Line.png",
                "Dimensions by Line.png");
            CreatePushButton(
                dimsByLinePulldown,
                "Quick\nFace/Edge",
                "Quickly create dimensions using both side faces/edges for each selected parallel element (for ducts/pipes this captures both sides).",
                typeof(CmdQuickParallelFaceEdgeDimension),
                "Dimensions by Line.png",
                "Dimensions by Line.png");
            CreatePushButton(
                dimsByLinePulldown,
                "Dim By Line\nGrid Only",
                "Create a dimension string across intersecting grids using a picked line (plan, section, or elevation).",
                typeof(CmdDimensionGridsByLine),
                "Dimensions by Line.png",
                "Dimensions by Line.png");
            CreatePushButton(
                dimsByLinePulldown,
                "Dim By Line\nLevel Only",
                "Create a dimension string across levels within the picked vertical range.",
                typeof(CmdDimensionLevelsByLine),
                "Dimensions by Line.png",
                "Dimensions by Line.png");
        }

        private void AddCopyDimensionTextTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Copy Dim\nText",
                "Copy Above/Below/Prefix/Suffix text from one dimension to others.",
                typeof(CmdCopyDimensionText),
                "Copy Dim Text.png",
                "Copy Dim Text.png");
        }

        private void AddResetDatumsTools(RibbonPanel panel)
        {
            var resetDatumsPulldown = CreatePulldownButton(
                panel,
                "Reset to\n3D Extents",
                "Reset grid or level datum extents back to 3D.",
                "Resetto3DExtents.png",
                "Resetto3DExtents.png");
            CreatePushButton(
                resetDatumsPulldown,
                "Grids Only",
                "Reset all visible grids to 3D extents in this view.",
                typeof(CmdResetDatumsGrids),
                "Resetto3DExtents.png",
                "Resetto3DExtents.png");
            CreatePushButton(
                resetDatumsPulldown,
                "Levels Only",
                "Reset all visible levels to 3D extents in this view.",
                typeof(CmdResetDatumsLevels),
                "Resetto3DExtents.png",
                "Resetto3DExtents.png");
            CreatePushButton(
                resetDatumsPulldown,
                "Grids + Levels",
                "Reset both grids and levels visible in this view.",
                typeof(CmdResetDatums),
                "Resetto3DExtents.png",
                "Resetto3DExtents.png");
        }

        private void AddExtendLevelsTool(RibbonPanel panel)
        {
            var levelExtentsPulldown = CreatePulldownButton(
                panel,
                "Level\nExtents",
                "Match or maximize level 3D extents.",
                "Resetto3DExtents.png",
                "Resetto3DExtents.png");
            CreatePushButton(
                levelExtentsPulldown,
                "Match Level Extents",
                "Select one source level, then pick target levels one-by-one to match extents (Esc to finish).",
                typeof(CmdExtendLevelsBySelected),
                "Resetto3DExtents.png",
                "Resetto3DExtents.png");
            CreatePushButton(
                levelExtentsPulldown,
                "Maximize by Section Box",
                "Maximize all level 3D extents to the active 3D view's section box.",
                typeof(CmdMaximizeLevelsBySectionBox),
                "Resetto3DExtents.png",
                "Resetto3DExtents.png");
        }

        private void AddViewCrop3DExtentsTools(RibbonPanel panel)
        {
            var viewCropPulldown = CreatePulldownButton(
                panel,
                "View Crop\n3D Extents",
                "Resize view crop by projected model extents.",
                "view crop 3d extents.png",
                "view crop 3d extents.png");
            CreatePushButton(
                viewCropPulldown,
                "View Crop by Active View Elements",
                "Fit crop per target view using only elements currently visible in that view.",
                typeof(CmdViewCropByActiveViewElements),
                "view crop 3d extents.png",
                "view crop 3d extents.png");
            CreatePushButton(
                viewCropPulldown,
                "View Crop by All Model Elements",
                "Fit crop per target view using project-wide model extents projected to that view.",
                typeof(CmdViewCropByAllModelElements),
                "view crop 3d extents.png",
                "view crop 3d extents.png");
            CreatePushButton(
                viewCropPulldown,
                "Set Annotation Crop\nby View Crop",
                "Enable annotation crop in selected views and set equal offsets on all sides using each view's active crop box.",
                typeof(CmdSetAnnotationCropByViewCrop),
                "view crop 3d extents.png",
                "view crop 3d extents.png");
        }

        private void AddFlipGridBubbleTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Flip Grid\nBubble",
                "Toggle which grid end shows the bubble, one grid at a time.",
                typeof(CmdFlipGridBubble),
                "GridbubbleFlip.png",
                "GridbubbleFlip.png");
        }

        private void AddSharedToFamilyTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Shared\nTo Family",
                "Convert selected shared parameters in the active family into normal family parameters.",
                typeof(SharedParamToFamilyParamCommand),
                "Share To Family.png",
                "Share To Family.png");
        }

        private void AddRevisionCloudByElementsTool(RibbonPanel panel)
        {
            var cloudByElementsPulldown = CreatePulldownButton(
                panel,
                "Cloud By\nElements",
                "Create orthogonal stepped revision cloud boundaries and configure settings.",
                "Cloud By Elements.png",
                "Cloud By Elements.png");

            CreatePushButton(
                cloudByElementsPulldown,
                "Place",
                "Create orthogonal stepped revision cloud boundaries aligned to dominant selected-element angle. Keeps running until Esc.",
                typeof(CmdRevisionCloudByElements),
                "Cloud By Elements.png",
                "Cloud By Elements.png");

            CreatePushButton(
                cloudByElementsPulldown,
                "Settings",
                "Configure offset distance for Cloud By Elements.",
                typeof(CmdRevisionCloudByElementsSettings),
                "settings.png",
                "settings.png");
        }

        private void AddResetTextTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Reset\nText",
                "Reset selected text notes/tags back to their default text offset.",
                typeof(CmdResetTextPosition),
                "Reset Position.png",
                "Reset Position.png");
        }

        private void AddMatchElevationTool(RibbonPanel panel)
        {
            var matchElevationPulldown = CreatePulldownButton(
                panel,
                "Match\nElevation",
                "Match center, top, or bottom elevation from a source MEP element to others.",
                "Match Elevation.png",
                "Match Elevation.png");

            CreatePushButton(
                matchElevationPulldown,
                "By\nCenter",
                "Match center elevation from a source MEP element to selected targets.",
                typeof(CmdMatchElevation),
                "Match Elevation.png",
                "Match Elevation.png");
            CreatePushButton(
                matchElevationPulldown,
                "By\nTop",
                "Match top elevation from a source MEP element to selected targets.",
                typeof(CmdMatchElevationTop),
                "Match Elevation.png",
                "Match Elevation.png");
            CreatePushButton(
                matchElevationPulldown,
                "By\nBottom",
                "Match bottom elevation from a source MEP element to selected targets.",
                typeof(CmdMatchElevationBottom),
                "Match Elevation.png",
                "Match Elevation.png");
        }

        private void AddLocationDataTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Location\nData",
                "Assign Room, Level, Coordinates, Altitude, and HVAC Zone data to selected categories.",
                typeof(CmdLocationDataAssigner),
                "Location Data.png",
                "Location Data.png");
        }

        private void AddSmartConnectTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Smart\nConnect",
                "Connect two same-category MEP elements (Pipe, Duct, Cable Tray) with routing and angle settings.",
                typeof(SmartConnectCommand),
                "SmartConnect.png",
                "SmartConnect.png");
        }

        private void AddCeilingMagnetTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Ceiling\nMagnet",
                "Pick ceiling, pick grid intersection, then snap point-based elements to nearest tile centers.",
                typeof(CmdCeilingMagnet),
                "cursor.png",
                "cursor.png");
        }

        private void AddDuctFlowTools(RibbonPanel panel)
        {
            var flowPulldown = CreatePulldownButton(
                panel,
                "Duct\nFlow",
                "Duct flow annotation tools.",
                "Flowdirectioncreate.png",
                "Flowdirectioncreate.png");
            CreatePushButton(
                flowPulldown,
                "Place",
                "Place duct flow annotations along horizontal ducts.",
                typeof(CmdFlowDirectionAnnotations),
                "Flowdirectioncreate.png",
                "Flowdirectioncreate.png");
            CreatePushButton(
                flowPulldown,
                "Settings",
                "Choose the annotation family and spacing used for duct flow placement.",
                typeof(CmdFlowDirectionSettings),
                "settings.png",
                "settings.png");
        }

        private void AddDuctStandardsTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "Duct\nStandards",
                "Calculate and write duct sheet thickness, gauge, weight, and area based on SMACNA-style rules.",
                typeof(CmdDuctStandardsManager),
                "Duct Standards.png",
                "Duct Standards.png");
        }

        private void AddSmartMepTagTools(RibbonPanel panel)
        {
            var smartTagPulldown = CreatePulldownButton(
                panel,
                "Smart MEP\nTag",
                "Smart MEP tagging tools.",
                "Smart MEP TAG.png",
                "Smart MEP TAG.png");
            CreatePushButton(
                smartTagPulldown,
                "Place",
                "Analyse the active view and intelligently tag MEP elements (ducts, pipes, equipment, accessories, cable trays) with clash-free placement.",
                typeof(CmdSmartMepTag),
                "Smart MEP TAG.png",
                "Smart MEP TAG.png");
            CreatePushButton(
                smartTagPulldown,
                "Settings",
                "Configure category-wise enable/disable for Smart MEP Tag.",
                typeof(CmdSmartMepTagSettings),
                "settings.png",
                "settings.png");
        }

        private void AddLShapeLeaderTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "L-Shape\nLeader",
                "Force tags to use a right-angle leader. Run again on the same tag to flip the elbow side. Preselect tags or pick tags (Tab cycles) until Esc.",
                typeof(CmdForceTagLeaderLShape),
                "L-ShapeLeader.png",
                "L-ShapeLeader.png");
        }

        private void AddArrangeTagsTools(RibbonPanel panel)
        {
            var arrangeTagsPulldown = CreatePulldownButton(
                panel,
                "Arrange\nTags",
                "Arrange tag tools.",
                "Arrange Tag.png",
                "Arrange Tag.png");
            CreatePushButton(
                arrangeTagsPulldown,
                "Place",
                "Rearrange selected tags into a clean vertical stack. The nearest T1-to-L1 tag position is placed first, then remaining tags stack above or below based on T1 relative to L1.",
                typeof(CmdIntelligentTagArranger),
                "Arrange Tag.png",
                "Arrange Tag.png");
            CreatePushButton(
                arrangeTagsPulldown,
                "Settings",
                "Set default vertical spacing for Arrange Tags (tag_spacing_mm).",
                typeof(CmdIntelligentTagArrangerSettings),
                "settings.png",
                "settings.png");
        }

        private void AddCopySwapTextTools(RibbonPanel panel)
        {
            var copySwapPulldown = CreatePulldownButton(
                panel,
                "Copy Swap\nText",
                "Copy or swap text values between text notes.",
                "copyswaptext.png",
                "copyswaptext.png");
            CreatePushButton(
                copySwapPulldown,
                "Copy Text",
                "Copy the text value from one text note to others (click targets until ESC).",
                typeof(CmdCopyText),
                "copyswaptext.png",
                "copyswaptext.png");
            CreatePushButton(
                copySwapPulldown,
                "Swap Text",
                "Swap the text values between two picked text notes (one-time).",
                typeof(CmdSwapText),
                "copyswaptext.png",
                "copyswaptext.png");
        }

        private void AddAboutTool(RibbonPanel panel)
        {
            CreatePushButton(
                panel,
                "About\nAJ Tools",
                "View AJ Tools version, capabilities, and support contact.",
                typeof(CmdAbout),
                "information.png",
                "information.png");
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
        /// Adds a push button to a ribbon panel with the provided command and icon file names.
        /// </summary>
        private PushButton CreatePushButton(RibbonPanel panel, string text, string tooltip, Type command, string largeIconFileName, string smallIconFileName)
        {
            var pushButton = panel.AddItem(CreatePushButtonData(text, command)) as PushButton;
            ConfigurePushButton(
                pushButton,
                tooltip,
                _iconLoader.LoadLarge(largeIconFileName),
                _iconLoader.LoadSmall(smallIconFileName));
            return pushButton;
        }

        /// <summary>
        /// Adds a push button to a pull-down menu with the provided command and icon file names.
        /// </summary>
        private PushButton CreatePushButton(PulldownButton pulldown, string text, string tooltip, Type command, string largeIconFileName, string smallIconFileName)
        {
            var pushButton = pulldown.AddPushButton(CreatePushButtonData(text, command));
            ConfigurePushButton(
                pushButton,
                tooltip,
                _iconLoader.LoadLarge(largeIconFileName),
                _iconLoader.LoadSmall(smallIconFileName));
            return pushButton;
        }

        /// <summary>
        /// Creates button data pointing at the given external command type.
        /// </summary>
        private PushButtonData CreatePushButtonData(string text, Type command)
        {
            return new PushButtonData($"cmd{command.Name}", text, _assemblyPath, command.FullName);
        }

        /// <summary>
        /// Applies tooltip and icons to a button.
        /// </summary>
        private static void ConfigurePushButton(PushButton pushButton, string tooltip, BitmapSource largeIcon, BitmapSource smallIcon)
        {
            if (pushButton == null)
            {
                return;
            }

            pushButton.ToolTip = tooltip;
            if (largeIcon != null)
            {
                pushButton.LargeImage = largeIcon;
            }
            if (smallIcon != null)
            {
                pushButton.Image = smallIcon;
            }
        }

        /// <summary>
        /// Adds a pull-down button to a panel with the supplied tooltip and icon file names.
        /// </summary>
        private PulldownButton CreatePulldownButton(RibbonPanel panel, string text, string tooltip, string largeIconFileName, string smallIconFileName)
        {
            var pulldownData = new PulldownButtonData($"pulldown_{text.Replace("\n", "")}", text);
            var pulldownButton = panel.AddItem(pulldownData) as PulldownButton;
            if (pulldownButton != null)
            {
                pulldownButton.ToolTip = tooltip;
                var largeIcon = _iconLoader.LoadLarge(largeIconFileName);
                if (largeIcon != null)
                {
                    pulldownButton.LargeImage = largeIcon;
                }
                var smallIcon = _iconLoader.LoadSmall(smallIconFileName);
                if (smallIcon != null)
                {
                    pulldownButton.Image = smallIcon;
                }
            }

            return pulldownButton;
        }
    }
}
