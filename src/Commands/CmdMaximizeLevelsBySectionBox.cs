// Tool Name: Maximize Levels By Section Box
// Description: Maximizes all level 3D extents to the active 3D view's section box.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-12
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.LevelExtents

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.LevelExtents;

namespace AJTools.Commands
{
    /// <summary>
    /// Maximizes all level 3D extents to fit the active 3D view's section box.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMaximizeLevelsBySectionBox : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return LevelExtentsBySectionBoxService.Execute(commandData, "Maximize Levels by Section Box");
        }
    }
}
