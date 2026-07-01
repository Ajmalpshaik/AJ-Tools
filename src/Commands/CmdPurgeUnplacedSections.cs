#region Metadata
/*
 * Tool Name     : Purge Unplaced Sections
 * File Name     : CmdPurgeUnplacedSections.cs
 * Purpose       : Entry command that previews unplaced section views (not on any sheet) and deletes the
 *                 ones the user selects. Delegates to the shared UnplacedViewPurgeCommandRunner.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-05-11
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Purge (UnplacedViewPurgeCommandRunner)
 *
 * Input         : Full Project - unplaced section views; the user selects which to delete in the window.
 * Output        : Selected sections deleted in one transaction; final report from the window.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Project-only; the runner guards the Family Editor.
 * - Deletion is user-confirmed by the explicit selection + Delete action in the preview window.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-11) - Converted from interactive Python shell script.
 * v1.1.0 (2026-07-01) - Refactor/audit: standardized metadata block. Purge behaviour unchanged.
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
    public class CmdPurgeUnplacedSections : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return UnplacedViewPurgeCommandRunner.Execute(
                commandData,
                ref message,
                UnplacedViewPurgeMode.SectionViews);
        }
    }
}
