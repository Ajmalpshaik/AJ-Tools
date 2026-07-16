#region Metadata
/*
 * Tool Name     : Arrange Text in Box
 * File Name     : CmdArrangeTextInBox.cs
 * Purpose       : Fits selected text notes into a rectangle the user drags. Each note's width is set to
 *                 the box width and the notes are spread evenly top-to-bottom with left edges aligned.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-05
 * Last Updated  : 2026-07-05
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (TextNoteSelectionFilter, DialogHelper)
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
 * - Esc during a pick is a normal cancel (handled silently); normal success is silent.
 * - Gaps and spacing scale with the view scale, matching paper-size intent (mm converted to feet).
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-05) - Initial release. Ported from the pyRevit "Text Box Arrange Loop" script.
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
        // Paper-space gaps (mm) applied inside the box; converted to feet and scaled by the view scale.
        private const double SideGapMm = 1.0;
        private const double TopGapMm = 3.5;
        private const double BottomGapMm = 3.5;
        private const double MmPerFoot = 304.8;

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
                    .OrderByDescending(n => DotValue(n.Coord, upDir))
                    .ThenBy(n => DotValue(n.Coord, rightDir))
                    .ToList();

                SelectNotes(uidoc, notes);

                XYZ topLeft = uidoc.Selection.PickPoint("Pick the TOP-LEFT corner of the box (one time)");

                int arrangeCount = 0;

                // Group the whole session so a single Ctrl+Z reverses every resize and move.
                using (TransactionGroup group = new TransactionGroup(doc, "AJ-Tools: Arrange Text in Box"))
                {
                    group.Start();

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

                        if (ArrangeNotes(doc, view, notes, topLeft, bottomRight, rightDir, upDir))
                            arrangeCount++;

                        SelectNotes(uidoc, notes);
                    }

                    if (arrangeCount > 0)
                        group.Assimilate();
                    else
                        group.RollBack();
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
        /// Resizes and distributes the notes inside the rectangle defined by the two picked corners.
        /// Returns false (no model change) when the rectangle is degenerate.
        /// </summary>
        private static bool ArrangeNotes(
            Document doc,
            View view,
            IList<TextNote> notes,
            XYZ topLeftPoint,
            XYZ bottomRightPoint,
            XYZ rightDir,
            XYZ upDir)
        {
            // On a sheet everything is already at paper size, so treat the scale as 1.
            // Guard against any view reporting a non-positive scale.
            double scale = view is ViewSheet ? 1.0 : view.Scale;
            if (scale <= 0)
                scale = 1.0;

            double rectLeftX = DotValue(topLeftPoint, rightDir);
            double rectTopY = DotValue(topLeftPoint, upDir);
            double rectRightX = DotValue(bottomRightPoint, rightDir);
            double rectBottomY = DotValue(bottomRightPoint, upDir);

            // The second corner must be to the right of and below the first.
            if (rectRightX <= rectLeftX || rectBottomY >= rectTopY)
                return false;

            double sideGap = (SideGapMm / MmPerFoot) * scale;
            double topGap = (TopGapMm / MmPerFoot) * scale;
            double bottomGap = (BottomGapMm / MmPerFoot) * scale;

            double targetLeftX = rectLeftX + sideGap;
            double targetRightX = rectRightX - sideGap;

            double targetFirstMidY = rectTopY - topGap;
            double targetLastMidY = rectBottomY + bottomGap;

            double modelWidth = targetRightX - targetLeftX;
            if (modelWidth <= 0)
                return false;

            double paperWidth = modelWidth / scale;

            int count = notes.Count;
            double spacing = count <= 1 ? 0.0 : (targetFirstMidY - targetLastMidY) / (count - 1);

            using (Transaction trans = new Transaction(doc, "Arrange Text in Box"))
            {
                trans.Start();

                foreach (TextNote note in notes)
                    note.Width = SafeTextWidth(doc, note, paperWidth);

                doc.Regenerate();

                for (int index = 0; index < count; index++)
                {
                    TextNote note = notes[index];

                    if (!TryGetBBoxLeftAndMid(note, view, rightDir, upDir, out double bboxLeftX, out double bboxMidY))
                        continue;

                    double targetMidY = targetFirstMidY - (spacing * index);

                    double moveX = targetLeftX - bboxLeftX;
                    double moveY = targetMidY - bboxMidY;

                    XYZ moveVector = rightDir.Multiply(moveX).Add(upDir.Multiply(moveY));
                    ElementTransformUtils.MoveElement(doc, note.Id, moveVector);
                }

                trans.Commit();
            }

            return true;
        }

        /// <summary>
        /// Clamps the requested width to Revit's allowed range for the note's text type.
        /// </summary>
        private static double SafeTextWidth(Document doc, TextNote note, double width)
        {
            ElementId typeId = note.GetTypeId();
            double minWidth = TextNote.GetMinimumAllowedWidth(doc, typeId);
            double maxWidth = TextNote.GetMaximumAllowedWidth(doc, typeId);
            return Clamp(width, minWidth, maxWidth);
        }

        /// <summary>
        /// Gets the note's left edge and vertical centre in the view's right/up frame from its
        /// bounding box. Returns false when the note has no bounding box in this view.
        /// </summary>
        private static bool TryGetBBoxLeftAndMid(
            TextNote note,
            View view,
            XYZ rightDir,
            XYZ upDir,
            out double leftX,
            out double midY)
        {
            leftX = 0.0;
            midY = 0.0;

            BoundingBoxXYZ bbox = note.get_BoundingBox(view);
            if (bbox == null)
                return false;

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            foreach (XYZ corner in GetBBoxCorners(bbox))
            {
                double x = DotValue(corner, rightDir);
                double y = DotValue(corner, upDir);

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            leftX = minX;
            midY = (maxY + minY) / 2.0;
            return true;
        }

        /// <summary>
        /// Returns the eight corner points of a bounding box.
        /// </summary>
        private static IEnumerable<XYZ> GetBBoxCorners(BoundingBoxXYZ bbox)
        {
            XYZ mn = bbox.Min;
            XYZ mx = bbox.Max;

            yield return new XYZ(mn.X, mn.Y, mn.Z);
            yield return new XYZ(mx.X, mn.Y, mn.Z);
            yield return new XYZ(mn.X, mx.Y, mn.Z);
            yield return new XYZ(mx.X, mx.Y, mn.Z);
            yield return new XYZ(mn.X, mn.Y, mx.Z);
            yield return new XYZ(mx.X, mn.Y, mx.Z);
            yield return new XYZ(mn.X, mx.Y, mx.Z);
            yield return new XYZ(mx.X, mx.Y, mx.Z);
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

        private static double DotValue(XYZ point, XYZ direction)
        {
            return point.DotProduct(direction);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
