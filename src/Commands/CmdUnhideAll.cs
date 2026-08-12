#region Metadata
/*
 * Tool Name     : Unhide All
 * File Name     : CmdUnhideAll.cs
 * Purpose       : Unhides permanently hidden elements and clears Temporary Hide/Isolate in the active view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.3.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, UnhideAllService
 *
 * Input         : Active View — no selection required.
 * Output        : Hidden elements restored in the active view; Temporary Hide/Isolate cleared where active.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Verify the newest Revit version's required .NET target before building.
 * - Production-ready implementation.
 * - Safe transaction handling.
 * - Collector uses full-model scan to safely capture permanently hidden elements (view-scoped
 *   collector may exclude hidden elements — needs Revit verification before switching).
 *
 * Changelog     :
 * v1.3.0 (2026-08-12) - Model work moved out to Services/UnhideAll/UnhideAllService.cs so the Web
 *                       Panel can run the identical code from a browser. This command keeps owning
 *                       validation and the TaskDialog report; behaviour from the ribbon is unchanged
 *                       (same collector, transaction name, Temporary Hide/Isolate handling and
 *                       wording). The dialog text now comes from UnhideAllResult.Summary, so the two
 *                       front doors can never drift apart.
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.1.0 (2026-05-06) - API-safe hidden element restore and standardized metadata.
 * v1.2.0 (2026-06-28) - Added Regeneration attribute; corrected transaction name to "AJ-Tools: Unhide All";
 *                        corrected dialog title to "AJ-Tools"; added IsFamilyDocument guard via
 *                        ValidateEditableDocument; added summary report; renamed private methods.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.UnhideAll;
using AJTools.Utils;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdUnhideAll : IExternalCommand
    {
        private const string ToolTitle = "AJ-Tools";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocumentAndView(uidoc, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                // The model work lives in UnhideAllService so the Web Panel runs the identical code.
                UnhideAllResult result = UnhideAllService.Run(doc, doc.ActiveView);

                TaskDialog.Show(ToolTitle, result.Summary);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(ToolTitle, "Could not unhide elements in the active view.\n\nPlease check that the view is editable and try again.");
                return Result.Failed;
            }
        }

    }
}
