#region Metadata
/*
 * Tool Name     : Toggle Link
 * File Name     : CmdToggleRevitLinks.cs
 * Purpose       : Toggles Revit Link category visibility in the active view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View — no selection required.
 * Output        : Revit Links category visibility toggled in the active view.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Verify the newest Revit version's required .NET target before building.
 * - Production-ready implementation.
 * - Safe transaction handling.
 * - Silent on success — no summary needed for a category visibility toggle.
 * - View templates that lock Revit Links visibility will block the toggle; user is informed.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.1.0 (2026-05-06) - Added safe validation, standardized metadata, and cleaned command flow.
 * v1.2.0 (2026-06-29) - Added Regeneration attribute; corrected transaction name to "AJ-Tools: Toggle Link";
 *                        corrected dialog title to "AJ-Tools"; replaced ex.Message in user-facing
 *                        error dialogs with plain-language guidance.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdToggleRevitLinks : IExternalCommand
    {
        private const string ToolTitle = "AJ Tools";
        private const string TransactionName = "AJ Tools - Toggle Link";
        private static readonly ElementId RevitLinksCategoryId = new ElementId(BuiltInCategory.OST_RvtLinks);

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

                View view = doc.ActiveView;
                if (!CanToggleRevitLinks(view, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                bool hideLinks = !view.GetCategoryHidden(RevitLinksCategoryId);
                using (Transaction transaction = new Transaction(doc, TransactionName))
                {
                    transaction.Start();
                    view.SetCategoryHidden(RevitLinksCategoryId, hideLinks);
                    transaction.Commit();
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(
                    ToolTitle,
                    "Could not toggle Revit Link visibility in the active view.\n\n"
                    + "If a view template controls Revit Links visibility, unlock that setting and try again.");
                return Result.Failed;
            }
        }

        private static bool CanToggleRevitLinks(View view, out string reason)
        {
            reason = string.Empty;

            if (view == null || !view.IsValidObject)
            {
                reason = "No valid active view found.";
                return false;
            }

            try
            {
                if (!view.CanCategoryBeHidden(RevitLinksCategoryId))
                {
                    reason = "Revit Links category visibility cannot be changed in the active view.";
                    return false;
                }
            }
            catch
            {
                reason = "Revit Links category visibility check failed. Please try in a different view.";
                return false;
            }

            return true;
        }
    }
}
