#region Metadata
/*
 * Tool Name     : Auto Duct Dimension (single / all duct to wall)
 * File Name     : DuctReferenceDimensionCommand.cs
 * Purpose       : Ribbon entry commands for chained perpendicular reference dimensions from ducts to nearby
 *                 ducts, walls, columns, and beams - one picked duct, or all eligible ducts in the view.
 *                 Each delegates to DuctReferenceDimensionService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-05-10
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.DuctReferenceDimension (DuctReferenceDimensionService)
 *
 * Input         : Active View - one picked duct, or all eligible ducts in the active plan view.
 * Output        : Perpendicular reference dimension strings; validation/transaction/report in the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. The active-view variant skips vertical, short (<1000 mm),
 *   and already-dimensioned ducts (handled in the service).
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-10) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Dimension behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.DuctReferenceDimension;

namespace AJTools.Commands.Annotation
{
    [Transaction(TransactionMode.Manual)]
    public class DuctReferenceDimensionCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return DuctReferenceDimensionService.Execute(commandData);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class DuctReferenceDimensionActiveViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return DuctReferenceDimensionService.ExecuteActiveView(commandData);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
