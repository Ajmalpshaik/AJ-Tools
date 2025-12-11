// Tool Name: Ribbon Manager
// Description: Builds the AJ Tools ribbon UI and registers push buttons and split buttons.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI, System.IO, System.Reflection, System.Windows.Media.Imaging, AJTools.Commands
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using AJTools.Commands;

namespace AJTools
{
    /// <summary>
    /// Builds the AJ Tools ribbon tab, panels, and buttons when Revit starts.
    /// </summary>
    internal class RibbonManager
    {
        private readonly UIControlledApplication _app;
        private readonly string _assemblyPath;
        private readonly string _assemblyFolder;

        private const string TabName = "AJ Tools";

        /// <summary>
        /// Initializes a new RibbonManager bound to the current Revit application.
        /// </summary>
        public RibbonManager(UIControlledApplication app)
        {
            _app = app;
            _assemblyPath = Assembly.GetExecutingAssembly().Location;
            _assemblyFolder = Path.GetDirectoryName(_assemblyPath);
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
            CreateViewsPanel();
            CreateMepPanel();
            CreateAnnotationsPanel();
            CreateInfoPanel();
        }

        private void CreateGraphicsPanel()
        {
            var panel = GetOrCreatePanel("Graphics");
            var toggleIcon = LoadIcon("ToggleLinks.png");
            var unhideIcon = LoadIcon("UnhideAll.png");
            var resetIcon = LoadIcon("ResetOverrides.png");

            CreatePushButton(panel, "Toggle\nLinks", "Toggle visibility of all Revit Links in the active view.", typeof(CmdToggleRevitLinks), toggleIcon);
            CreatePushButton(panel, "Unhide\nAll", "Unhide all elements in the active view (Temporary Hide/Isolate + hidden items).", typeof(CmdUnhideAll), unhideIcon);
            CreatePushButton(panel, "Reset\nGraphics", "Clear per-element graphic overrides in the active view.", typeof(CmdResetOverrides), resetIcon);
        }

        private void CreateLinksPanel()
        {
            var panel = GetOrCreatePanel("Links");
            var linkedIdIcon = LoadIcon("linkedID.png");

            var pulldownButton = CreatePulldownButton(panel, "Element\nID", "Element ID tools for current and linked models.", linkedIdIcon);
            CreatePushButton(pulldownButton, "Linked ID of\nSelection", "Pick any element (model or linked) and view its Element ID with source info.", typeof(CmdLinkedElementIdViewer), linkedIdIcon);
            CreatePushButton(pulldownButton, "View by\nLinked ID", "Search by Element ID in current or linked models and zoom to it.", typeof(CmdLinkedElementSearch), linkedIdIcon);
        }

        private void CreateDimensionsPanel()
        {
            var panel = GetOrCreatePanel("Dimensions");
            var dimensionsIcon = LoadIcon("Dimensions.png");
            var dimByLineIcon = LoadIcon("Dimensions by Line.png");
            var copyDimTextIcon = LoadIcon("Copy Dim Text.png");

            var autoDimsPulldown = CreatePulldownButton(panel, "Auto\nDims", "Dimension grids and levels automatically.", dimensionsIcon);
            CreatePushButton(autoDimsPulldown, "Grids Only", "Create horizontal/vertical grid dimension strings in plan views.", typeof(CmdAutoDimensionsGrids), dimensionsIcon);
            CreatePushButton(autoDimsPulldown, "Levels Only", "Create level dimension strings in section or elevation views.", typeof(CmdAutoDimensionsLevels), dimensionsIcon);
            CreatePushButton(autoDimsPulldown, "Grids + Levels", "Plan views: dimension grids. Sections/Elevations: dimension levels and grids.", typeof(CmdAutoDimensions), dimensionsIcon);

            var dimsByLinePulldown = CreatePulldownButton(panel, "Dims by\nLine", "Pick two points to place grid or level dimensions along a custom line.", dimByLineIcon);
            CreatePushButton(dimsByLinePulldown, "Grids by Line", "Create a dimension string across intersecting grids using a picked line.", typeof(CmdDimensionGridsByLine), dimByLineIcon);
            CreatePushButton(dimsByLinePulldown, "Levels by Line", "Create a dimension string across levels within the picked vertical range.", typeof(CmdDimensionLevelsByLine), dimByLineIcon);

            CreatePushButton(panel, "Copy Dim\nText", "Copy Above/Below/Prefix/Suffix text from one dimension to others.", typeof(CmdCopyDimensionText), copyDimTextIcon);
        }

