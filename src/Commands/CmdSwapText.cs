// Tool Name: Swap Text
// Description: Swaps text values between two text notes.
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
    /// Swaps the text values between two picked text notes (one-time action).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSwapText : IExternalCommand
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
                Reference firstRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new TextNoteSelectionFilter(),
                    "Select FIRST text note");

                TextNote firstNote = doc.GetElement(firstRef) as TextNote;
                if (firstNote == null)
                    return Result.Cancelled;

                Reference secondRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new TextNoteSelectionFilter(),
                    "Select SECOND text note to swap with");

                TextNote secondNote = doc.GetElement(secondRef) as TextNote;
                if (secondNote == null)
                    return Result.Cancelled;

                if (firstNote.Id == secondNote.Id)
                {
                    DialogHelper.ShowError("Swap Text", "Pick two different text notes to swap their values.");
                    return Result.Cancelled;
                }

                using (Transaction t = new Transaction(doc, "Swap Text Notes"))
                {
                    t.Start();

                    string firstText = firstNote.Text ?? string.Empty;
                    string secondText = secondNote.Text ?? string.Empty;

                    firstNote.Text = secondText;
                    secondNote.Text = firstText;

                    t.Commit();
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
