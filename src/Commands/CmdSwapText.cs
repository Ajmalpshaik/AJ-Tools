#region Metadata
/*
 * Tool Name     : Swap Text Notes
 * File Name     : CmdSwapText.cs
 * Purpose       : Swaps the text values between two picked text notes in one action.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-14
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (TextNoteSelectionFilter, DialogHelper)
 *
 * Input         : Active View - two picked text notes.
 * Output        : The two notes' text values swapped in one transaction (single undo step); silent success.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Esc during a pick is a normal cancel (handled silently); picking the same note twice is rejected.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-14) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Swap behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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

                using (Transaction t = new Transaction(doc, "AJ Tools - Swap Text Notes"))
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
