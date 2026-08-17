#region Metadata
/*
 * Tool Name     : Tag Clash - Shared View / Tag Geometry
 * File Name     : TagViewGeometry.cs
 * Purpose       : The single place that turns 3D model geometry into flat view-plane rectangles, and
 *                 that measures a tag's TEXT box (as opposed to its raw bounding box, which also
 *                 contains the leader). Before this file the same work existed three times over:
 *                 SmartTagPlacementEngine.ConvertBoundingBoxToViewPlane / TryGetTagTextBoxSizeInViewPlane
 *                 / TryGetTagBoundsInView, and ForceTagLeaderLShapeService.TryGetTagBoundsInView -
 *                 plus a sixth copy of the raw 8-corner loop in SmartMepTagService.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-16
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, System.Reflection, AJTools.Services.SmartTag (AnnotationBox),
 *                 AJTools.Utils (Constants)
 *
 * Input         : Model points, bounding boxes, tag elements, and the active view.
 * Output        : Flat view-plane rectangles (AnnotationBox) and UV points.
 *
 * Notes         :
 * - Bounding boxes are walked corner by corner through their own Transform, so ROTATED boxes and
 *   rotated views give correct extents. Taking Min/Max directly is wrong and was the bug this
 *   technique exists to avoid.
 * - The "re-centre on the tag head" trick is kept exactly as Smart MEP Tag and L-Shape Leader both
 *   had it: a tag's bounding box includes its leader, so one side is stretched. Mirroring the
 *   shorter half about the head gives a far better estimate of the TEXT extents. Verified behaviour,
 *   not a guess - do not "simplify" it away.
 * - TagHeadPosition is read by reflection, not by casting to IndependentTag, because Revit's tag
 *   types (IndependentTag, SpatialElementTag, ...) share no base class exposing it.
 * - Every "paper mm" here is millimetres on the printed sheet. PaperMmToFeet applies the view scale.
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release. New shared block; the three older private copies still
 *                       exist and are switched over separately once a Windows build can verify it.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Reflection;
using Autodesk.Revit.DB;
using AJTools.Services.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.TagClash
{
    /// <summary>
    /// Shared view-plane projection and tag measurement helpers.
    /// </summary>
    internal static class TagViewGeometry
    {
        /// <summary>The view's horizontal axis, falling back to world X.</summary>
        internal static XYZ GetViewRight(View view)
        {
            try
            {
                if (view != null
                    && view.RightDirection != null
                    && view.RightDirection.GetLength() > Constants.ZERO_LENGTH_TOLERANCE)
                {
                    return view.RightDirection.Normalize();
                }
            }
            catch (Exception)
            {
                // Falls through to the world-axis default below.
            }

            return XYZ.BasisX;
        }

        /// <summary>The view's vertical axis, falling back to world Y.</summary>
        internal static XYZ GetViewUp(View view)
        {
            try
            {
                if (view != null
                    && view.UpDirection != null
                    && view.UpDirection.GetLength() > Constants.ZERO_LENGTH_TOLERANCE)
                {
                    return view.UpDirection.Normalize();
                }
            }
            catch (Exception)
            {
                // Falls through to the world-axis default below.
            }

            return XYZ.BasisY;
        }

        /// <summary>The view scale (1:x), never less than 1.</summary>
        internal static int GetViewScale(View view)
        {
            try
            {
                if (view != null && view.Scale > 0)
                    return view.Scale;
            }
            catch (Exception)
            {
                // Falls through to the 1 default below.
            }

            return 1;
        }

        /// <summary>Converts millimetres on the printed sheet to model feet at this view's scale.</summary>
        internal static double PaperMmToFeet(double paperMm, int viewScale)
        {
            int safeScale = viewScale > 0 ? viewScale : 1;
            return paperMm * Constants.MM_TO_FEET * safeScale;
        }

        /// <summary>Converts model feet back to millimetres on the printed sheet at this view's scale.</summary>
        internal static double FeetToPaperMm(double feet, int viewScale)
        {
            int safeScale = viewScale > 0 ? viewScale : 1;
            return feet / Constants.MM_TO_FEET / safeScale;
        }

        /// <summary>Flattens a model point onto the view plane.</summary>
        internal static UV ProjectToView(XYZ point, XYZ viewRight, XYZ viewUp)
        {
            if (point == null || viewRight == null || viewUp == null)
                return new UV(0, 0);

            return new UV(point.DotProduct(viewRight), point.DotProduct(viewUp));
        }

        /// <summary>Moves a model point by view-space horizontal / vertical amounts.</summary>
        internal static XYZ OffsetInView(XYZ point, double deltaRight, double deltaUp, XYZ viewRight, XYZ viewUp)
        {
            if (point == null || viewRight == null || viewUp == null)
                return point;

            return point
                .Add(viewRight.Multiply(deltaRight))
                .Add(viewUp.Multiply(deltaUp));
        }

        /// <summary>Flat view-plane distance between two model points.</summary>
        internal static double DistanceInView(XYZ a, XYZ b, XYZ viewRight, XYZ viewUp)
        {
            UV ua = ProjectToView(a, viewRight, viewUp);
            UV ub = ProjectToView(b, viewRight, viewUp);
            double du = ua.U - ub.U;
            double dv = ua.V - ub.V;
            return Math.Sqrt((du * du) + (dv * dv));
        }

        /// <summary>
        /// Converts a bounding box to a flat view-plane rectangle, walking all eight corners through
        /// the box's own Transform so rotated boxes and rotated views come out right.
        /// </summary>
        internal static AnnotationBox ToViewBox(BoundingBoxXYZ box, XYZ viewRight, XYZ viewUp)
        {
            if (box == null || box.Min == null || box.Max == null)
                return null;

            try
            {
                XYZ min = box.Min;
                XYZ max = box.Max;
                Transform transform = box.Transform ?? Transform.Identity;

                XYZ[] corners = new[]
                {
                    new XYZ(min.X, min.Y, min.Z),
                    new XYZ(min.X, min.Y, max.Z),
                    new XYZ(min.X, max.Y, min.Z),
                    new XYZ(min.X, max.Y, max.Z),
                    new XYZ(max.X, min.Y, min.Z),
                    new XYZ(max.X, min.Y, max.Z),
                    new XYZ(max.X, max.Y, min.Z),
                    new XYZ(max.X, max.Y, max.Z)
                };

                double minU = double.MaxValue;
                double minV = double.MaxValue;
                double maxU = double.MinValue;
                double maxV = double.MinValue;

                foreach (XYZ corner in corners)
                {
                    UV uv = ProjectToView(transform.OfPoint(corner), viewRight, viewUp);
                    if (uv.U < minU) minU = uv.U;
                    if (uv.V < minV) minV = uv.V;
                    if (uv.U > maxU) maxU = uv.U;
                    if (uv.V > maxV) maxV = uv.V;
                }

                if (minU > maxU || minV > maxV)
                    return null;

                return new AnnotationBox(minU, minV, maxU, maxV);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Reads an element's bounding box in the view, falling back to model extents.</summary>
        internal static BoundingBoxXYZ GetBoundingBox(Element element, View view)
        {
            if (element == null)
                return null;

            try
            {
                if (view != null)
                {
                    BoundingBoxXYZ viewBox = element.get_BoundingBox(view);
                    if (viewBox != null)
                        return viewBox;
                }
            }
            catch (Exception)
            {
                // Falls through to the model-extents attempt below.
            }

            try
            {
                return element.get_BoundingBox(null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads a tag's head position by reflection, so any tag type works (IndependentTag,
        /// SpatialElementTag and friends share no base class exposing this).
        /// </summary>
        internal static XYZ GetTagHeadPosition(Element tag)
        {
            if (tag == null)
                return null;

            try
            {
                PropertyInfo prop = tag.GetType().GetProperty(
                    "TagHeadPosition", BindingFlags.Instance | BindingFlags.Public);
                if (prop == null)
                    return null;

                return prop.GetValue(tag, null) as XYZ;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>True when this element exposes a tag head, i.e. it behaves like a tag.</summary>
        internal static bool LooksLikeTag(Element element)
        {
            if (element == null)
                return false;

            try
            {
                return element.GetType().GetProperty(
                    "TagHeadPosition", BindingFlags.Instance | BindingFlags.Public) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Measures a tag's TEXT box in the view plane. A tag's raw bounding box also covers its
        /// leader, so one side is stretched far past the text; when the head sits inside the box the
        /// shorter half is mirrored about the head to recover a realistic text extent.
        /// </summary>
        internal static AnnotationBox GetTagTextBox(Element tag, View view, XYZ viewRight, XYZ viewUp)
        {
            AnnotationBox raw = ToViewBox(GetBoundingBox(tag, view), viewRight, viewUp);
            if (raw == null)
                return null;

            XYZ head = GetTagHeadPosition(tag);
            if (head == null)
                return raw;

            UV headUv = ProjectToView(head, viewRight, viewUp);

            bool headInside = headUv.U > raw.MinX && headUv.U < raw.MaxX
                           && headUv.V > raw.MinY && headUv.V < raw.MaxY;
            if (!headInside)
                return raw;

            double halfWidth = Math.Min(headUv.U - raw.MinX, raw.MaxX - headUv.U);
            double halfHeight = Math.Min(headUv.V - raw.MinY, raw.MaxY - headUv.V);

            double minX = raw.MinX;
            double maxX = raw.MaxX;
            double minY = raw.MinY;
            double maxY = raw.MaxY;

            if (halfWidth > Constants.ZERO_LENGTH_TOLERANCE)
            {
                minX = headUv.U - halfWidth;
                maxX = headUv.U + halfWidth;
            }

            if (halfHeight > Constants.ZERO_LENGTH_TOLERANCE)
            {
                minY = headUv.V - halfHeight;
                maxY = headUv.V + halfHeight;
            }

            if (minX > maxX || minY > maxY)
                return raw;

            return new AnnotationBox(minX, minY, maxX, maxY);
        }

        /// <summary>Shifts a rectangle by view-space amounts, keeping its size.</summary>
        internal static AnnotationBox ShiftBox(AnnotationBox box, double deltaRight, double deltaUp)
        {
            if (box == null)
                return null;

            return new AnnotationBox(
                box.MinX + deltaRight,
                box.MinY + deltaUp,
                box.MaxX + deltaRight,
                box.MaxY + deltaUp);
        }
    }
}
