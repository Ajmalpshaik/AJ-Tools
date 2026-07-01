#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : CmdFilterPro.cs
 * Purpose       : Entry point command — validates context and launches the Filter Pro window.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
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
 * Input         : Active View (must be a filter-capable view type)
 * Output        : Opens Filter Pro UI; returns Succeeded if the user created or applied filters
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Verify the newest Revit version's required .NET target before building.
 * - Production-ready implementation.
 * - Safe transaction handling.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdFilterPro : IExternalCommand
    {
        /// <summary>
        /// Opens the Filter Pro window after validating document context.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            if (!ValidateContext(uiDoc, out Document doc, out View activeView, out message))
            {
                TaskDialog.Show("Filter Pro", message);
                return Result.Cancelled;
            }

            try
            {
                if (!WarnForLargeDocument(doc))
                    return Result.Cancelled;

                var window = new FilterProWindow(doc, activeView);
                window.ShowDialog();

                return window.HasChanges ? Result.Succeeded : Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"An error occurred: {ex.Message}";
                TaskDialog.Show("Filter Pro Error", $"An unexpected error occurred:\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        private static bool ValidateContext(
            UIDocument uiDoc,
            out Document doc,
            out View activeView,
            out string validationMessage)
        {
            doc = null;
            activeView = null;
            validationMessage = string.Empty;

            if (uiDoc == null || uiDoc.Document == null)
            {
                validationMessage = "Open a project document before running this command.";
                return false;
            }

            doc = uiDoc.Document;

            if (doc.IsReadOnly)
            {
                validationMessage = "The current document is read-only. Please open an editable document.";
                return false;
            }

            if (doc.IsFamilyDocument)
            {
                validationMessage = "Filter Pro cannot be used in family documents. Please open a project document.";
                return false;
            }

            activeView = uiDoc.ActiveView;

            if (activeView == null)
            {
                validationMessage = "No active view found. Please open a view before running this command.";
                return false;
            }

            if (!CmdFilterProAvailability.CanViewHaveFilters(activeView, out string viewReason))
            {
                validationMessage =
                    $"The current view ({activeView.ViewType}) does not support filters.\n\n" +
                    $"{viewReason}\n\n" +
                    "Please switch to a view that supports visibility/graphics filters " +
                    "(e.g. plan, section, elevation, 3D, detail).";
                return false;
            }

            return true;
        }

        private static bool WarnForLargeDocument(Document doc)
        {
            int elementCount = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .GetElementCount();

            if (elementCount > 100_000)
            {
                TaskDialogResult result = TaskDialog.Show(
                    "Filter Pro - Large Document",
                    $"This document contains {elementCount:N0} elements.\n" +
                    "Filter operations may take some time.\n\n" +
                    "Do you want to continue?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                return result == TaskDialogResult.Yes;
            }

            return true;
        }
    }
}
