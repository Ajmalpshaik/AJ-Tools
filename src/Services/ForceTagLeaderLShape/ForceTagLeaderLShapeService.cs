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
 * Version       : 1.0.1
 *
 * Created Date  : 2026-07-17
 * Last Updated  : 2026-10-12
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
 * - EXCEPT an IndependentTag's leader end and elbow: Revit 2023 removed IndependentTag.LeaderEnd and
 *   .LeaderElbow (only GetLeaderEnd(reference) / SetLeaderElbow(reference, point) remain), so those
 *   two go through LeaderLogicService.GetL1 / TrySetLeaderElbow, which wrap TagCompat. HasLeader,
 *   LeaderEndCondition, TagOrientation and TagHeadPosition still exist on IndependentTag in every
 *   version, so they stay on reflection. Room / space / area tags keep reflection for everything.
 * - Pure algorithm - no direct Selection/dialog/transaction interaction; the caller owns the
 *   transaction (or sub-transaction) per tag and tallies success/failure.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.1 (2026-10-12) - Fixed on Revit 2023 and newer: every element tag (MEP, door, keynote...)
 *                       was skipped ("No editable tag leaders were found") because the leader end
 *                       and elbow were read and written through IndependentTag properties that 2023
 *                       removed. Those two now go through the shared version-safe helpers for
 *                       IndependentTag. Room / space / area tags, the horizontal/vertical split and
 *                       the Free-then-restore leader end handling are unchanged.
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
        // The 3mm outside-text margin that used to be a constant here now lives with the nudge itself,
        // in TagLeaderService - one value, one place, shared with Smart MEP Tags.

        /// <summary>
        /// Attempts to force the given tag's leader into an L-shape elbow. Returns false when the
        /// tag's elbow cannot be written, it has no leader, or the elbow could not be computed.
        /// </summary>
        internal static bool TryForceLShape(Element tag, View activeView, LeaderLogicService leaderLogic)
        {
            if (tag == null || leaderLogic == null)
                return false;

            if (!EnsureLeaderEnabled(tag))
                return false;

            // An IndependentTag (MEP, door, keynote... tags) always takes an elbow through the
            // version-safe helpers - it has no LeaderElbow PROPERTY at all from Revit 2023 on, so this
            // check used to reject every one of them there. Room / space / area tags still have it.
            if (!(tag is IndependentTag) && !HasWritableProperty(tag, "LeaderElbow"))
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

            if (!TryGetLeaderEnd(tag, out XYZ end))
                return false;

            XYZ elbow = leaderLogic.ComputeElbow(head, end);

            if (elbow == null)
                return true;

            elbow = TagLeaderService.NudgeElbowClearOfText(tag, activeView, leaderLogic, elbow);
            return TrySetLeaderElbow(tag, elbow);
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

            if (!TryGetLeaderEnd(tag, out XYZ end))
                return false;

            // Always use Top/Bottom attachment for vertical text, shifted outside the bounding box,
            // to perfectly mirror how horizontal text uses Side attachment shifted outside the box.
            XYZ elbow = leaderLogic.ComputeTopBottomElbow(head, end);

            if (elbow == null)
                return true;

            elbow = TagLeaderService.NudgeElbowClearOfTextVertical(
                tag, activeView, leaderLogic, elbow, head, end);

            return TrySetLeaderElbow(tag, elbow);
        }

        /// <summary>
        /// Reads where the leader meets the element. An IndependentTag goes through the shared
        /// version-safe read (LeaderLogicService.GetL1 -> TagCompat): its LeaderEnd property was
        /// removed in Revit 2023 in favour of GetLeaderEnd(reference). Room / space / area tags
        /// (SpatialElementTag) still have the LeaderEnd property on every version, so they keep the
        /// reflection read. Either way an Attached leader end does not read - that is what the
        /// Free-then-restore retry in the callers above is for, unchanged.
        /// </summary>
        private static bool TryGetLeaderEnd(Element tag, out XYZ end)
        {
            if (tag is IndependentTag independentTag)
            {
                end = LeaderLogicService.GetL1(independentTag);
                return end != null;
            }

            return TryGetXYZProperty(tag, "LeaderEnd", out end);
        }

        /// <summary>
        /// Writes the elbow. An IndependentTag goes through the shared version-safe write
        /// (LeaderLogicService.TrySetLeaderElbow -> TagCompat), because its LeaderElbow property was
        /// removed in Revit 2023 in favour of SetLeaderElbow(reference, point). Room / space / area
        /// tags keep the reflection write.
        /// </summary>
        private static bool TrySetLeaderElbow(Element tag, XYZ elbow)
        {
            if (tag is IndependentTag independentTag)
                return LeaderLogicService.TrySetLeaderElbow(independentTag, elbow);

            return TrySetXYZProperty(tag, "LeaderElbow", elbow);
        }

        // Removed 2026-08-16 (about 240 lines): AdjustElbowOutsideTextBoundsRight,
        // AdjustElbowTopBottom, GetScaledElbowOutsideMarginFeet, TryGetTagBoundsInView,
        // GetTagBoundingBox and IsPointInsideBounds. The first was near-verbatim identical to
        // SmartTagPlacementEngine's copy, and the bounds/re-centre-on-the-head work was a third
        // copy of the same trick. They now live once, in Services/LeaderLogic/TagLeaderService.cs
        // (NudgeElbowClearOfText / NudgeElbowClearOfTextVertical) and
        // Services/TagClash/TagViewGeometry.cs. This file keeps only what is genuinely its own:
        // reflection-based tag property access, and the horizontal/vertical split that makes it the
        // one tool able to handle vertical tag text. Behaviour is unchanged.

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
