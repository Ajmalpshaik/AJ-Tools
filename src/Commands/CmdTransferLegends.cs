#region Metadata
/*
 * Tool Name     : Transfer Legends
 * File Name     : CmdTransferLegends.cs
 * Purpose       : Entry command that copies selected legend views from one open project to another, with
 *                 optional override + sheet-placement restore. Delegates to TransferViewsCommandRunner.
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
 * Dependencies  : Autodesk Revit API, AJTools.Commands (TransferViewsCommandRunner)
 *
 * Input         : Two or more open projects - source, target, legends, and override flag chosen in the window.
 * Output        : Selected legends copied into the target; final report from the runner.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
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
using AJTools.Models.Transfer;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdTransferLegends : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return TransferViewsCommandRunner.Execute(commandData, ref message, TransferViewKind.Legend);
        }
    }
}
