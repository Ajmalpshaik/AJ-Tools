#region Metadata
/*
 * Tool Name     : Maximize Level Extents to Section Box
 * File Name     : CmdMaximizeLevelsBySectionBox.cs
 * Purpose       : Ribbon entry point that maximizes all level 3D extents to the active 3D view's
 *                 section box; delegates to LevelExtentsBySectionBoxService.
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
 * Input         : Full Project - all levels; bounds from the active 3D view's section box.
 * Output        : Level 3D extents maximized to the section box; single undo step.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-12) - Initial release.
 * v1.1.0 (2026-06-30) - Added mandatory metadata block; aligned with refactored service.
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
    /// Maximizes all level 3D extents to fit the active 3D view's section box.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMaximizeLevelsBySectionBox : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return LevelExtentsBySectionBoxService.Execute(commandData, "Maximize Levels by Section Box");
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
