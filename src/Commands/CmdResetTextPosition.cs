// Tool Name: Reset Text Position
// Description: Resets selected text notes/tags back to default positions.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Resets selected text notes/tags back to default positions.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetTextPosition : IExternalCommand
    {
        /// <summary>
        /// Executes the reset text position workflow.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            if (uidoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TaskDialog.Show("Reset Text", "Select text notes or tags, then run this command.");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            int resetCount = 0;

            try
            {
                using (Transaction t = new Transaction(doc, "Reset Text Position"))
                {
                    t.Start();

                    foreach (ElementId id in selectedIds)
                    {
                        Element el = doc.GetElement(id);
                        if (el != null && ResetTextPositionForElement(el))
                        {
                            resetCount++;
                        }
                    }

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            if (resetCount == 0)
            {
                TaskDialog.Show("Reset Text", "No text notes/tags were reset.");
                return Result.Cancelled;
            }

            TaskDialog.Show("Reset Text", $"Reset {resetCount} text note/tag(s).");
            return Result.Succeeded;
        }

        private static bool ResetTextPositionForElement(Element el)
        {
            // Attempt to find common text/leader related parameters by name for TextNote/Tag types
            Parameter leaderEnd = el.LookupParameter("Leader Elbow") ?? el.LookupParameter("Leader End");
            Parameter textOffset = el.LookupParameter("Text Position") ?? el.LookupParameter("Text Offset");

            bool reset = false;

            if (leaderEnd != null && !leaderEnd.IsReadOnly && leaderEnd.StorageType == StorageType.Double)
            {
                leaderEnd.Set(0.0);
                reset = true;
            }

            if (textOffset != null && !textOffset.IsReadOnly && textOffset.StorageType == StorageType.Double)
            {
                textOffset.Set(0.0);
                reset = true;
            }
            return reset;
        }
    }
}