        private void CreateDatumsPanel()
        {
            var panel = GetOrCreatePanel("Datums");
            var datumIcon = LoadIcon("Resetto3DExtents.png");
            var flipGridBubbleIcon = LoadIcon("Grid bubble Flip.png");

            var resetDatumsPulldown = CreatePulldownButton(panel, "Reset to\n3D Extents", "Reset grid or level datum extents back to 3D.", datumIcon);
            CreatePushButton(resetDatumsPulldown, "Grids Only", "Reset all visible grids to 3D extents in this view.", typeof(CmdResetDatumsGrids), datumIcon);
            CreatePushButton(resetDatumsPulldown, "Levels Only", "Reset all visible levels to 3D extents in this view.", typeof(CmdResetDatumsLevels), datumIcon);
            CreatePushButton(resetDatumsPulldown, "Grids + Levels", "Reset both grids and levels visible in this view.", typeof(CmdResetDatums), datumIcon);

            CreatePushButton(panel, "Flip Grid\nBubble", "Toggle which grid end shows the bubble, one grid at a time.", typeof(CmdFlipGridBubble), flipGridBubbleIcon);
        }

        private void CreateViewsPanel()
        {
            var panel = GetOrCreatePanel("Views");
            var copyViewRangeIcon = LoadIcon("Copy View Range.png");
            CreatePushButton(panel, "Copy View\nRange", "Copy the active plan view's range and paste it to other plan views.", typeof(CmdCopyViewRange), copyViewRangeIcon);
        }

        private void CreateMepPanel()
        {
            var panel = GetOrCreatePanel("MEP");
            var matchElevationIcon = LoadIcon("Match Elevation.png");
            var filterProIcon = LoadIcon("FilterPro.png");

            CreatePushButton(panel, "Match\nElevation", "Match the middle elevation from a source MEP element to others.", typeof(CmdMatchElevation), matchElevationIcon);
            var filterProButton = CreatePushButton(panel, "Filter\nPro", "Create parameter filters quickly (category, parameter, values) and apply them to the active view.", typeof(CmdFilterPro), filterProIcon);
            filterProButton.AvailabilityClassName = typeof(CmdFilterProAvailability).FullName;
        }

        private void CreateAnnotationsPanel()
        {
            var panel = GetOrCreatePanel("Annotations");
            var resetTextIcon = LoadIcon("Rest Position.png");
            CreatePushButton(panel, "Reset\nText", "Reset selected text notes/tags back to their default text offset.", typeof(CmdResetTextPosition), resetTextIcon);
        }

        private void CreateInfoPanel()
        {
            var panel = GetOrCreatePanel("Info");
            var aboutIcon = LoadIcon("information.png");
            CreatePushButton(panel, "About", "About this AJ Tools add-in.", typeof(CmdAbout), aboutIcon);
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
        /// Adds a push button to a ribbon panel with the provided command and icon.
        /// </summary>
        private PushButton CreatePushButton(RibbonPanel panel, string text, string tooltip, Type command, BitmapImage icon)
        {
            var pushButton = panel.AddItem(CreatePushButtonData(text, command)) as PushButton;
            ConfigurePushButton(pushButton, tooltip, icon);
            return pushButton;
        }

        /// <summary>
        /// Adds a push button to a pull-down menu with the provided command and icon.
        /// </summary>
        private PushButton CreatePushButton(PulldownButton pulldown, string text, string tooltip, Type command, BitmapImage icon)
        {
            var pushButton = pulldown.AddPushButton(CreatePushButtonData(text, command));
            ConfigurePushButton(pushButton, tooltip, icon);
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
        /// Applies tooltip and icon to a button if available.
        /// </summary>
        private static void ConfigurePushButton(PushButton pushButton, string tooltip, BitmapImage icon)
        {
            if (pushButton == null)
            {
                return;
            }

            pushButton.ToolTip = tooltip;
            if (icon != null)
            {
                pushButton.LargeImage = icon;
                pushButton.Image = icon;
            }
        }

        /// <summary>
        /// Adds a pull-down button to a panel with the supplied icon and tooltip.
        /// </summary>
        private PulldownButton CreatePulldownButton(RibbonPanel panel, string text, string tooltip, BitmapImage icon)
        {
            var pulldownData = new PulldownButtonData($"pulldown_{text.Replace("\n", "")}", text);
            var pulldownButton = panel.AddItem(pulldownData) as PulldownButton;
            if (pulldownButton != null)
            {
                pulldownButton.ToolTip = tooltip;
                if (icon != null)
                {
                    pulldownButton.LargeImage = icon;
                    pulldownButton.Image = icon;
                }
            }

            return pulldownButton;
        }

        private BitmapImage LoadIcon(string fileName)
        {
            var path = Path.Combine(_assemblyFolder, "Images", fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 32;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
    }
}
