// Tool Name: Ribbon Manager
// Description: Builds the AJ Tools ribbon UI and registers push buttons and split buttons.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI, System.Reflection, System.Windows.Media.Imaging, AJTools.Commands, AJTools.Utils
using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using AJTools.Commands;
using AJTools.Utils;

namespace AJTools.App
{
    /// <summary>
    /// Builds the AJ Tools ribbon tab, panels, and buttons when Revit starts.
    /// </summary>
    internal class RibbonManager
    {
        private readonly UIControlledApplication _app;
        private readonly string _assemblyPath;
        private readonly IconLoader _iconLoader;

        private const string TabName = "AJ Tools";

        /// <summary>
        /// Initializes a new RibbonManager bound to the current Revit application.
        /// </summary>
        public RibbonManager(UIControlledApplication app)
        {
            _app = app;
            _assemblyPath = Assembly.GetExecutingAssembly().Location;
            _iconLoader = new IconLoader(_assemblyPath);
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
                // Tab already exists—safe to continue building panels.
            }

            CreateGraphicsPanel();
            CreateLinksPanel();
            CreateDimensionsPanel();
            CreateDatumsPanel();
            CreateMepPanel();
            CreateAnnotationsPanel();
            CreateInfoPanel();
        }

        private void CreateGraphicsPanel()
        {
            var panel = GetOrCreatePanel("Graphics");

            CreatePushButton(panel, "Toggle\nLinks", "Toggle visibility of all Revit Links in the active view.", typeof(CmdToggleRevitLinks), "Toggle Links.png", "Toggle Links.png");
            CreatePushButton(panel, "Unhide\nAll", "Unhide all elements in the active view (Temporary Hide/Isolate + hidden items).", typeof(CmdUnhideAll), "Unhide All.png", "Unhide All.png");
            CreatePushButton(panel, "Reset\nGraphics", "Clear per-element graphic overrides in the active view.", typeof(CmdResetOverrides), "Reset Overrides.png", "Reset Overrides.png");
        }

        private void CreateLinksPanel()
        {
            var panel = GetOrCreatePanel("Links");
            var pulldownButton = CreatePulldownButton(panel, "Element\nID", "Element ID tools for current and linked models.", "linkedID.png", "linkedID.png");
            CreatePushButton(pulldownButton, "Linked ID of\nSelection", "Pick any element (model or linked) and view its Element ID with source info.", typeof(CmdLinkedElementIdViewer), "Linked ID of Selection.png", "Linked ID of Selection.png");
            CreatePushButton(pulldownButton, "View by\nLinked ID", "Search by Element ID in current or linked models and zoom to it.", typeof(CmdLinkedElementSearch), "View by Linked ID.png", "View by Linked ID.png");
            CreatePushButton(panel, "Set Link\nWorkset", "Assign selected Revit links and CAD imports to a workset.", typeof(CmdSetLinkWorkset), "Set Link Workset.png", "Set Link Workset.png");
        }

        private void CreateDimensionsPanel()
        {
            var panel = GetOrCreatePanel("Dimensions");
            var autoDimsPulldown = CreatePulldownButton(panel, "Auto\nDims", "Dimension grids and levels automatically.", "Dimensions.png", "Dimensions.png");
            CreatePushButton(autoDimsPulldown, "Grids Only", "Create horizontal/vertical grid dimension strings in plan views.", typeof(CmdAutoDimensionsGrids), "Dimensions.png", "Dimensions.png");
            CreatePushButton(autoDimsPulldown, "Levels Only", "Create level dimension strings in section or elevation views.", typeof(CmdAutoDimensionsLevels), "Dimensions.png", "Dimensions.png");
            CreatePushButton(autoDimsPulldown, "Grids + Levels", "Plan views: dimension grids. Sections/Elevations: dimension levels and grids.", typeof(CmdAutoDimensions), "Dimensions.png", "Dimensions.png");

            var dimsByLinePulldown = CreatePulldownButton(panel, "Dim By\nLine", "Pick two points to place grid or level dimensions along a custom line.", "Dimensions by Line.png", "Dimensions by Line.png");
            CreatePushButton(dimsByLinePulldown, "Dim By Line\nGrid Only", "Create a dimension string across intersecting grids using a picked line (plan, section, or elevation).", typeof(CmdDimensionGridsByLine), "Dimensions by Line.png", "Dimensions by Line.png");
            CreatePushButton(dimsByLinePulldown, "Dim By Line\nLevel Only", "Create a dimension string across levels within the picked vertical range.", typeof(CmdDimensionLevelsByLine), "Dimensions by Line.png", "Dimensions by Line.png");

            CreatePushButton(panel, "Copy Dim\nText", "Copy Above/Below/Prefix/Suffix text from one dimension to others.", typeof(CmdCopyDimensionText), "Copy Dim Text.png", "Copy Dim Text.png");
        }

