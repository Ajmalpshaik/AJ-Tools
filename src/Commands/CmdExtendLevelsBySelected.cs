// Tool Name: Extend Levels By Selected
// Description: Matches visible level extents to a selected source level in the active view.
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
    /// Extends picked target levels in the active view to match a selected source level.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdExtendLevelsBySelected : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return LevelExtentsService.Execute(commandData, "Match Level Extents");
        }
    }
}
