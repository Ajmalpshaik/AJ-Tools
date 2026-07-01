#region Metadata
/*
 * Tool Name     : Duct Standard (Duct Standards Manager)
 * File Name     : CmdDuctStandardsManager.cs
 * Purpose       : Opens the Duct Standards Manager window, which calculates and writes duct sheet
 *                 thickness, gauge, weight, and area onto ducts using SMACNA-style rules.
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
 * Dependencies  : Autodesk Revit API, AJTools.UI.DuctStandards (DuctStandardsManagerWindow), AJTools.Utils
 *
 * Input         : Active project - rules and scope chosen in the window.
 * Output        : Calculated duct standard values written to ducts (transaction owned by the window).
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Project-only tool; validates an editable, non-family document before opening the window.
 * - No active project cancels quietly with a plain message instead of reporting a failure.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-11) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: full metadata block; no-document path cancels cleanly; added
 *                       project-document guard. Calculation behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI.DuctStandards;
using AJTools.Utils;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdDuctStandardsManager : IExternalCommand
    {
        private const string Title = "Duct Standard";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                if (!ValidationHelper.ValidateEditableDocument(uidoc.Document, out message))
                {
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                var window = new DuctStandardsManagerWindow(uidoc);
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
