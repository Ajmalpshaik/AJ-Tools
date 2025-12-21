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

namespace AJTools.App
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
            var toggleLarge = LoadIconLarge("ToggleLinks.png");
            var toggleSmall = LoadIconSmall("ToggleLinks.png");
            var unhideLarge = LoadIconLarge("UnhideAll.png");
            var unhideSmall = LoadIconSmall("UnhideAll.png");
            var resetLarge = LoadIconLarge("ResetOverrides.png");
            var resetSmall = LoadIconSmall("ResetOverrides.png");

            CreatePushButton(panel, "Toggle\nLinks", "Toggle visibility of all Revit Links in the active view.", typeof(CmdToggleRevitLinks), toggleLarge, toggleSmall);
            CreatePushButton(panel, "Unhide\nAll", "Unhide all elements in the active view (Temporary Hide/Isolate + hidden items).", typeof(CmdUnhideAll), unhideLarge, unhideSmall);
            CreatePushButton(panel, "Reset\nGraphics", "Clear per-element graphic overrides in the active view.", typeof(CmdResetOverrides), resetLarge, resetSmall);
        }

        private void CreateLinksPanel()
        {
            var panel = GetOrCreatePanel("Links");

            var linkedLarge = LoadIconLarge("linkedID.png");
            var linkedSmall = LoadIconSmall("linkedID.png");

            var pulldownButton = CreatePulldownButton(panel, "Element\nID", "Element ID tools for current and linked models.", linkedLarge, linkedSmall);
            CreatePushButton(pulldownButton, "Linked ID of\nSelection", "Pick any element (model or linked) and view its Element ID with source info.", typeof(CmdLinkedElementIdViewer), linkedLarge, linkedSmall);
            CreatePushButton(pulldownButton, "View by\nLinked ID", "Search by Element ID in current or linked models and zoom to it.", typeof(CmdLinkedElementSearch), linkedLarge, linkedSmall);
        }

        private void CreateDimensionsPanel()
        {
            var panel = GetOrCreatePanel("Dimensions");

            var dimensionsLarge = LoadIconLarge("Dimensions.png");
            var dimensionsSmall = LoadIconSmall("Dimensions.png");
            var dimByLineLarge = LoadIconLarge("Dimensions by Line.png");
            var dimByLineSmall = LoadIconSmall("Dimensions by Line.png");
            var copyDimLarge = LoadIconLarge("Copy Dim Text.png");
            var copyDimSmall = LoadIconSmall("Copy Dim Text.png");

            var autoDimsPulldown = CreatePulldownButton(panel, "Auto\nDims", "Dimension grids and levels automatically.", dimensionsLarge, dimensionsSmall);
            CreatePushButton(autoDimsPulldown, "Grids Only", "Create horizontal/vertical grid dimension strings in plan views.", typeof(CmdAutoDimensionsGrids), dimensionsLarge, dimensionsSmall);
            CreatePushButton(autoDimsPulldown, "Levels Only", "Create level dimension strings in section or elevation views.", typeof(CmdAutoDimensionsLevels), dimensionsLarge, dimensionsSmall);
            CreatePushButton(autoDimsPulldown, "Grids + Levels", "Plan views: dimension grids. Sections/Elevations: dimension levels and grids.", typeof(CmdAutoDimensions), dimensionsLarge, dimensionsSmall);

            var dimsByLinePulldown = CreatePulldownButton(panel, "Dim By\nLine", "Pick two points to place grid or level dimensions along a custom line.", dimByLineLarge, dimByLineSmall);
            CreatePushButton(dimsByLinePulldown, "Dim By Line\nGrid Only", "Create a dimension string across intersecting grids using a picked line (plan, section, or elevation).", typeof(CmdDimensionGridsByLine), dimByLineLarge, dimByLineSmall);
            CreatePushButton(dimsByLinePulldown, "Dim By Line\nLevel Only", "Create a dimension string across levels within the picked vertical range.", typeof(CmdDimensionLevelsByLine), dimByLineLarge, dimByLineSmall);

            CreatePushButton(panel, "Copy Dim\nText", "Copy Above/Below/Prefix/Suffix text from one dimension to others.", typeof(CmdCopyDimensionText), copyDimLarge, copyDimSmall);
        }

        private void CreateDatumsPanel()
        {
            var panel = GetOrCreatePanel("Datums");

            var datumLarge = LoadIconLarge("Resetto3DExtents.png");
            var datumSmall = LoadIconSmall("Resetto3DExtents.png");
            var flipLarge = LoadIconLarge("Grid bubble Flip.png");
            var flipSmall = LoadIconSmall("Grid bubble Flip.png");

            var resetDatumsPulldown = CreatePulldownButton(panel, "Reset to\n3D Extents", "Reset grid or level datum extents back to 3D.", datumLarge, datumSmall);
            CreatePushButton(resetDatumsPulldown, "Grids Only", "Reset all visible grids to 3D extents in this view.", typeof(CmdResetDatumsGrids), datumLarge, datumSmall);
            CreatePushButton(resetDatumsPulldown, "Levels Only", "Reset all visible levels to 3D extents in this view.", typeof(CmdResetDatumsLevels), datumLarge, datumSmall);
            CreatePushButton(resetDatumsPulldown, "Grids + Levels", "Reset both grids and levels visible in this view.", typeof(CmdResetDatums), datumLarge, datumSmall);

            CreatePushButton(panel, "Flip Grid\nBubble", "Toggle which grid end shows the bubble, one grid at a time.", typeof(CmdFlipGridBubble), flipLarge, flipSmall);
        }

        private void CreateViewsPanel()
        {
            var panel = GetOrCreatePanel("Views");
            var copyViewLarge = LoadIconLarge("Copy View Range.png");
            var copyViewSmall = LoadIconSmall("Copy View Range.png");
            CreatePushButton(panel, "Copy View\nRange", "Copy the active plan view's range and paste it to other plan views.", typeof(CmdCopyViewRange), copyViewLarge, copyViewSmall);
        }

        private void CreateMepPanel()
        {
            var panel = GetOrCreatePanel("MEP");

            var matchLarge = LoadIconLarge("Match Elevation.png");
            var matchSmall = LoadIconSmall("Match Elevation.png");
            var filterLarge = LoadIconLarge("FilterPro.png");
            var filterSmall = LoadIconSmall("FilterPro.png");

            CreatePushButton(panel, "Match\nElevation", "Match the middle elevation from a source MEP element to others.", typeof(CmdMatchElevation), matchLarge, matchSmall);
            var filterProButton = CreatePushButton(panel, "Filter\nPro", "Create parameter filters quickly (category, parameter, values) and apply them to the active view.", typeof(CmdFilterPro), filterLarge, filterSmall);
            filterProButton.AvailabilityClassName = typeof(CmdFilterProAvailability).FullName;
        }

        private void CreateAnnotationsPanel()
        {
            var panel = GetOrCreatePanel("Annotations");

            var leaderLarge = LoadIconLarge("apply.png");
            var leaderSmall = LoadIconSmall("apply.png");
            var resetLarge = LoadIconLarge("Rest Position.png");
            var resetSmall = LoadIconSmall("Rest Position.png");
            var copySwapLarge = LoadIconLarge("copyswaptext.png");
            var copySwapSmall = LoadIconSmall("copyswaptext.png");
            var copyLarge = LoadIconLarge("copy.png");
            var copySmall = LoadIconSmall("copy.png");

            CreatePushButton(panel, "L-Shape\nLeader", "Force tags to use a right-angle leader. Run again on the same tag to flip the elbow side. Preselect tags or pick tags (Tab cycles) until Esc.", typeof(CmdForceTagLeaderLShape), leaderLarge, leaderSmall);
            CreatePushButton(panel, "Reset\nText", "Reset selected text notes/tags back to their default text offset.", typeof(CmdResetTextPosition), resetLarge, resetSmall);

            var copySwapPulldown = CreatePulldownButton(panel, "Copy Swap\nText", "Copy or swap text values between text notes.", copySwapLarge, copySwapSmall);
            CreatePushButton(copySwapPulldown, "Copy Text", "Copy the text value from one text note to others (click targets until ESC).", typeof(CmdCopyText), copyLarge, copySmall);
            CreatePushButton(copySwapPulldown, "Swap Text", "Swap the text values between two picked text notes (one-time).", typeof(CmdSwapText), copySwapLarge, copySwapSmall);
        }

        private void CreateInfoPanel()
        {
            var panel = GetOrCreatePanel("Info");
            var aboutLarge = LoadIconLarge("information.png");
            var aboutSmall = LoadIconSmall("information.png");
            CreatePushButton(panel, "About", "About this AJ Tools add-in.", typeof(CmdAbout), aboutLarge, aboutSmall);
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
        /// Adds a push button to a ribbon panel with the provided command and icons.
        /// </summary>
        private PushButton CreatePushButton(RibbonPanel panel, string text, string tooltip, Type command, BitmapImage largeIcon, BitmapImage smallIcon)
        {
            var pushButton = panel.AddItem(CreatePushButtonData(text, command)) as PushButton;
            ConfigurePushButton(pushButton, tooltip, largeIcon, smallIcon);
            return pushButton;
        }

        /// <summary>
        /// Adds a push button to a pull-down menu with the provided command and icons.
        /// </summary>
        private PushButton CreatePushButton(PulldownButton pulldown, string text, string tooltip, Type command, BitmapImage largeIcon, BitmapImage smallIcon)
        {
            var pushButton = pulldown.AddPushButton(CreatePushButtonData(text, command));
            ConfigurePushButton(pushButton, tooltip, largeIcon, smallIcon);
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
        private static void ConfigurePushButton(PushButton pushButton, string tooltip, BitmapImage largeIcon, BitmapImage smallIcon)
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
        /// Adds a pull-down button to a panel with the supplied tooltip and icons.
        /// </summary>
        private PulldownButton CreatePulldownButton(RibbonPanel panel, string text, string tooltip, BitmapImage largeIcon, BitmapImage smallIcon)
        {
            var pulldownData = new PulldownButtonData($"pulldown_{text.Replace("\n", "")}", text);
            var pulldownButton = panel.AddItem(pulldownData) as PulldownButton;
            if (pulldownButton != null)
            {
                pulldownButton.ToolTip = tooltip;
                if (largeIcon != null)
                {
                    pulldownButton.LargeImage = largeIcon;
                }
                if (smallIcon != null)
                {
                    pulldownButton.Image = smallIcon;
                }
            }

            return pulldownButton;
        }

        private BitmapImage LoadIconLarge(string fileName)
        {
            return LoadIcon(fileName, 32);
        }

        private BitmapImage LoadIconSmall(string fileName)
        {
            return LoadIcon(fileName, 16);
        }

        /// <summary>
        /// Loads a PNG from the Resources folder and decodes it to the requested size.
        /// Revit expects 32x32 for LargeImage and 16x16 for Image; decoding here keeps
        /// the ribbon crisp and avoids oversized menu icons.
        /// </summary>
        private BitmapImage LoadIcon(string fileName, int decodePixels)
        {
            var path = Path.Combine(_assemblyFolder, "Resources", fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = decodePixels;
            bmp.DecodePixelHeight = decodePixels;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
    }
}
