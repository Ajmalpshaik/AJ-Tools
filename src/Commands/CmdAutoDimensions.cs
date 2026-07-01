#region Metadata
/*
 * Tool Name     : Automatic Dimensions (Grids / Levels)
 * File Name     : CmdAutoDimensions.cs
 * Purpose       : Ribbon entry commands for the three auto-dimension modes - combined grids+levels,
 *                 grids only, and levels only. Each delegates to AutoDimensionService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.AutoDimension (AutoDimensionService)
 *
 * Input         : Active View - grids (plan) and/or levels (section/elevation).
 * Output        : Dimension strings created by the service in one transaction; report from the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Validation, transaction, and reporting live in the service.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Dimension behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.AutoDimension;

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
            try
            {
                return AutoDimensionService.Execute(
                    commandData,
                    AutoDimensionMode.Combined,
                    "Auto Dimension Grids & Levels");
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
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
            try
            {
                return AutoDimensionService.Execute(
                    commandData,
                    AutoDimensionMode.GridsOnly,
                    "Auto Dimension Grids");
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
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
            try
            {
                return AutoDimensionService.Execute(
                    commandData,
                    AutoDimensionMode.LevelsOnly,
                    "Auto Dimension Levels");
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
