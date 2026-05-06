// Tool Name: Unhide All
// Description: Unhides all elements in the active view (temporary hide and hidden items).
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2026-05-06
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Unhides all elements in the active view (temporary hide and hidden items).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdUnhideAll : IExternalCommand
    {
        private const string Title = "Unhide All";
        private const string TransactionName = "AJ Tools - Unhide All";

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
                if (doc.IsReadOnly)
                {
                    message = "The document is read-only. Please open an editable document.";
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                View view = doc.ActiveView;

                ICollection<ElementId> hiddenElementIds = CollectPermanentlyHiddenElementIds(doc, view);

                using (Transaction transaction = new Transaction(doc, TransactionName))
                {
                    transaction.Start();

                    if (hiddenElementIds.Count > 0)
                        view.UnhideElements(hiddenElementIds);

                    TryDisableTemporaryHideIsolate(view);

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
                DialogHelper.ShowError(Title, $"Could not unhide elements in the active view.\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        private static ICollection<ElementId> CollectPermanentlyHiddenElementIds(Document doc, View view)
        {
            var hiddenElementIds = new List<ElementId>();

            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                if (element == null || !element.IsValidObject)
                    continue;

                if (IsPermanentlyHidden(element, view))
                    hiddenElementIds.Add(element.Id);
            }

            return hiddenElementIds;
        }

        private static bool IsPermanentlyHidden(Element element, View view)
        {
            try
            {
                return element.IsHidden(view);
            }
            catch
            {
                return false;
            }
        }

        private static void TryDisableTemporaryHideIsolate(View view)
        {
            try
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }
            catch
            {
                // Some view types or states may reject temporary mode changes.
            }
        }
    }
}
