// Tool Name: Dimension By Line Commands
// Description: Command entry points for dimensioning grids or levels along a picked line.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.DimensionByLine;

namespace AJTools.Commands
{
    /// <summary>
    /// Dimensions levels along a user-picked line in section/elevation views.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdDimensionLevelsByLine : IExternalCommand
    {
        /// <summary>
        /// Entry point for level-by-line dimensioning.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return DimensionByLineService.DimensionLevels(commandData);
        }
    }

    /// <summary>
    /// Dimensions grids along a user-picked line (plan, section, or elevation).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdDimensionGridsByLine : IExternalCommand
    {
        /// <summary>
        /// Entry point for grid-by-line dimensioning.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return DimensionByLineService.DimensionGrids(commandData);
        }
    }
}
