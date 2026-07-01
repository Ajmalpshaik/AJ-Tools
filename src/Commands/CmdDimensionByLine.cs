#region Metadata
/*
 * Tool Name     : Dimension Grids / Levels by Picked Line
 * File Name     : CmdDimensionByLine.cs
 * Purpose       : Ribbon entry commands to dimension across grids or across levels along a line the user
 *                 picks. Each delegates to DimensionByLineService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-11
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.DimensionByLine (DimensionByLineService)
 *
 * Input         : Active View - a picked line; grids (plan/section/elevation) or levels (section/elevation).
 * Output        : A dimension string across the crossed datums; validation/transaction/report in the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Pick/ESC handling and reporting live in the service.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-11) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Dimension behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
            try
            {
                return DimensionByLineService.DimensionLevels(commandData);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
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
            try
            {
                return DimensionByLineService.DimensionGrids(commandData);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
