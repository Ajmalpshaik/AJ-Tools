#region Metadata
/*
 * Tool Name     : Stack Tags
 * File Name     : CmdStackTags.cs
 * Purpose       : Select MEP elements, then click one location - a fresh tag is created for every
 *                 eligible element and the whole batch is arranged into a vertical stack starting
 *                 there. Clicking again relocates the whole stack. Delegates to StackTagsService.
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
 * Dependencies  : Autodesk Revit API, AJTools.Services.CreateTags (StackTagsService)
 *
 * Input         : Selection - one or more MEP elements in the active view, plus one click (repeatable
 *                 to relocate) for the whole stack.
 * Output        : Tags created/moved with an L-shaped leader; validation/transaction/report in the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Category enable/disable and minimum length come from Create Tags Settings; stack spacing comes
 *   from Arrange Tags Settings - nothing new to configure for this tool specifically.
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
using AJTools.Services.CreateTags;

namespace AJTools.Commands
{
    /// <summary>
    /// Select MEP elements, then click one location to create and stack a tag for each.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdStackTags : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return StackTagsService.Execute(commandData, ref message);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
