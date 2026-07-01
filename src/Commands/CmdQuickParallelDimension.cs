#region Metadata
/*
 * Tool Name     : Quick Parallel Dimension
 * File Name     : CmdQuickParallelDimension.cs
 * Purpose       : Ribbon entry commands to quickly dimension selected parallel elements - by centerline
 *                 or by both side faces/edges. Each delegates to QuickParallelDimensionService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-03-29
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.QuickDimension (QuickParallelDimensionService)
 *
 * Input         : Selection - parallel elements (e.g. ducts/pipes) in the active view.
 * Output        : A parallel dimension string; validation/transaction/report handled by the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. The plain command keeps centerline mode for backward compatibility.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-03-29) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Dimension behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.QuickDimension;

namespace AJTools.Commands
{
    /// <summary>
    /// Backward-compatible quick parallel command (defaults to center line mode).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdQuickParallelDimension : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return QuickParallelDimensionService.Execute(commandData, QuickDimensionReferenceMode.Centerline);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Creates quick dimensions using center line references.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdQuickParallelCenterLineDimension : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return QuickParallelDimensionService.Execute(commandData, QuickDimensionReferenceMode.Centerline);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Creates quick dimensions using both side faces/edges where available.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdQuickParallelFaceEdgeDimension : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return QuickParallelDimensionService.Execute(commandData, QuickDimensionReferenceMode.FaceEdge);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
