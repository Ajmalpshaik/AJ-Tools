#region Metadata
/*
 * Tool Name     : Arrange Text in Box
 * File Name     : ArrangeTextInBoxService.cs
 * Purpose       : Implements the text-box-fit algorithm: rectangle math in the active view's own
 *                 right/up frame, bounding-box corner projection, text width clamping, and even
 *                 top-to-bottom vertical distribution used to fit selected text notes into a
 *                 user-dragged rectangle.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-17
 * Last Updated  : 2026-07-17
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Document, View, the picked text notes, and the two picked box corners - all
 *                 supplied by CmdArrangeTextInBox.cs, which owns selection, the pick-loop, and the
 *                 enclosing TransactionGroup.
 * Output        : Text notes resized to the box width and evenly distributed inside it (each
 *                 resize/move batched into its own Transaction, called once per picked rectangle).
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Works in the view's own right/up frame (View.RightDirection / UpDirection), so it stays correct
 *   in rotated plan views and in section / elevation / drafting / sheet views.
 * - Pure algorithm - no direct selection/dialog interaction; the caller owns the TransactionGroup,
 *   this Service opens its own Transaction per call (correct nested pattern: one TransactionGroup
 *   per pick session, one Transaction per picked rectangle).
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-17) - Initial extraction from CmdArrangeTextInBox.cs (code review cleanup pass) -
 *                       no behavior change.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Services.ArrangeTextInBox
{
    /// <summary>
    /// Text-box-fit algorithm: rectangle math, bounding-box projection, width clamping, and even
    /// vertical distribution. Contains no direct selection/dialog interaction.
    /// </summary>
    internal static class ArrangeTextInBoxService
    {
        // Paper-space gaps (mm) applied inside the box; converted to feet and scaled by the view scale.
        private const double SideGapMm = 1.0;
        private const double TopGapMm = 3.5;
        private const double BottomGapMm = 3.5;
        private const double MmPerFoot = 304.8;

        /// <summary>
        /// Resizes and distributes the notes inside the rectangle defined by the two picked corners.
        /// Returns false (no model change) when the rectangle is degenerate.
        /// </summary>
        internal static bool ArrangeNotes(
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
        /// Projects a point onto a direction vector via dot product. Also used by the Command to
        /// sort notes top-to-bottom / left-to-right before a rectangle is picked.
        /// </summary>
        internal static double DotValue(XYZ point, XYZ direction)
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
