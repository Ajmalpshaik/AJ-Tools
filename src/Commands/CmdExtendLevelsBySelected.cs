#region Metadata
/*
 * Tool Name     : Match Level Extents
 * File Name     : CmdExtendLevelsBySelected.cs
 * Purpose       : Ribbon entry point - pick a source level, then pick target levels one-by-one to
 *                 match their 3D extents; delegates to LevelExtentsService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-12
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.LevelExtents
 *
 * Input         : Selection - one source level, then target levels (Esc to finish).
 * Output        : Target levels' 3D extents matched to the source.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-12) - Initial release.
 * v1.1.0 (2026-06-30) - Added mandatory metadata block; aligned with refactored LevelExtentsService.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
            try
            {
                return LevelExtentsService.Execute(commandData, "Match Level Extents");
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
