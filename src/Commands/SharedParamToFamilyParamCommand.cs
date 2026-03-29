// Tool Name: Shared Parameter to Family Parameter - Command
// Description: Converts selected shared parameters in a family into normal family parameters.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-26
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services;
using AJTools.UI;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SharedParamToFamilyParamCommand : IExternalCommand
    {
        private const string ToolTitle = "Shared Parameter to Family Parameter";
        private const string FamilyEditorOnlyMessage = "This tool can only be used inside a Family Editor document.";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData?.Application?.ActiveUIDocument;

            if (!ValidateContext(uiDoc, out Document doc, out string validationMessage))
            {
                TaskDialog.Show(ToolTitle, validationMessage);
                return Result.Cancelled;
            }

            try
            {
                var service = new SharedParamToFamilyParamService(doc);
                var sharedParams = service.GetSharedParameters();

                if (sharedParams.Count == 0)
                {
                    TaskDialog.Show(ToolTitle, "No shared parameters were found in this family.");
                    return Result.Cancelled;
                }

                var window = new SharedParamToFamilyParamWindow(sharedParams);
                bool? dialogResult = window.ShowDialog();
                if (dialogResult != true)
                {
                    return Result.Cancelled;
                }

                var selectedItems = window.SelectedItems;
                if (selectedItems.Count == 0)
                {
                    TaskDialog.Show(ToolTitle, "No parameters were selected for conversion.");
                    return Result.Cancelled;
                }

                SharedParamToFamilyParamResult conversionResult = service.Convert(selectedItems);
                TaskDialog.Show(ToolTitle, conversionResult.BuildSummary());

                if (conversionResult.SuccessCount > 0)
                {
                    return Result.Succeeded;
                }

                return Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show(ToolTitle, $"An unexpected error occurred:\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        private static bool ValidateContext(UIDocument uiDoc, out Document doc, out string validationMessage)
        {
            doc = null;
            validationMessage = string.Empty;

            if (uiDoc == null || uiDoc.Document == null)
            {
                validationMessage = "Open a family document before running this tool.";
                return false;
            }

            doc = uiDoc.Document;
            if (!doc.IsFamilyDocument)
            {
                validationMessage = FamilyEditorOnlyMessage;
                return false;
            }

            if (doc.IsReadOnly)
            {
                validationMessage = "The current family document is read-only. Open an editable family and try again.";
                return false;
            }

            return true;
        }
    }
}
