#region Metadata
/*
 * Tool Name     : Create 3D Views by Workset
 * File Name     : Cmd3DViewsAsPerWorkset.cs
 * Purpose       : Entry command that creates one isometric 3D view per user workset and isolates each
 *                 workset in its matching view. Delegates the work to Workset3DViewService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-03-24
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.WorksetViews (Workset3DViewService)
 *
 * Input         : Full Project - all user worksets of a workshared model.
 * Output        : One 3D view per workset (created inside one transaction); final report of created / skipped / failed.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Project-only, workshared-only; the service validates the document and reports cleanly when not applicable.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-03-24) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. View-creation behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.WorksetViews;

namespace AJTools.Commands
{
    /// <summary>
    /// Creates 3D views named after user worksets and isolates each workset in its own view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class Cmd3DViewsAsPerWorkset : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return Workset3DViewService.Execute(commandData, ref message);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
