#region Metadata
/*
 * Tool Name     : Rearrange Tags
 * File Name     : CmdIntelligentTagArranger.cs
 * Purpose       : Rearranges the selected tags into a clean vertical stack using nearest-first T1-to-L1
 *                 logic. Delegates to IntelligentTagArrangerService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-07
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.TagArrange (IntelligentTagArrangerService)
 *
 * Input         : Selection - IndependentTags in the active view.
 * Output        : Tags repositioned into a vertical stack; validation/transaction/report in the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Vertical spacing is set in Arrange Tag Settings.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-07) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Arrange behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.TagArrange;

namespace AJTools.Commands
{
    /// <summary>
    /// Rearranges pre-selected IndependentTags into a clean vertical stack.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdIntelligentTagArranger : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return IntelligentTagArrangerService.Execute(commandData, ref message);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
