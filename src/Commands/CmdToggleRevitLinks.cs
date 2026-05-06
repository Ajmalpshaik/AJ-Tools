// ==================================================
// Tool Name    : Toggle Link
// Purpose      : Toggles Revit Link category visibility in the active view.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2025-12-10
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active editable Revit project view.
// Output       : Revit Links category visibility toggled in the active view.
// Notes        : Normal success is silent; view templates or unsupported views may block category visibility changes.
// Changelog    : v1.1.0 - Added safe validation, standardized metadata, and cleaned command flow.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Toggles link category visibility in the active view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdToggleRevitLinks : IExternalCommand
    {
        private const string Title = "Toggle Link";
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
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                {
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                View view = doc.ActiveView;
                if (!CanToggleRevitLinks(view, out message))
                {
                    DialogHelper.ShowError(Title, message);
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
                    Title,
                    "Could not toggle Revit Link visibility in the active view.\n\n"
                    + ex.Message
                    + "\n\nIf a view template controls Revit Links visibility, unlock that setting and try again.");
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
            catch (Exception ex)
            {
                reason = $"Could not validate Revit Links category visibility support: {ex.Message}";
                return false;
            }

            return true;
        }
    }
}
