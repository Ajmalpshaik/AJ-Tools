#region Metadata
/*
 * Tool Name     : Purge Unplaced Legends
 * File Name     : CmdPurgeUnplacedLegends.cs
 * Purpose       : Entry command that previews legends not placed on any sheet and deletes the ones the
 *                 user selects. Delegates to the shared UnplacedViewPurgeCommandRunner.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Purge (UnplacedViewPurgeCommandRunner)
 *
 * Input         : Full Project - unplaced legends; the user selects which to delete in the window.
 * Output        : Selected legends deleted in one transaction; final report from the window.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Project-only; the runner guards the Family Editor.
 * - Deletion is user-confirmed by the explicit selection + Delete action in the preview window.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
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
    public class CmdPurgeUnplacedLegends : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return UnplacedViewPurgeCommandRunner.Execute(
                commandData,
                ref message,
                UnplacedViewPurgeMode.Legends);
        }
    }
}
