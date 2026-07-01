#region Metadata
/*
 * Tool Name     : Reset Grid / Level Extents to 3D
 * File Name     : CmdResetDatums.cs
 * Purpose       : Ribbon entry points (grids only / levels only / both) that reset visible datum
 *                 extents in the active view back to 3D, delegating to ResetDatumService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-07
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.ResetDatums
 *
 * Input         : Active View - visible grids and/or levels.
 * Output        : Datum extents reset to 3D (Model); single undo step.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Three commands share one service via ResetDatumMode (GridsOnly / LevelsOnly / Combined).
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-07) - Initial release.
 * v1.1.0 (2026-06-30) - Added mandatory metadata block; aligned with refactored ResetDatumService.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.ResetDatums;
using AJTools.Models;

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
            try
            {
                return ResetDatumService.Execute(
                    commandData,
                    ResetDatumMode.Combined,
                    "Reset Grids & Levels");
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
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
            try
            {
                return ResetDatumService.Execute(
                    commandData,
                    ResetDatumMode.GridsOnly,
                    "Reset Grids");
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
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
            try
            {
                return ResetDatumService.Execute(
                    commandData,
                    ResetDatumMode.LevelsOnly,
                    "Reset Levels");
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
