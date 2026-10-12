#region Metadata
/*
 * Tool Name     : Purge Unplaced Schedules
 * File Name     : CmdPurgeUnplacedSchedules.cs
 * Purpose       : Entry command that previews schedules not placed on any sheet and deletes the ones the
 *                 user selects. Delegates to the shared UnplacedViewPurgeCommandRunner.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Purge (UnplacedViewPurgeMode),
 *                 AJTools.Commands (UnplacedViewPurgeCommandRunner)
 *
 * Input         : Full Project - unplaced schedules; the user selects which to delete in the window.
 * Output        : Selected schedules deleted in one transaction; the window reports only what was skipped or failed.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Project-only; the runner guards the Family Editor.
 * - Deletion is user-confirmed by the explicit selection + Delete action in the preview window.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
 * v1.0.1 (2026-10-12) - Header corrected: the runner lives in AJTools.Commands, not AJTools.Models.Purge;
 *                       Output now says the window reports only skips/failures. No code change here.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.Purge;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPurgeUnplacedSchedules : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return UnplacedViewPurgeCommandRunner.Execute(
                commandData,
                ref message,
                UnplacedViewPurgeMode.Schedules);
        }
    }
}
