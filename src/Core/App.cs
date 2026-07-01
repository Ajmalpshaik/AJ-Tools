#region Metadata
/*
 * Tool Name     : AJ-Tools
 * File Name     : App.cs
 * Purpose       : IExternalApplication entry point — registers the ribbon and all AJ-Tools
 *                 commands on Revit startup; handles assembly resolution for bundled DLLs.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.7.0
 *
 * Created Date  : 2025-01-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, RibbonManager, AnnotationRibbonManager, GeminiShell
 *
 * Input         : UIControlledApplication from Revit on startup
 * Output        : AJ-Tools ribbon created; dockable Gemini Shell pane registered
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Preloads System.Collections.Immutable.dll to prevent Roslyn assembly resolution conflicts.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.5.1 (2026-06-30) - Added mandatory metadata block.
 * v1.5.2 (2026-06-30) - Section Mark Visibility tool cleanup (perf, worksharing safety,
 *                       correct result code, new ribbon icon).
 * v1.6.0 (2026-07-01) - Suite bumped for the Modify / MEP / Coordination / Data / Manage / Family
 *                       panels refactor/audit. Ribbon registration unchanged.
 * v1.7.0 (2026-07-01) - Suite bumped for the AJ Annotation tab refactor/audit (Dimensions, Tags, Flow,
 *                       Revision Clouds, Text) plus About. Ribbon registration unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.UI;
using AJTools.GeminiShell.DockablePane;
using System;
using System.IO;
using System.Reflection;

namespace AJTools.App
{
    public class App : IExternalApplication
    {
        private static string _addinFolder;

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
            catch { }

            try
            {
                var ribbonManager = new RibbonManager(app);
                ribbonManager.CreateRibbon();

                var annotationRibbonManager = new AnnotationRibbonManager(app);
                annotationRibbonManager.CreateRibbon();

                app.RegisterDockablePane(GeminiShellPaneProvider.PaneId, "Gemini Shell", new GeminiShellPaneProvider());

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
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            return Result.Succeeded;
        }
    }
}
