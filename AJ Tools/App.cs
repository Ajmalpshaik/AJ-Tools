using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    public class App : IExternalApplication
    {
        private const string TAB_NAME = "AJ Tools";
        private const string PANEL_GRAPHICS = "Graphics";
        private const string PANEL_DIMENSIONS = "Dimensions";
        private const string PANEL_DATUMS = "Datums";
        private const string PANEL_INFO = "Info";   // always last

        public Result OnStartup(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(TAB_NAME); }
            catch { }

            RibbonPanel panelGraphics = GetOrCreatePanel(app, PANEL_GRAPHICS);
            if (panelGraphics == null)
                return Result.Failed;

            RibbonPanel panelDimensions = GetOrCreatePanel(app, PANEL_DIMENSIONS);
            if (panelDimensions == null)
                return Result.Failed;

            RibbonPanel panelDatums = GetOrCreatePanel(app, PANEL_DATUMS);
            if (panelDatums == null)
                return Result.Failed;

            RibbonPanel panelInfo = GetOrCreatePanel(app, PANEL_INFO);
            if (panelInfo == null)
                return Result.Failed;

            string asmPath = Assembly.GetExecutingAssembly().Location;
            string assemblyFolder = Path.GetDirectoryName(asmPath);

            BitmapImage toggleIcon = LoadIcon(assemblyFolder, "ToggleLinks.png");
            BitmapImage unhideIcon = LoadIcon(assemblyFolder, "UnhideAll.png");
            BitmapImage resetIcon = LoadIcon(assemblyFolder, "ResetOverrides.png");
            BitmapImage aboutIcon = LoadIcon(assemblyFolder, "information.png");
            BitmapImage dimensionsIcon = LoadIcon(assemblyFolder, "Dimensions.png");
            BitmapImage datumIcon = LoadIcon(assemblyFolder, "Resetto3DExtents.png");
            BitmapImage dimByLineIcon = LoadIcon(assemblyFolder, "Dimensions by Line.png");

            // Graphics panel - Toggle Links button
            PushButtonData pbdToggleLinks = new PushButtonData(
                "CmdToggleRevitLinks",
                "Toggle\nLinks",
                asmPath,
                "AJTools.CmdToggleRevitLinks"
            );

            PushButton btnToggle = panelGraphics.AddItem(pbdToggleLinks) as PushButton;

            if (btnToggle != null)
            {
                btnToggle.ToolTip = "Toggle visibility of all Revit Links in the active view.";
                if (toggleIcon != null)
                {
                    btnToggle.LargeImage = toggleIcon;
                    btnToggle.Image = toggleIcon;
                }
            }

            // Graphics panel - Unhide All button
            PushButtonData pbdUnhideAll = new PushButtonData(
                "CmdUnhideAll",
                "Unhide\nAll",
                asmPath,
                "AJTools.CmdUnhideAll"
            );

            PushButton btnUnhide = panelGraphics.AddItem(pbdUnhideAll) as PushButton;

            if (btnUnhide != null)
            {
                btnUnhide.ToolTip = "Unhide all elements in the active view (Temporary Hide/Isolate + hidden items).";
                if (unhideIcon != null)
                {
                    btnUnhide.LargeImage = unhideIcon;
                    btnUnhide.Image = unhideIcon;
                }
            }

            // Graphics panel - Reset Overrides button
            PushButtonData pbdResetOverrides = new PushButtonData(
                "CmdResetOverrides",
                "Reset\nGraphics",
                asmPath,
                "AJTools.CmdResetOverrides"
            );

            PushButton btnReset = panelGraphics.AddItem(pbdResetOverrides) as PushButton;

            if (btnReset != null)
            {
                btnReset.ToolTip = "Clear per-element graphic overrides in the active view.";
                if (resetIcon != null)
                {
                    btnReset.LargeImage = resetIcon;
                    btnReset.Image = resetIcon;
                }
            }

            // Dimensions panel - Auto Dimensions pulldown
            PulldownButtonData pbdAutoDims = new PulldownButtonData(
                "CmdAutoDimensionsPulldown",
                "Auto\nDims");

            PulldownButton btnAutoDims = panelDimensions.AddItem(pbdAutoDims) as PulldownButton;

            if (btnAutoDims != null)
            {
                btnAutoDims.ToolTip = "Dimension grids and levels automatically.";
                if (dimensionsIcon != null)
                {
                    btnAutoDims.LargeImage = dimensionsIcon;
                    btnAutoDims.Image = dimensionsIcon;
                }

                PushButton btnDimsGrids = btnAutoDims.AddPushButton(new PushButtonData(
                    "CmdAutoDimensionsGrids",
                    "Grids Only",
                    asmPath,
                    "AJTools.CmdAutoDimensionsGrids")) as PushButton;

                if (btnDimsGrids != null)
                {
                    btnDimsGrids.ToolTip = "Create horizontal/vertical grid dimension strings in plan views.";
                    if (dimensionsIcon != null)
                    {
                        btnDimsGrids.LargeImage = dimensionsIcon;
                        btnDimsGrids.Image = dimensionsIcon;
                    }
                }

                PushButton btnDimsLevels = btnAutoDims.AddPushButton(new PushButtonData(
                    "CmdAutoDimensionsLevels",
                    "Levels Only",
                    asmPath,
                    "AJTools.CmdAutoDimensionsLevels")) as PushButton;

                if (btnDimsLevels != null)
                {
                    btnDimsLevels.ToolTip = "Create level dimension strings in section or elevation views.";
                    if (dimensionsIcon != null)
                    {
                        btnDimsLevels.LargeImage = dimensionsIcon;
                        btnDimsLevels.Image = dimensionsIcon;
                    }
                }

                PushButton btnDimsCombined = btnAutoDims.AddPushButton(new PushButtonData(
                    "CmdAutoDimensionsCombined",
                    "Grids + Levels",
                    asmPath,
                    "AJTools.CmdAutoDimensions")) as PushButton;

                if (btnDimsCombined != null)
                {
                    btnDimsCombined.ToolTip = "Plan views: dimension grids. Sections/Elevations: dimension levels and grids.";
                    if (dimensionsIcon != null)
                    {
                        btnDimsCombined.LargeImage = dimensionsIcon;
                        btnDimsCombined.Image = dimensionsIcon;
                    }
                }
            }

            // Dimensions panel - Dimension by Line pulldown
            PulldownButtonData pbdDimsByLine = new PulldownButtonData(
                "CmdDimensionByLinePulldown",
                "Dims by\nLine");

            PulldownButton btnDimsByLine = panelDimensions.AddItem(pbdDimsByLine) as PulldownButton;

            if (btnDimsByLine != null)
            {
                btnDimsByLine.ToolTip = "Pick two points to place grid or level dimensions along a custom line.";
                if (dimByLineIcon != null)
                {
                    btnDimsByLine.LargeImage = dimByLineIcon;
                    btnDimsByLine.Image = dimByLineIcon;
                }

                PushButton btnGridsByLine = btnDimsByLine.AddPushButton(new PushButtonData(
                    "CmdDimensionGridsByLine",
                    "Grids by Line",
                    asmPath,
                    "AJTools.CmdDimensionGridsByLine")) as PushButton;

                if (btnGridsByLine != null && dimByLineIcon != null)
                {
                    btnGridsByLine.ToolTip = "Create a dimension string across intersecting grids using a picked line.";
                    btnGridsByLine.LargeImage = dimByLineIcon;
                    btnGridsByLine.Image = dimByLineIcon;
                }

                PushButton btnLevelsByLine = btnDimsByLine.AddPushButton(new PushButtonData(
                    "CmdDimensionLevelsByLine",
                    "Levels by Line",
                    asmPath,
                    "AJTools.CmdDimensionLevelsByLine")) as PushButton;

                if (btnLevelsByLine != null && dimByLineIcon != null)
                {
                    btnLevelsByLine.ToolTip = "Create a dimension string across levels within the picked vertical range.";
                    btnLevelsByLine.LargeImage = dimByLineIcon;
                    btnLevelsByLine.Image = dimByLineIcon;
                }
            }

            // Datums panel - Reset Datums pulldown
            PulldownButtonData pbdResetDatums = new PulldownButtonData(
                "CmdResetDatumsPulldown",
                "Reset to\n3D Extents");

            PulldownButton btnResetDatums = panelDatums.AddItem(pbdResetDatums) as PulldownButton;

            if (btnResetDatums != null)
            {
                btnResetDatums.ToolTip = "Reset grid or level datum extents back to 3D.";
                if (datumIcon != null)
                {
                    btnResetDatums.LargeImage = datumIcon;
                    btnResetDatums.Image = datumIcon;
                }

                PushButton btnResetGrids = btnResetDatums.AddPushButton(new PushButtonData(
                    "CmdResetDatumsGrids",
                    "Grids Only",
                    asmPath,
                    "AJTools.CmdResetDatumsGrids")) as PushButton;

                if (btnResetGrids != null && datumIcon != null)
                {
                    btnResetGrids.ToolTip = "Reset all visible grids to 3D extents in this view.";
                    btnResetGrids.LargeImage = datumIcon;
                    btnResetGrids.Image = datumIcon;
                }

                PushButton btnResetLevels = btnResetDatums.AddPushButton(new PushButtonData(
                    "CmdResetDatumsLevels",
                    "Levels Only",
                    asmPath,
                    "AJTools.CmdResetDatumsLevels")) as PushButton;

                if (btnResetLevels != null && datumIcon != null)
                {
                    btnResetLevels.ToolTip = "Reset all visible levels to 3D extents in this view.";
                    btnResetLevels.LargeImage = datumIcon;
                    btnResetLevels.Image = datumIcon;
                }

                PushButton btnResetBoth = btnResetDatums.AddPushButton(new PushButtonData(
                    "CmdResetDatumsCombined",
                    "Grids + Levels",
                    asmPath,
                    "AJTools.CmdResetDatums")) as PushButton;

                if (btnResetBoth != null && datumIcon != null)
                {
                    btnResetBoth.ToolTip = "Reset both grids and levels visible in this view.";
                    btnResetBoth.LargeImage = datumIcon;
                    btnResetBoth.Image = datumIcon;
                }
            }

            // Info panel - About button
            PushButtonData pbdAbout = new PushButtonData(
                "CmdAbout",
                "About",
                asmPath,
                "AJTools.CmdAbout"
            );

            PushButton btnAbout = panelInfo.AddItem(pbdAbout) as PushButton;

            if (btnAbout != null)
            {
                btnAbout.ToolTip = "About this AJ Tools add-in.";
                if (aboutIcon != null)
                {
                    btnAbout.LargeImage = aboutIcon;
                    btnAbout.Image = aboutIcon;
                }
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            return Result.Succeeded;
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string panelName)
        {
            RibbonPanel panel = null;
            try
            {
                panel = app.CreateRibbonPanel(TAB_NAME, panelName);
            }
            catch
            {
                foreach (RibbonPanel existing in app.GetRibbonPanels(TAB_NAME))
                {
                    if (existing.Name == panelName)
                    {
                        panel = existing;
                        break;
                    }
                }
            }

            return panel;
        }

        private static BitmapImage LoadIcon(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
                return null;

            string[] possiblePaths = new[]
            {
                Path.Combine(folder, fileName),
                Path.Combine(folder, "Images", fileName)
            };

            foreach (string path in possiblePaths)
            {
                if (!File.Exists(path))
                    continue;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new System.Uri(path, System.UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 32;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }

            return null;
        }
    }
}
