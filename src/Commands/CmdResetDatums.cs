// Tool Name: Reset Datums
// Description: Resets grid/level extents back to 3D for selected categories.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Resets both grid and level datums back to 3D extents.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetDatums : IExternalCommand
    {
        /// <summary>
        /// Executes the reset datums workflow for grids and levels.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ResetDatumService.Execute(
                commandData,
                ResetDatumMode.Combined,
                "Reset Grids & Levels");
        }
    }

    /// <summary>
    /// Resets grid datums back to 3D extents.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetDatumsGrids : IExternalCommand
    {
        /// <summary>
        /// Executes the reset datums workflow for grids only.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ResetDatumService.Execute(
                commandData,
                ResetDatumMode.GridsOnly,
                "Reset Grids");
        }
    }

    /// <summary>
    /// Resets level datums back to 3D extents.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetDatumsLevels : IExternalCommand
    {
        /// <summary>
        /// Executes the reset datums workflow for levels only.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ResetDatumService.Execute(
                commandData,
                ResetDatumMode.LevelsOnly,
                "Reset Levels");
        }
    }
}
