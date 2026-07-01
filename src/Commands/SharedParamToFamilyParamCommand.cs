#region Metadata
/*
 * Tool Name     : Shared to Family (Shared Parameter to Family Parameter)
 * File Name     : SharedParamToFamilyParamCommand.cs
 * Purpose       : Converts selected shared parameters in the active family into normal (non-shared) family
 *                 parameters, keeping their values, via the Shared-to-Family window and service.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-03-26
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services (SharedParamToFamilyParamService), AJTools.UI
 *
 * Input         : Active Family document - shared parameters the user selects in the window.
 * Output        : Selected shared parameters converted to family parameters; conversion summary reported.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Family-Editor-only tool; validates an editable family document before running.
 * - Esc / cancel is handled silently; the conversion summary is the tool's final report.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-03-26) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Conversion behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
