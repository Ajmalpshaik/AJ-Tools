#region Metadata
/*
 * Tool Name     : Copy Text Notes
 * File Name     : CmdCopyText.cs
 * Purpose       : Copies the text value from one picked source text note to one or more picked target
 *                 text notes, until Esc.
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
 * Input         : Active View - one source text note, then target text notes picked one-by-one (Esc to finish).
 * Output        : Target text notes set to the source text (single undo step); silent success.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - The whole pick session is wrapped in one TransactionGroup and assimilated, so a single Ctrl+Z
 *   reverses every pasted note.
 * - Esc during a pick is a normal cancel (handled silently); normal success is silent.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-14) - Source/target text-copy pick loop.
 * v1.1.0 (2026-07-01) - Refactor/audit: full metadata block; whole pick session now assimilated into a
 *                       single undo step. Copy behaviour unchanged.
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

                // Group the whole pick session so a single Ctrl+Z reverses every pasted note.
                using (TransactionGroup group = new TransactionGroup(doc, "AJ Tools - Copy Text Notes"))
                {
                    group.Start();

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

                    if (pastedCount > 0)
                        group.Assimilate();
                    else
                        group.RollBack();
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
