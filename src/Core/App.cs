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
