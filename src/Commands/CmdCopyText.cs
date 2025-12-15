// Tool Name: Copy Text
// Description: Copies a text note's contents to other text notes.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-14
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Copies the text value from one text note to multiple target text notes.
    /// Select source once, then click targets until ESC.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCopyText : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            try
            {
                Reference sourceRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new TextNoteSelectionFilter(),
                    "Select SOURCE text note to copy from");

                TextNote sourceNote = doc.GetElement(sourceRef) as TextNote;
                if (sourceNote == null)
                    return Result.Cancelled;

                string sourceText = sourceNote.Text ?? string.Empty;
                if (string.IsNullOrEmpty(sourceText))
                {
                    DialogHelper.ShowError("Copy Text", "The selected text note is empty.");
                    return Result.Cancelled;
                }

                const string targetPrompt = "Select TARGET text note (ESC to finish)";
                int pastedCount = 0;

                while (true)
                {
                    Reference targetRef;
                    try
                    {
                        targetRef = uidoc.Selection.PickObject(
                            ObjectType.Element,
                            new TextNoteSelectionFilter(),
                            targetPrompt);
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    TextNote targetNote = doc.GetElement(targetRef) as TextNote;
                    if (targetNote == null)
                        continue;

                    using (Transaction t = new Transaction(doc, "Paste Text Note"))
                    {
                        t.Start();
                        targetNote.Text = sourceText;
                        t.Commit();
                    }

                    pastedCount++;
                }

                if (pastedCount == 0)
                {
                    DialogHelper.ShowError("Copy Text", "Source captured, but no target text notes were selected.");
                    return Result.Cancelled;
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
                return Result.Failed;
            }
        }
    }
}
