// Tool Name: AJ Tools Application Entry
// Description: Registers the AJ Tools ribbon and handles add-in startup/shutdown.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI, System.IO
using Autodesk.Revit.UI;
using System.IO;

namespace AJTools
{
    /// <summary>
    /// Entry point for the AJ Tools Revit add-in.
    /// Wires up the custom ribbon on startup.
    /// </summary>
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                var ribbonManager = new RibbonManager(app);
                ribbonManager.CreateRibbon();
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                try
                {
                    string log = Path.Combine(Path.GetTempPath(), "AJTools_OnStartup_Error.txt");
                    File.WriteAllText(log, ex.ToString());
                    TaskDialog.Show(
                        "AJ Tools - Startup Error",
                        "An error occurred during startup. See log:\n" + log);
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
