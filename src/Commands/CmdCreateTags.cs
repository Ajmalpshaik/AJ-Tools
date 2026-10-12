#region Metadata
/*
 * Tool Name     : Create Tags
 * File Name     : CmdCreateTags.cs
 * Purpose       : Tags MEP elements automatically at the distance set in Create Tags Settings -
 *                 below a horizontal run, to the right of a vertical one. Works on the elements
 *                 already selected, or with nothing selected tags each element as it is clicked
 *                 (Esc to finish). Skips elements already tagged in the view, shorter than the
 *                 configured minimum length, or a vertical run (when that setting is on).
 *                 Delegates to CreateTagsService.
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
 * Dependencies  : Autodesk Revit API, AJTools.Services.CreateTags (CreateTagsService)
 *
 * Input         : Selection - one or more MEP elements in the active view, or, with nothing selected,
 *                 a click on each element to tag.
 * Output        : Tags placed with an L-shaped leader; validation/transaction/report handled by the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Category enable/disable and minimum length are configured via the Create Tags Settings command.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.1 (2026-10-12) - Header and class summary corrected: they still described clicking a location
 *                       for each tag, but placement has been automatic since 2026-08-17. No behaviour
 *                       change.
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.CreateTags;

namespace AJTools.Commands
{
    /// <summary>
    /// Tags MEP elements automatically at the set distance from each one - the current selection,
    /// or, with nothing selected, each element as it is clicked.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCreateTags : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return CreateTagsService.Execute(commandData, ref message);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
