#region Metadata
/*
 * Tool Name     : L-Shape Leader
 * File Name     : ForceTagLeaderLShapeService.cs
 * Purpose       : Implements the non-elbow-math logic behind forcing a tag leader into an L-shape:
 *                 reflection-based tag property access (tags don't share a common base type with a
 *                 LeaderElbow/LeaderEndCondition property, so these are read/written by name),
 *                 bounding-box math, and elbow-adjustment/margin geometry. The core elbow-position
 *                 math itself stays in the shared Services/LeaderLogic/LeaderLogicService.cs (used
 *                 by other tools too) - this Service calls into it, it does not duplicate it.
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
 * Dependencies  : Autodesk Revit API, System.Reflection, AJTools.Services.LeaderLogic (LeaderLogicService),
 *                 AJTools.Utils (Constants)
 *
 * Input         : A single tag Element, the active View, and a LeaderLogicService instance built by
 *                 the caller (CmdForceTagLeaderLShape.cs, which owns selection, the pick-loop, and
 *                 the transaction/sub-transaction per tag).
 * Output         : True/false per tag - whether its leader was successfully forced into an L-shape.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Reflection is used because Revit's tag element types (IndependentTag, SpatialElementTag, etc.)
 *   don't share a common base exposing LeaderElbow/LeaderEndCondition/TagHeadPosition/LeaderEnd -
 *   these are read/written by property name across whatever concrete tag type is passed in.
 * - Pure algorithm - no direct Selection/dialog/transaction interaction; the caller owns the
 *   transaction (or sub-transaction) per tag and tallies success/failure.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-17) - Initial extraction from CmdForceTagLeaderLShape.cs (code review cleanup
 *                       pass) - no behavior change. The prior cleanup pass's empty-catch comments
 *                       (reflection can legitimately fail if a property is missing on some element)
 *                       are preserved as-is.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Reflection;
using Autodesk.Revit.DB;
using AJTools.Services.LeaderLogic;
using AJTools.Utils;

namespace AJTools.Services.ForceTagLeaderLShape
{
    /// <summary>
    /// Reflection-based tag property access, bounding-box math, and elbow-adjustment/margin
    /// geometry for forcing a tag leader into an L-shape. Core elbow-position math is delegated to
    /// the shared LeaderLogicService, not duplicated here.
    /// </summary>
    internal static class ForceTagLeaderLShapeService
    {
        private const double ElbowOutsideTextMarginMm = 3.0;

        /// <summary>
        /// Attempts to force the given tag's leader into an L-shape elbow. Returns false when the
        /// tag has no writable LeaderElbow property, no leader, or the elbow could not be computed.
        /// </summary>
        internal static bool TryForceLShape(Element tag, View activeView, LeaderLogicService leaderLogic)
        {
            if (tag == null || leaderLogic == null)
                return false;

            if (!EnsureLeaderEnabled(tag))
                return false;

            if (!HasWritableProperty(tag, "LeaderElbow"))
                return false;

            // Determine if horizontal or vertical
            bool isHorizontal = true; // default
            if (TryGetProperty(tag, "TagOrientation", out object orientationObj) && orientationObj != null)
            {
                if (string.Equals(orientationObj.ToString(), "Vertical", StringComparison.OrdinalIgnoreCase))
                {
                    isHorizontal = false;
                }
            }

            if (isHorizontal)
            {
                return TryForceLShapeHorizontal(tag, activeView, leaderLogic);
            }
            else
            {
                return TryForceLShapeVertical(tag, activeView, leaderLogic);
            }
        }

        private static bool TryForceLShapeHorizontal(Element tag, View activeView, LeaderLogicService leaderLogic)
        {
            bool hasInitialCondition = TryGetLeaderEndCondition(tag, out object initialCondition);

            if (TryApplyComputedElbowHorizontal(tag, activeView, leaderLogic))
                return true;

            if (!TrySetLeaderEndCondition(tag, "Free"))
                return false;

            if (!TryApplyComputedElbowHorizontal(tag, activeView, leaderLogic))
                return false;

            if (hasInitialCondition && !TrySetLeaderEndConditionValue(tag, initialCondition))
                return false;

            return true;
        }

        private static bool TryApplyComputedElbowHorizontal(Element tag, View activeView, LeaderLogicService leaderLogic)
        {
            if (!TryGetXYZProperty(tag, "TagHeadPosition", out XYZ head))
                return false;

            if (!TryGetXYZProperty(tag, "LeaderEnd", out XYZ end))
                return false;

            XYZ elbow = leaderLogic.ComputeElbow(head, end);

            if (elbow == null)
                return true;

            elbow = AdjustElbowOutsideTextBoundsRight(tag, activeView, leaderLogic, elbow);
            return TrySetXYZProperty(tag, "LeaderElbow", elbow);
        }

        private static XYZ AdjustElbowOutsideTextBoundsRight(
            Element tag,
            View activeView,
            LeaderLogicService leaderLogic,
            XYZ elbow)
        {
            if (tag == null || leaderLogic == null || elbow == null)
                return elbow;

            if (!TryGetTagBoundsInView(tag, activeView, leaderLogic, out double minX, out double maxX, out double minY, out double maxY))
                return elbow;

            UV elbowUv = leaderLogic.ProjectToView(elbow);
            if (!IsPointInsideBounds(elbowUv, minX, maxX, minY, maxY))
                return elbow;

            double rightMarginFeet = GetScaledElbowOutsideMarginFeet(activeView);
            double targetX = maxX + rightMarginFeet;
            double deltaX = targetX - elbowUv.U;
            return leaderLogic.OffsetInView(elbow, deltaX, 0);
        }

        private static bool TryForceLShapeVertical(Element tag, View activeView, LeaderLogicService leaderLogic)
        {
            bool hasInitialCondition = TryGetLeaderEndCondition(tag, out object initialCondition);

            if (TryApplyComputedElbowVertical(tag, activeView, leaderLogic))
                return true;

            if (!TrySetLeaderEndCondition(tag, "Free"))
                return false;

            if (!TryApplyComputedElbowVertical(tag, activeView, leaderLogic))
                return false;

            if (hasInitialCondition && !TrySetLeaderEndConditionValue(tag, initialCondition))
                return false;

            return true;
        }

        private static bool TryApplyComputedElbowVertical(Element tag, View activeView, LeaderLogicService leaderLogic)
        {
            if (!TryGetXYZProperty(tag, "TagHeadPosition", out XYZ head))
                return false;

            if (!TryGetXYZProperty(tag, "LeaderEnd", out XYZ end))
                return false;

            // Always use Top/Bottom attachment for vertical text, shifted outside the bounding box,
            // to perfectly mirror how horizontal text uses Side attachment shifted outside the box.
            XYZ elbow = leaderLogic.ComputeTopBottomElbow(head, end);

            if (elbow == null)
                return true;

            elbow = AdjustElbowTopBottom(tag, activeView, leaderLogic, elbow, head, end);

            return TrySetXYZProperty(tag, "LeaderElbow", elbow);
        }

        private static XYZ AdjustElbowTopBottom(
            Element tag,
            View activeView,
            LeaderLogicService leaderLogic,
            XYZ elbow,
            XYZ head,
            XYZ leaderEnd)
        {
            if (tag == null || leaderLogic == null || elbow == null)
                return elbow;

            if (!TryGetTagBoundsInView(tag, activeView, leaderLogic, out double minX, out double maxX, out double minY, out double maxY))
                return elbow;

            UV elbowUv = leaderLogic.ProjectToView(elbow);
            if (!IsPointInsideBounds(elbowUv, minX, maxX, minY, maxY))
                return elbow;

            double marginFeet = GetScaledElbowOutsideMarginFeet(activeView);
            UV headUv = leaderLogic.ProjectToView(head);
            UV endUv = leaderLogic.ProjectToView(leaderEnd);

            double targetY;
            if (endUv.V < headUv.V)
            {
                // Element is below, push elbow to the bottom
                targetY = minY - marginFeet;
            }
            else
            {
                // Element is above, push elbow to the top
                targetY = maxY + marginFeet;
            }

            double deltaY = targetY - elbowUv.V;
            return leaderLogic.OffsetInView(elbow, 0, deltaY);
        }

        private static double GetScaledElbowOutsideMarginFeet(View activeView)
        {
            int scale = 1;
            try
            {
                if (activeView != null && activeView.Scale > 0)
                    scale = activeView.Scale;
            }
            catch
            {
                // Falls through to the scale = 1 default above - not worth failing the whole tool over.
            }

            return ElbowOutsideTextMarginMm * Constants.MM_TO_FEET * scale;
        }

        private static bool TryGetTagBoundsInView(
            Element tag,
            View activeView,
            LeaderLogicService leaderLogic,
            out double minX,
            out double maxX,
            out double minY,
            out double maxY)
        {
            minX = 0;
            maxX = 0;
            minY = 0;
            maxY = 0;

            BoundingBoxXYZ bb = GetTagBoundingBox(tag, activeView);
            if (bb == null || bb.Min == null || bb.Max == null)
                return false;

            XYZ min = bb.Min;
            XYZ max = bb.Max;
            Transform transform = bb.Transform ?? Transform.Identity;
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

            double localMinX = double.MaxValue;
            double localMinY = double.MaxValue;
            double localMaxX = double.MinValue;
            double localMaxY = double.MinValue;

            foreach (XYZ corner in corners)
            {
                XYZ worldCorner = transform.OfPoint(corner);
                UV uv = leaderLogic.ProjectToView(worldCorner);
                if (uv.U < localMinX) localMinX = uv.U;
                if (uv.U > localMaxX) localMaxX = uv.U;
                if (uv.V < localMinY) localMinY = uv.V;
                if (uv.V > localMaxY) localMaxY = uv.V;
            }

            // Bounding boxes may include leader geometry. Re-center around TagHeadPosition
            // to better represent text extents when one side is heavily skewed.
            if (TryGetXYZProperty(tag, "TagHeadPosition", out XYZ headPoint))
            {
                UV headUv = leaderLogic.ProjectToView(headPoint);
                bool headInside = headUv != null
                    && headUv.U > localMinX && headUv.U < localMaxX
                    && headUv.V > localMinY && headUv.V < localMaxY;

                if (headInside)
                {
                    double left = headUv.U - localMinX;
                    double right = localMaxX - headUv.U;
                    double down = headUv.V - localMinY;
                    double up = localMaxY - headUv.V;

                    double halfWidth = Math.Min(left, right);
                    double halfHeight = Math.Min(down, up);

                    if (halfWidth > Constants.ZERO_LENGTH_TOLERANCE)
                    {
                        localMinX = headUv.U - halfWidth;
                        localMaxX = headUv.U + halfWidth;
                    }

                    if (halfHeight > Constants.ZERO_LENGTH_TOLERANCE)
                    {
                        localMinY = headUv.V - halfHeight;
                        localMaxY = headUv.V + halfHeight;
                    }
                }
            }

            if (localMinX > localMaxX || localMinY > localMaxY)
                return false;

            minX = localMinX;
            maxX = localMaxX;
            minY = localMinY;
            maxY = localMaxY;
            return true;
        }

        private static BoundingBoxXYZ GetTagBoundingBox(Element tag, View activeView)
        {
            if (tag == null)
                return null;

            try
            {
                if (activeView != null)
                {
                    BoundingBoxXYZ viewBox = tag.get_BoundingBox(activeView);
                    if (viewBox != null)
                        return viewBox;
                }
            }
            catch
            {
                // Falls through to the null/model-bounds fallback below.
            }

            try
            {
                return tag.get_BoundingBox(null);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPointInsideBounds(UV point, double minX, double maxX, double minY, double maxY)
        {
            if (point == null)
                return false;

            return point.U >= minX && point.U <= maxX
                && point.V >= minY && point.V <= maxY;
        }

        private static bool EnsureLeaderEnabled(Element tag)
        {
            if (!TryGetBoolProperty(tag, "HasLeader", out bool hasLeader))
                return true;

            if (hasLeader)
                return true;

            return TrySetBoolProperty(tag, "HasLeader", true);
        }

        private static bool TryGetProperty(Element tag, string propertyName, out object value)
        {
            value = null;
            PropertyInfo prop = GetProperty(tag, propertyName);
            if (prop == null)
                return false;
            try
            {
                value = prop.GetValue(tag, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetLeaderEndCondition(Element tag, out object value)
        {
            value = null;
            PropertyInfo prop = GetProperty(tag, "LeaderEndCondition");
            if (prop == null)
                return false;
            try
            {
                value = prop.GetValue(tag, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetLeaderEndCondition(Element tag, string targetConditionName)
        {
            if (string.IsNullOrWhiteSpace(targetConditionName))
                return false;
            PropertyInfo prop = GetProperty(tag, "LeaderEndCondition");
            if (prop == null || !prop.CanWrite)
                return false;
            try
            {
                object current = prop.GetValue(tag, null);
                if (IsCondition(current, targetConditionName))
                    return true;
                if (prop.PropertyType == typeof(LeaderEndCondition))
                {
                    LeaderEndCondition target = string.Equals(targetConditionName, "Attached", StringComparison.OrdinalIgnoreCase)
                        ? LeaderEndCondition.Attached
                        : LeaderEndCondition.Free;
                    prop.SetValue(tag, target, null);
                    return true;
                }
                if (prop.PropertyType.IsEnum)
                {
                    foreach (object enumValue in Enum.GetValues(prop.PropertyType))
                    {
                        if (IsCondition(enumValue, targetConditionName))
                        {
                            prop.SetValue(tag, enumValue, null);
                            return true;
                        }
                    }
                }
                int fallbackNumeric = string.Equals(targetConditionName, "Attached", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0;
                object numericValue = Convert.ChangeType(
                    fallbackNumeric,
                    prop.PropertyType.IsEnum ? Enum.GetUnderlyingType(prop.PropertyType) : prop.PropertyType);
                if (prop.PropertyType.IsEnum)
                    numericValue = Enum.ToObject(prop.PropertyType, numericValue);
                prop.SetValue(tag, numericValue, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetLeaderEndConditionValue(Element tag, object value)
        {
            if (value == null)
                return false;

            PropertyInfo prop = GetProperty(tag, "LeaderEndCondition");
            if (prop == null || !prop.CanWrite)
                return false;

            try
            {
                object targetValue;

                if (prop.PropertyType.IsInstanceOfType(value))
                {
                    targetValue = value;
                }
                else if (prop.PropertyType.IsEnum)
                {
                    if (value is string name)
                    {
                        targetValue = Enum.Parse(prop.PropertyType, name, true);
                    }
                    else
                    {
                        object numeric = Convert.ChangeType(value, Enum.GetUnderlyingType(prop.PropertyType));
                        targetValue = Enum.ToObject(prop.PropertyType, numeric);
                    }
                }
                else
                {
                    targetValue = Convert.ChangeType(value, prop.PropertyType);
                }

                prop.SetValue(tag, targetValue, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCondition(object value, string conditionName)
        {
            if (value == null || string.IsNullOrWhiteSpace(conditionName))
                return false;
            return string.Equals(value.ToString(), conditionName, StringComparison.OrdinalIgnoreCase);
        }

        #region Reflection Helpers

        private static PropertyInfo GetProperty(Element element, string propertyName)
        {
            if (element == null || string.IsNullOrEmpty(propertyName))
                return null;

            return element.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        }

        /// <summary>
        /// Also used by the caller (CmdForceTagLeaderLShape) to pre-filter preselected elements and
        /// to gate its pick-loop's selection filter to elements exposing a TagHeadPosition property.
        /// </summary>
        internal static bool HasProperty(Element element, string propertyName)
        {
            return GetProperty(element, propertyName) != null;
        }

        private static bool HasWritableProperty(Element element, string propertyName)
        {
            PropertyInfo prop = GetProperty(element, propertyName);
            return prop != null && prop.CanWrite;
        }

        private static bool TryGetXYZProperty(Element element, string propertyName, out XYZ value)
        {
            value = null;
            PropertyInfo prop = GetProperty(element, propertyName);
            if (prop == null)
                return false;

            try
            {
                object raw = prop.GetValue(element, null);
                if (raw is XYZ xyz)
                {
                    value = xyz;
                    return true;
                }
            }
            catch
            {
                // Falls through to "return false" below - reflection against the Revit API tag
                // properties, which can legitimately fail if a property is missing on some element.
            }

            return false;
        }

        private static bool TrySetXYZProperty(Element element, string propertyName, XYZ value)
        {
            PropertyInfo prop = GetProperty(element, propertyName);
            if (prop == null || !prop.CanWrite)
                return false;

            try
            {
                prop.SetValue(element, value, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetBoolProperty(Element element, string propertyName, out bool value)
        {
            value = false;
            PropertyInfo prop = GetProperty(element, propertyName);
            if (prop == null)
                return false;

            try
            {
                object raw = prop.GetValue(element, null);
                if (raw is bool flag)
                {
                    value = flag;
                    return true;
                }
            }
            catch
            {
                // Falls through to "return false" below - same reflection-safety note as
                // TryGetXYZProperty above.
            }

            return false;
        }

        private static bool TrySetBoolProperty(Element element, string propertyName, bool value)
        {
            PropertyInfo prop = GetProperty(element, propertyName);
            if (prop == null || !prop.CanWrite)
                return false;

            try
            {
                prop.SetValue(element, value, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