        private void CreateDatumsPanel()
        {
            var panel = GetOrCreatePanel("Datums");
            var resetDatumsPulldown = CreatePulldownButton(panel, "Reset to\n3D Extents", "Reset grid or level datum extents back to 3D.", "Resetto3DExtents.png", "Resetto3DExtents.png");
            CreatePushButton(resetDatumsPulldown, "Grids Only", "Reset all visible grids to 3D extents in this view.", typeof(CmdResetDatumsGrids), "Resetto3DExtents.png", "Resetto3DExtents.png");
            CreatePushButton(resetDatumsPulldown, "Levels Only", "Reset all visible levels to 3D extents in this view.", typeof(CmdResetDatumsLevels), "Resetto3DExtents.png", "Resetto3DExtents.png");
            CreatePushButton(resetDatumsPulldown, "Grids + Levels", "Reset both grids and levels visible in this view.", typeof(CmdResetDatums), "Resetto3DExtents.png", "Resetto3DExtents.png");

            CreatePushButton(panel, "Flip Grid\nBubble", "Toggle which grid end shows the bubble, one grid at a time.", typeof(CmdFlipGridBubble), "GridbubbleFlip.png", "GridbubbleFlip.png");
        }

        private void CreateMepPanel()
        {
            var panel = GetOrCreatePanel("MEP");
            CreatePushButton(panel, "Match\nElevation", "Match the middle elevation from a source MEP element to others.", typeof(CmdMatchElevation), "Match Elevation.png", "Match Elevation.png");
            var flowPulldown = CreatePulldownButton(panel, "Flow\nDirection", "Flow direction annotation tools.", "Flowdirection.png", "Flowdirection.png");
            CreatePushButton(flowPulldown, "Place", "Place flow direction annotations along ducts and pipes.", typeof(CmdFlowDirectionAnnotations), "Flowdirectioncreate.png", "Flowdirectioncreate.png");
            CreatePushButton(flowPulldown, "Settings", "Choose the annotation family and spacing used for flow direction placement.", typeof(CmdFlowDirectionSettings), "settings.png", "settings.png");
            var filterProButton = CreatePushButton(panel, "Filter\nPro", "Create parameter filters quickly (category, parameter, values) and apply them to the active view.", typeof(CmdFilterPro), "FilterPro.png", "FilterPro.png");
            filterProButton.AvailabilityClassName = typeof(CmdFilterProAvailability).FullName;
        }

        private void CreateAnnotationsPanel()
        {
            var panel = GetOrCreatePanel("Annotations");
            CreatePushButton(panel, "L-Shape\nLeader", "Force tags to use a right-angle leader. Run again on the same tag to flip the elbow side. Preselect tags or pick tags (Tab cycles) until Esc.", typeof(CmdForceTagLeaderLShape), "L-ShapeLeader.png", "L-ShapeLeader.png");
            CreatePushButton(panel, "Reset\nText", "Reset selected text notes/tags back to their default text offset.", typeof(CmdResetTextPosition), "Reset Position.png", "Reset Position.png");

            var copySwapPulldown = CreatePulldownButton(panel, "Copy Swap\nText", "Copy or swap text values between text notes.", "copyswaptext.png", "copyswaptext.png");
            CreatePushButton(copySwapPulldown, "Copy Text", "Copy the text value from one text note to others (click targets until ESC).", typeof(CmdCopyText), "copyswaptext.png", "copyswaptext.png");
            CreatePushButton(copySwapPulldown, "Swap Text", "Swap the text values between two picked text notes (one-time).", typeof(CmdSwapText), "copyswaptext.png", "copyswaptext.png");
        }

        private void CreateInfoPanel()
        {
            var panel = GetOrCreatePanel("Info");
            CreatePushButton(panel, "About", "About this AJ Tools add-in.", typeof(CmdAbout), "information.png", "information.png");
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
