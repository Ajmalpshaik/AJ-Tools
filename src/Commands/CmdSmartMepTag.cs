#region Metadata
/*
 * Tool Name     : Smart MEP Tags
 * File Name     : CmdSmartMepTag.cs
 * Purpose       : Analyses the active view and intelligently tags MEP elements (ducts, pipes, equipment,
 *                 accessories, cable trays) with clash-free placement. Delegates to SmartMepTagService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-04-20
 * Last Updated  : 2026-10-14
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.SmartTag (SmartMepTagService), AJTools.Utils
 *
 * Input         : Active View - MEP elements collected by the service.
 * Output        : Tags placed with clash-free scoring; validation/transaction/report handled by the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Category enable/disable is configured via the Smart MEP Tagging Settings command, as is
 *   "Also tag elements in linked models" (off by default).
 * - Linked elements: the tags are created in this model, on a link reference.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-20) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Tagging behaviour unchanged.
 * v1.2.0 (2026-10-14) - Can tag elements inside linked models shown in the view (settings tick box).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.SmartTag;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Analyses the active Revit view, collects MEP elements, and places tags intelligently
    /// using a scoring engine and clash detection — like an experienced BIM modeller would.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSmartMepTag : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData?.Application?.ActiveUIDocument == null)
                {
                    message = "No active document found.";
                    DialogHelper.ShowError("Smart MEP Tag", message);
                    return Result.Cancelled;
                }

                return SmartMepTagService.Execute(commandData, ref message);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
