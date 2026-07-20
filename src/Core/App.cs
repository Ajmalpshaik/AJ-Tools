#region Metadata
/*
 * Tool Name     : AJ-Tools
 * File Name     : App.cs
 * Purpose       : IExternalApplication entry point — registers the ribbon and all AJ-Tools
 *                 commands on Revit startup; handles assembly resolution for bundled DLLs.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.13.0
 *
 * Created Date  : 2025-01-01
 * Last Updated  : 2026-07-20
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, RibbonManager, AnnotationRibbonManager, AiShell
 *
 * Input         : UIControlledApplication from Revit on startup
 * Output        : AJ-Tools ribbon created; dockable "C#" pane registered
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Preloads System.Collections.Immutable.dll to prevent Roslyn assembly resolution conflicts.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.13.0 (2026-07-20) - Suite bumped: added the Smart Selection tool (Modify panel, AJ Tools tab).
 * v1.12.1 (2026-07-18) - Dockable pane title shortened to just "C#" (was "C# with AI"), matching the
 *                       ribbon button label Ajmal shortened the same day.
 * v1.12.0 (2026-07-18) - Registers the dockable pane as "C# with AI" now (was "AJ AI" - that name
 *                       moved to the ribbon bridge button). Added a static AiBridgeButton PushButton
 *                       reference (set by RibbonManager when the AJ AI button is built, cleared on
 *                       shutdown) so ToggleAiBridgeCommand can swap that button's own icon between
 *                       connected/disconnected art after each click.
 * v1.11.0 (2026-07-18) - Exposed the running AiShellPaneProvider's McpBridgeService as a static
 *                       AiBridge property (set on startup, cleared on shutdown) so the new standalone
 *                       "AJ AI Bridge" ribbon button (ToggleAiBridgeCommand) can connect/disconnect
 *                       the same bridge instance without going through the AJ AI chat panel.
 * v1.5.1 (2026-06-30) - Added mandatory metadata block.
 * v1.5.2 (2026-06-30) - Section Mark Visibility tool cleanup (perf, worksharing safety,
 *                       correct result code, new ribbon icon).
 * v1.6.0 (2026-07-01) - Suite bumped for the Modify / MEP / Coordination / Data / Manage / Family
 *                       panels refactor/audit. Ribbon registration unchanged.
 * v1.7.0 (2026-07-01) - Suite bumped for the AJ Annotation tab refactor/audit (Dimensions, Tags, Flow,
 *                       Revision Clouds, Text) plus About. Ribbon registration unchanged.
 * v1.9.0 (2026-07-05) - Added the Arrange Text in Box tool on a new "Text" panel (AJ Annotation tab).
 * v1.10.0 (2026-07-07) - Added the AutoDebugger MCP bridge: a Connect/Disconnect toggle in the AJ AI
 *                        pane that starts a local named-pipe server (McpBridgeService) for an external
 *                        MCP process to run C# against the live document through RevitExecutionService.
 *                        Pane provider instance now retained so OnShutdown can stop the bridge cleanly.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.UI;
using AJTools.AiShell.DockablePane;
using AJTools.AiShell.Services;
using System;
using System.IO;
using System.Reflection;

namespace AJTools.App
{
    public class App : IExternalApplication
    {
        private static string _addinFolder;
        private AiShellPaneProvider _aiShellPaneProvider;

        /// <summary>Shared AJ AI Bridge instance, set once at startup, so the standalone ribbon
        /// button (ToggleAiBridgeCommand) can connect/disconnect the same running bridge the C# with
        /// AI pane owns instead of creating a second one.</summary>
        public static McpBridgeService AiBridge { get; private set; }

        /// <summary>The "AJ AI" ribbon PushButton itself, captured at ribbon-build time so
        /// ToggleAiBridgeCommand can swap its icon between AJ_AI_ON.png / AJ_AI_OFF.png after each
        /// connect/disconnect - a plain PushButton has no built-in on/off visual state.</summary>
        public static PushButton AiBridgeButton { get; set; }

        public Result OnStartup(UIControlledApplication app)
        {
            _addinFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            try
            {
                // Force preload the correct DLL before Roslyn can even ask for it
                string immutablePath = Path.Combine(_addinFolder, "System.Collections.Immutable.dll");
                if (File.Exists(immutablePath))
                {
                    Assembly.LoadFrom(immutablePath);
                }
            }
            catch (Exception ex)
            {
                // Not fatal - CurrentDomain_AssemblyResolve below is a fallback for this same DLL -
                // but log it, since silently eating this is exactly the failure mode this preload
                // exists to prevent (a Roslyn assembly resolution conflict).
                string errLog = Path.Combine(Path.GetTempPath(), "AJTools_AssemblyResolve_Error.txt");
                try { File.AppendAllText(errLog, $"\nFailed to preload System.Collections.Immutable.dll:\n{ex}\n"); } catch { }
            }

            try
            {
                var ribbonManager = new RibbonManager(app);
                ribbonManager.CreateRibbon();

                var annotationRibbonManager = new AnnotationRibbonManager(app);
                annotationRibbonManager.CreateRibbon();

                _aiShellPaneProvider = new AiShellPaneProvider();
                AiBridge = _aiShellPaneProvider.Bridge;
                app.RegisterDockablePane(AiShellPaneProvider.PaneId, "C#", _aiShellPaneProvider);

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("AJ Tools Startup Error", "ERROR during OnStartup:\n\n" + ex.ToString());
                return Result.Failed;
            }
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            
            if (assemblyName == "System.Collections.Immutable" || 
                assemblyName.StartsWith("Microsoft.CodeAnalysis") ||
                assemblyName == "System.Runtime.CompilerServices.Unsafe" ||
                assemblyName == "System.Memory")
            {
                string assemblyPath = Path.Combine(_addinFolder, assemblyName + ".dll");

                if (File.Exists(assemblyPath))
                {
                    try
                    {
                        return Assembly.LoadFrom(assemblyPath);
                    }
                    catch (Exception ex)
                    {
                        string errLog = Path.Combine(Path.GetTempPath(), "AJTools_AssemblyResolve_Error.txt");
                        File.AppendAllText(errLog, $"\nFailed to load {assemblyName}:\n{ex.ToString()}\n");
                    }
                }
            }

            return null;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            _aiShellPaneProvider?.Shutdown();
            AiBridge = null;
            AiBridgeButton = null;
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            return Result.Succeeded;
        }
    }
}
