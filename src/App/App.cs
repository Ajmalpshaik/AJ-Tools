// Tool Name: AJ Tools Application Entry
// Description: Registers the AJ Tools ribbon and handles add-in startup/shutdown.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI, System.IO
using Autodesk.Revit.UI;
using System.IO;

namespace AJTools.App
{
    /// <summary>
    /// Entry point for the AJ Tools Revit add-in.
    /// Wires up the custom ribbon on startup.
    /// </summary>
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            // Enhanced startup logging to diagnose load failures in Revit.
            string logPath = null;
            try
            {
                logPath = Path.Combine(Path.GetTempPath(), "AJTools_OnStartup_Info.txt");
                using (var sw = File.CreateText(logPath))
                {
                    sw.WriteLine("AJ Tools OnStartup - {0}", System.DateTime.Now);
                    sw.WriteLine("CurrentDirectory: {0}", Directory.GetCurrentDirectory());
                    sw.WriteLine("AssemblyLocation: {0}", Assembly.GetExecutingAssembly().Location);
                    try
                    {
                        sw.WriteLine("AssemblyFolder: {0}", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                    }
                    catch { }
                    sw.Flush();
                }

                var ribbonManager = new RibbonManager(app);
                ribbonManager.CreateRibbon();

                // Log success
                try
                {
                    File.AppendAllText(logPath, "\nRibbon creation succeeded." + System.Environment.NewLine);
                }
                catch { }

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                // Write full exception details to temp log so user can inspect outside of Revit.
                try
                {
                    string errLog = logPath ?? Path.Combine(Path.GetTempPath(), "AJTools_OnStartup_Error.txt");
                    File.AppendAllText(errLog, "\nERROR during OnStartup:\n" + ex.ToString() + System.Environment.NewLine);
                    // Show a simple dialog inside Revit to notify the user (best-effort)
                    try { TaskDialog.Show("AJ Tools - Startup Error", "AJ Tools failed to start. See log: " + errLog); } catch { }
                }
                catch
                {
                    // Swallow any secondary logging errors silently.
                }

                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            return Result.Succeeded;
        }
    }
}
