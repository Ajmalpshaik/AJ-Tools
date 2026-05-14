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
            try
            {
                var ribbonManager = new RibbonManager(app);
                ribbonManager.CreateRibbon();

                var annotationRibbonManager = new AnnotationRibbonManager(app);
                annotationRibbonManager.CreateRibbon();

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                try
                {
                    string errLog = Path.Combine(Path.GetTempPath(), "AJTools_OnStartup_Error.txt");
                    File.AppendAllText(errLog, "\nERROR during OnStartup:\n" + ex.ToString() + System.Environment.NewLine);
                    try { TaskDialog.Show("AJ Tools - Startup Error", "AJ Tools failed to start. See log: " + errLog); } catch { }
                }
                catch
                {
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
