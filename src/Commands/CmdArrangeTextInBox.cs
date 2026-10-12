#region Metadata
/*
 * Tool Name     : Arrange Text in Box
 * File Name     : CmdArrangeTextInBox.cs
 * Purpose       : Fits selected text notes into a rectangle the user drags. Each note's width is set to
 *                 the box width and the notes are spread evenly top-to-bottom with left edges aligned.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.1
 *
 * Created Date  : 2026-07-05
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (TextNoteSelectionFilter, DialogHelper),
 *                 AJTools.Services.ArrangeTextInBox
 *
 * Input         : Selection - preselected text notes, or text notes picked one-by-one. Then the user
 *                 picks the TOP-LEFT corner once and BOTTOM-RIGHT corner repeatedly (Esc to finish).
 * Output        : Selected text notes resized to the box width and evenly distributed inside the box
 *                 (single undo step). Silent success.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Works in the active view's own right/up frame (View.RightDirection / UpDirection), so it stays
 *   correct in rotated plan views and in section / elevation / drafting / sheet views.
 * - On a sheet the scale is treated as 1 (text notes are already at paper size).
 * - The whole pick session is wrapped in one TransactionGroup and assimilated, so a single Ctrl+Z
 *   reverses every resize and move.
 * - A section, elevation or detail view with no work plane gets one on its own view plane before the
 *   first pick (PickPoint throws without one); it is part of the same group, so a session that
 *   arranges nothing leaves the view's work plane as it was.
 * - Esc during a pick is a normal cancel (handled silently); normal success is silent.
 * - Gaps and spacing scale with the view scale, matching paper-size intent (mm converted to feet).
 * - Thin command wrapper: selection, the pick-loop, and the TransactionGroup live here; the
 *   text-box-fit algorithm itself lives in Services/ArrangeTextInBox/ArrangeTextInBoxService.cs.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.1.1 (2026-10-12) - Section, elevation and detail views without a work plane now get one before
 *                       the first pick (inside the session's TransactionGroup, rolled back if nothing
 *                       is arranged). Before this, PickPoint threw "no work plane" and the tool failed
 *                       in those views although it claimed to support them.
 * v1.1.0 (2026-07-17) - Extracted the text-box-fit algorithm (rectangle math, bounding-box
 *                       projection, width clamping, vertical distribution) into
 *                       Services/ArrangeTextInBox/ArrangeTextInBoxService.cs (code review cleanup
 *                       pass) - no behavior change.
 * v1.0.0 (2026-07-05) - Initial release. Ported from an earlier AJ Tools script.
 *                       Runs in plan, section, elevation, drafting, legend, and sheet views.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Services.ArrangeTextInBox;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Fits selected text notes into a rectangle the user drags: each note is resized to the box
    /// width and the notes are spread evenly top-to-bottom, left edges aligned. Pick the top-left
    /// corner once, then pick bottom-right corners to re-fit live; Esc finishes.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdArrangeTextInBox : IExternalCommand
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

            View view = doc.ActiveView;
            if (!IsPickableView(view))
            {
                DialogHelper.ShowError(
                    "Arrange Text in Box",
                    "Open a plan, section, elevation, drafting, or sheet view with text notes and try again.");
                return Result.Cancelled;
            }

            try
            {
                List<TextNote> notes = GetTextNotes(uidoc, doc);
                if (notes.Count == 0)
                {
                    DialogHelper.ShowError("Arrange Text in Box", "No text notes were selected.");
                    return Result.Cancelled;
                }

                XYZ rightDir = view.RightDirection.Normalize();
                XYZ upDir = view.UpDirection.Normalize();

                // Order the notes top-to-bottom, then left-to-right, in the view's own frame.
                notes = notes
                    .OrderByDescending(n => ArrangeTextInBoxService.DotValue(n.Coord, upDir))
                    .ThenBy(n => ArrangeTextInBoxService.DotValue(n.Coord, rightDir))
                    .ToList();

                SelectNotes(uidoc, notes);

                int arrangeCount = 0;

                // Group the whole session so a single Ctrl+Z reverses every resize and move - and the
                // work-plane setup below, which is rolled back with everything else if nothing is
                // arranged (Esc before the first box leaves the view exactly as it was).
                using (TransactionGroup group = new TransactionGroup(doc, "AJ-Tools: Arrange Text in Box"))
                {
                    group.Start();
                    try
                    {
                        // PickPoint throws when the view has no work plane, which is the normal state of
                        // a section, elevation or detail view - so give the view one before the first pick.
                        if (!EnsureWorkPlane(doc, view))
                        {
                            DialogHelper.ShowError(
                                "Arrange Text in Box",
                                "Could not prepare the active view for point picking.");
                            return Result.Cancelled;
                        }

                        XYZ topLeft = uidoc.Selection.PickPoint("Pick the TOP-LEFT corner of the box (one time)");

                        while (true)
                        {
                            XYZ bottomRight;
                            try
                            {
                                bottomRight = uidoc.Selection.PickPoint(
                                    "Pick the BOTTOM-RIGHT corner. Press Esc to finish.");
                            }
                            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                            {
                                break;
                            }

                            if (ArrangeTextInBoxService.ArrangeNotes(doc, view, notes, topLeft, bottomRight, rightDir, upDir))
                                arrangeCount++;

                            SelectNotes(uidoc, notes);
                        }

                        if (arrangeCount > 0)
                            group.Assimilate();
                        else
                            group.RollBack();
                    }
                    finally
                    {
                        // Esc on the TOP-LEFT pick or an error ends here with the group still open -
                        // undo the work-plane change explicitly rather than relying on disposal.
                        if (group.HasStarted() && !group.HasEnded())
                            group.RollBack();
                    }
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc before any box was drawn - normal cancel, no message.
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Returns preselected text notes, or prompts the user to pick text notes one-by-one.
        /// </summary>
        private static List<TextNote> GetTextNotes(UIDocument uidoc, Document doc)
        {
            List<TextNote> notes = new List<TextNote>();

            foreach (ElementId id in uidoc.Selection.GetElementIds())
            {
                if (doc.GetElement(id) is TextNote preselected)
                    notes.Add(preselected);
            }

            if (notes.Count > 0)
                return notes;

            IList<Reference> refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new TextNoteSelectionFilter(),
                "Pick the text notes to arrange, then click Finish");

            foreach (Reference reference in refs)
            {
                if (doc.GetElement(reference) is TextNote picked)
                    notes.Add(picked);
            }

            return notes;
        }

        /// <summary>
        /// Reselects the notes so they stay highlighted between picks.
        /// </summary>
        private static void SelectNotes(UIDocument uidoc, IList<TextNote> notes)
        {
            List<ElementId> ids = new List<ElementId>(notes.Count);
            foreach (TextNote note in notes)
                ids.Add(note.Id);

            uidoc.Selection.SetElementIds(ids);
        }

        /// <summary>
        /// Only graphical drawing views support point picking and hold text notes.
        /// </summary>
        private static bool IsPickableView(View view)
        {
            if (view == null || view.IsTemplate)
                return false;

            switch (view.ViewType)
            {
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.EngineeringPlan:
                case ViewType.AreaPlan:
                case ViewType.Section:
                case ViewType.Elevation:
                case ViewType.Detail:
                case ViewType.DraftingView:
                case ViewType.Legend:
                case ViewType.DrawingSheet:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gives a section, elevation or detail view a work plane on its own view plane when it has
        /// none, because PickPoint throws in a view without one. Plans already carry their level's
        /// work plane, and drafting, legend and sheet views are left untouched. The transaction is
        /// committed before the first pick (the API's own advice for PickPoint) but sits inside the
        /// caller's TransactionGroup, so it is undone together with the rest of the session.
        /// </summary>
        private static bool EnsureWorkPlane(Document doc, View view)
        {
            if (view.ViewType != ViewType.Section &&
                view.ViewType != ViewType.Elevation &&
                view.ViewType != ViewType.Detail)
            {
                return true;
            }

            if (view.SketchPlane != null)
                return true;

            try
            {
                using (Transaction t = new Transaction(doc, "Set Work Plane"))
                {
                    t.Start();
                    Plane plane = Plane.CreateByOriginAndBasis(view.Origin, view.RightDirection, view.UpDirection);
                    view.SketchPlane = SketchPlane.Create(doc, plane);
                    t.Commit();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
