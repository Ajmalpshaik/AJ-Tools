using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdFilterPro : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            // Enhanced validation
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show("Filter Pro", "Open a project document before running this command.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;

            // Validate document is not read-only
            if (doc.IsReadOnly)
            {
                TaskDialog.Show("Filter Pro", "The current document is read-only. Please open an editable document.");
                return Result.Cancelled;
            }

            // Validate document is not a family document
            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("Filter Pro", "Filter Pro cannot be used in family documents. Please open a project document.");
                return Result.Cancelled;
            }

            View activeView = uiDoc.ActiveView;

            // Validate active view
            if (activeView == null)
            {
                TaskDialog.Show("Filter Pro", "No active view found. Please open a view before running this command.");
                return Result.Cancelled;
            }

            // Check if view supports filters
            if (!CmdFilterProAvailability.CanViewHaveFilters(activeView, out string viewReason))
            {
                TaskDialog.Show("Filter Pro",
                    $"The current view ({activeView.ViewType}) does not support filters.\n\n" +
                    $"{viewReason}\n\n" +
                    "Please switch to a view that supports visibility/graphics filters (e.g. plan, section, elevation, 3D, detail).");
                return Result.Cancelled;
            }

            try
            {
                // Performance warning for large documents
                int elementCount = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                if (elementCount > 100000)
                {
                    TaskDialogResult result = TaskDialog.Show("Filter Pro - Large Document",
                        $"This document contains {elementCount:N0} elements.\n" +
                        "Filter operations may take some time.\n\n" +
                        "Do you want to continue?",
                        TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                    if (result != TaskDialogResult.Yes)
                        return Result.Cancelled;
                }

                // Open the Filter Pro window
                var window = new FilterProWindow(doc, activeView);
                bool? dialogResult = window.ShowDialog();

                // If any changes were committed, keep the command succeeded so Revit keeps them.
                if (window != null && window.HasChanges)
                    return Result.Succeeded;

                // No changes -> allow Revit to treat as cancelled
                return Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled operation
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                // Log error and show user-friendly message
                message = $"An error occurred: {ex.Message}";
                TaskDialog.Show("Filter Pro Error",
                    $"An unexpected error occurred:\n\n{ex.Message}\n\n" +
                    $"Please try again or contact support if the issue persists.");
                return Result.Failed;
            }
        }

    }
}
