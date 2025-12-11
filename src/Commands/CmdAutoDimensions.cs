// Tool Name: Auto Dimensions Commands
// Description: Launchers for combined, grid-only, and level-only auto-dimension workflows.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services;

namespace AJTools.Commands
{
    /// <summary>
    /// Launches combined grid and level auto-dimensioning.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdAutoDimensions : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return AutoDimensionService.Execute(
                commandData,
                AutoDimensionMode.Combined,
                "Auto Dimension Grids & Levels");
        }
    }

    /// <summary>
    /// Launches grid-only auto-dimensioning.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdAutoDimensionsGrids : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return AutoDimensionService.Execute(
                commandData,
                AutoDimensionMode.GridsOnly,
                "Auto Dimension Grids");
        }
    }

    /// <summary>
    /// Launches level-only auto-dimensioning.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdAutoDimensionsLevels : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return AutoDimensionService.Execute(
                commandData,
                AutoDimensionMode.LevelsOnly,
                "Auto Dimension Levels");
        }
    }
}
