#region Metadata
/*
 * Tool Name     : Unhide All
 * File Name     : CmdUnhideAll.cs
 * Purpose       : Unhides permanently hidden elements and clears Temporary Hide/Isolate in the active view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
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
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdUnhideAll : IExternalCommand
    {
        private const string ToolTitle = "AJ-Tools";
        private const string TransactionName = "AJ-Tools: Unhide All";

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
                ICollection<ElementId> hiddenIds = CollectHiddenElementIds(doc, view);

                bool thiCleared = false;

                using (Transaction trans = new Transaction(doc, TransactionName))
                {
                    trans.Start();

                    if (hiddenIds.Count > 0)
                        view.UnhideElements(hiddenIds);

                    thiCleared = TryDisableTemporaryHideIsolate(view);

                    trans.Commit();
                }

                ShowSummaryReport(hiddenIds.Count, thiCleared);
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

        private static ICollection<ElementId> CollectHiddenElementIds(Document doc, View view)
        {
            var hiddenIds = new List<ElementId>();

            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                if (element == null || !element.IsValidObject)
                    continue;

                if (IsElementHidden(element, view))
                    hiddenIds.Add(element.Id);
            }

            return hiddenIds;
        }

        private static bool IsElementHidden(Element element, View view)
        {
            try { return element.IsHidden(view); }
            catch { return false; }
        }

        private static bool TryDisableTemporaryHideIsolate(View view)
        {
            try
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ShowSummaryReport(int unhiddenCount, bool thiCleared)
        {
            string elementsLine = unhiddenCount > 0
                ? $"{unhiddenCount} hidden element(s) restored."
                : "No permanently hidden elements found.";

            string thiLine = thiCleared
                ? "Temporary Hide/Isolate cleared."
                : "Temporary Hide/Isolate was not active.";

            TaskDialog.Show(ToolTitle, $"{elementsLine}\n{thiLine}");
        }
    }
}
