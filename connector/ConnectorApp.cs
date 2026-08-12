#region Metadata
/*
 * Tool Name     : AJ Connect (add-in entry point)
 * File Name     : ConnectorApp.cs
 * Purpose       : Revit start-up/shutdown for AJ Connect. Builds the single ribbon button and
 *                 constructs the server, catalogue and script runner on Revit's UI thread.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-08-12
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, ConnectorServer, ToolCatalogue, ScriptRunner
 *
 * Input         : Revit start-up.
 * Output        : One ribbon button on its own "AJ Connect" tab.
 *
 * Notes         :
 * - Named "AJ Connect" rather than anything containing "Connector": in Revit that word already means
 *   duct/pipe connectors, and a ribbon tab reading "Connector" would be misread as MEP by a BIM team.
 *   Ajmal chose this name 2026-08-12.
 * - Own tab, deliberately NOT the "AJ Tools" tab. A colleague installing this may not have AJ Tools
 *   at all, and this add-in must stand completely alone - installing it gives Revit ONLY the AJ
 *   Connect tab, never the AJ Tools or AJ Annotation tabs.
 * - ScriptRunner is constructed here because ExternalEvent.Create must run on Revit's UI thread, and
 *   OnStartup is the one place guaranteed to be on it. Nothing listens on any port until the person
 *   presses Connect.
 * - The AssemblyResolve handler is required, not optional: Roslyn scripting pulls in
 *   System.Collections.Immutable and friends, and Revit's load context does not find them beside
 *   this DLL by itself. AJ Tools hit exactly this and solved it the same way.
 * - Icon loading is wrapped so a missing or unreadable PNG can never stop the add-in loading. A
 *   failure during OnStartup takes the whole add-in down, and a plain text button that works beats a
 *   pretty one that does not.
 *
 * Changelog     :
 * v1.1.0 (2026-08-12) - Renamed to AJ Connect. Ribbon button now opens the AJ Connect window instead
 *                       of toggling directly, and carries an on/off icon pair that swaps with the
 *                       connection state, matching how AJ Tools' own AJ AI button behaves.
 * v1.0.0 (2026-08-12) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace AJToolsConnector
{
    public class ConnectorApp : IExternalApplication
    {
        private const string TabName = "AJ Connect";
        private const string PanelName = "AJ Connect";

        private const string OnIconFile = "AJ_AI_ON.png";
        private const string OffIconFile = "AJ_AI_OFF.png";

        private static string _addinFolder;

        /// <summary>Shared instance so the window starts/stops the one running server.</summary>
        public static ConnectorServer Server { get; private set; }

        /// <summary>Captured at ribbon-build time so the icon can follow the connection state.</summary>
        public static PushButton ConnectButton { get; private set; }

        public Result OnStartup(UIControlledApplication app)
        {
            _addinFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                try { app.CreateRibbonTab(TabName); }
                catch { /* already exists (reload, or a second copy) - reuse it rather than fail */ }

                RibbonPanel panel = app.CreateRibbonPanel(TabName, PanelName);

                var data = new PushButtonData(
                    "AJConnectOpen",
                    "AJ\nConnect",
                    Assembly.GetExecutingAssembly().Location,
                    typeof(CmdOpenConnect).FullName);

                data.ToolTip = "Open AJ Connect - your tools, in the browser.";
                data.LongDescription =
                    "Starts a small server on this computer and opens your browser. The tools shown " +
                    "there are published by Ajmal, checked for his signature, and run on the model " +
                    "you have open.";

                ConnectButton = panel.AddItem(data) as PushButton;
                ApplyIcon(connected: false);

                var runner = new ScriptRunner();
                var catalogue = new ToolCatalogue();
                Server = new ConnectorServer(runner, catalogue);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AJ Connect", "Could not start:\n\n" + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            if (Server != null) Server.Stop();
            Server = null;
            ConnectButton = null;

            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            return Result.Succeeded;
        }

        /// <summary>
        /// Swaps the ribbon icon to match the connection state - a plain PushButton has no built-in
        /// on/off visual, so this is how the ribbon shows whether AJ Connect is live. Called by the
        /// AJ Connect window after every connect or disconnect.
        /// </summary>
        public static void ApplyIcon(bool connected)
        {
            if (ConnectButton == null) return;

            string file = connected ? OnIconFile : OffIconFile;

            var large = LoadIcon(file, 32);
            if (large != null) ConnectButton.LargeImage = large;

            var small = LoadIcon(file, 16);
            if (small != null) ConnectButton.Image = small;
        }

        private static BitmapImage LoadIcon(string fileName, int size)
        {
            try
            {
                string path = Path.Combine(_addinFolder ?? string.Empty, "Resources", fileName);
                if (!File.Exists(path)) return null;

                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.DecodePixelWidth = size;
                image.DecodePixelHeight = size;
                image.CacheOption = BitmapCacheOption.OnLoad;   // release the file handle immediately
                image.EndInit();
                image.Freeze();                                  // usable from any thread, and cheaper
                return image;
            }
            catch
            {
                // A missing or corrupt icon must never stop the add-in loading.
                return null;
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;

            if (name != "System.Collections.Immutable"
                && !name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
                && name != "System.Runtime.CompilerServices.Unsafe"
                && name != "System.Reflection.Metadata"
                && name != "System.Memory")
            {
                return null;
            }

            try
            {
                string path = Path.Combine(_addinFolder ?? string.Empty, name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
