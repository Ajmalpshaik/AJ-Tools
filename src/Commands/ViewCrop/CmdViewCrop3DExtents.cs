#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : CmdViewCrop3DExtents.cs
 * Purpose       : External commands for View Crop (active-view / all-model) and integrated annotation crop.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-06-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Tool Scope: Active View OR a user-selected batch of plan views (Floor / Ceiling / Engineering / Area Plan).
 * Output        : Updated view crop and/or annotation crop on each supported target view, plus batch report.
 *
 * Notes         :
 * - Two ribbon commands are registered:
 *     CmdViewCropByAllModelElements        - unified crop command; user picks mode (visible/all) inside the settings dialog.
 *     CmdSetAnnotationCropByViewCrop       - set annotation crop with equal offsets.
 * - Only plan-family views are supported. Sections, elevations, 3D, sheets, schedules, legends are skipped.
 * - Linked-model elements are read-only (used for extents, never modified).
 * - Bulk-edit confirmation is shown when more than one view is targeted (skill safety gate).
 *
 * Changelog     :
 * v1.2.0 (2026-06-28) - Merged two commands into one button: CmdViewCropByActiveViewElements removed; mode is now chosen inside the settings dialog.
 * v1.1.0 (2026-06-27) - Refactor/audit pass: bulk-edit confirmation, shared report presenter, ElementIdHelper, metadata, version coverage notes. Behaviour unchanged.
 * v1.0.1 (2026-05-06) - Standardized metadata after production cleanup.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.ViewCrop;

namespace AJTools.Commands
{
    /// <summary>
    /// Unified crop command. User picks "Visible elements" or "All model elements" inside the settings dialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdViewCropByAllModelElements : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return ViewCropCommandService.Execute(
                    commandData,
                    "Crop View by Elements",
                    ref message);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Enables annotation crop and sets annotation offsets from the view crop boundary.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSetAnnotationCropByViewCrop : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return ViewCropAnnotationCommandService.Execute(
                    commandData,
                    "Set Annotation Crop by View Crop",
                    ref message);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
