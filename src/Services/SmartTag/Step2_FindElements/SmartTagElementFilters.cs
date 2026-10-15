#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 2, Skip Rules
 * File Name     : SmartTagElementFilters.cs
 * Purpose       : Step 2 rules that decide an element is NOT tagged: too short or too small, a minor pipe
 *                 accessory, or outside the crop region.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-10-15
 * Last Updated  : 2026-10-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Utils, the other Smart MEP Tags files
 *
 * Input         : One element and the settings.
 * Output        : Yes/no, with the reason shown in the report.
 *
 * Notes         :
 * - Part of the Smart MEP Tags restructure (suite 1.58.0): the code that used to sit in
 *   SmartMepTagService.cs and SmartTagPlacementEngine.cs is split one job per file, in step
 *   folders. Every routine was moved as-is - tagging results are unchanged.
 * - The numbers that tune the algorithm live in SmartTagTuning.cs.
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Split out of the old Smart MEP Tag service / placement engine.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagElementGeometry;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// The skip rules: size, minor pipe accessories, crop region.
    /// </summary>
    internal static class SmartTagElementFilters
    {
        /// <summary>
        /// Checks whether an element passes the minimum size threshold for its category.
        /// Prevents tagging of small spurs, flex connections, and short segments.
        /// </summary>
        internal static bool PassesSizeFilter(
            Element elem, BuiltInCategory category, SmartTagSettingsState settings, out string reason)
        {
            reason = null;

            try
            {
                // Length and cross-section apply to curve-based elements only - duct, pipe and cable
                // tray. An accessory or a piece of equipment has no meaningful run length, so it is
                // never filtered on size; that matches what this method did before these became
                // settings, and what Create Tags does.
                MEPCurve mepCurve = elem as MEPCurve;
                if (mepCurve != null)
                {
                    double minLengthMm = settings != null
                        ? settings.MinLengthMm
                        : SmartTagSettingsTracker.DefaultMinLengthMm;

                    // 0 means no minimum on this axis, for all three values.
                    if (minLengthMm > 0.0)
                    {
                        double lengthMm = GetCurveLength(mepCurve) / Constants.MM_TO_FEET;
                        if (lengthMm >= 0 && lengthMm < minLengthMm)
                        {
                            reason = string.Format("Run length {0:F0}mm is below the {1:F0}mm minimum",
                                lengthMm, minLengthMm);
                            return false;
                        }
                    }

                    bool filterBySize = settings == null
                        ? SmartTagSettingsTracker.DefaultFilterBySize
                        : settings.FilterBySize;

                    if (filterBySize)
                    {
                        double minWidthMm = settings != null
                            ? settings.MinWidthMm
                            : SmartTagSettingsTracker.DefaultMinWidthMm;
                        double minHeightMm = settings != null
                            ? settings.MinHeightMm
                            : SmartTagSettingsTracker.DefaultMinHeightMm;

                        double widthMm;
                        double heightMm;

                        if ((minWidthMm > 0.0 || minHeightMm > 0.0)
                            && TryGetCrossSectionMm(mepCurve, out widthMm, out heightMm))
                        {
                            // Below on EITHER axis skips it (Ajmal's call, 2026-08-17) - a 400x50 duct
                            // is skipped against a 100x100 minimum, because 50 is under 100.
                            if (minWidthMm > 0.0 && widthMm < minWidthMm)
                            {
                                reason = string.Format("Width {0:F0}mm is below the {1:F0}mm minimum",
                                    widthMm, minWidthMm);
                                return false;
                            }

                            if (minHeightMm > 0.0 && heightMm < minHeightMm)
                            {
                                reason = string.Format("Height {0:F0}mm is below the {1:F0}mm minimum",
                                    heightMm, minHeightMm);
                                return false;
                            }
                        }
                    }
                }

                // The minor-accessory name check that used to sit here is now its own filter,
                // IsMinorPipeAccessory, so its skips are no longer reported as a size problem.
            }
            catch (Exception)
            {
                // If size check fails, include the element (safe default).
            }

            return true;
        }

        /// <summary>
        /// True for a pipe accessory that is not a valve, damper or other major fitting - those are
        /// left untagged. All duct accessories are tagged, regardless of size or subtype.
        /// </summary>
        internal static bool IsMinorPipeAccessory(Element elem, BuiltInCategory category, out string reason)
        {
            reason = null;

            // Only tag valves, dampers, and major fittings.
            // We check the family name for common accessory types.
            if (category == BuiltInCategory.OST_PipeAccessory && !IsMajorAccessory(elem))
            {
                reason = "Minor accessory — not a valve, damper, or major fitting";
                return true;
            }

            return false;
        }

        // GetPipeDiameter removed v1.49.7 - its only caller was the old hardcoded pipe size filter.
        // Its knowledge is not lost: TryGetCrossSectionMm reads RBS_PIPE_DIAMETER_PARAM as well as
        // RBS_CURVE_DIAMETER_PARAM, which is the whole reason pipes can now be size-filtered at all.

        /// <summary>
        /// Determines whether a pipe/duct accessory is a major fitting worth tagging.
        /// Checks family name for common valve, damper, and major accessory keywords.
        /// </summary>
        internal static bool IsMajorAccessory(Element elem)
        {
            try
            {
                string familyName = null;

                // Get the family name from the element's type.
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    FamilySymbol famSym = elem.Document.GetElement(typeId) as FamilySymbol;
                    if (famSym != null && famSym.Family != null)
                        familyName = famSym.Family.Name;
                }

                if (string.IsNullOrEmpty(familyName))
                    return true; // Unknown family — include to be safe.

                string upper = familyName.ToUpperInvariant();

                // Major accessories: valves, dampers, strainers, flow meters, etc.
                return upper.Contains("VALVE")
                    || upper.Contains("DAMPER")
                    || upper.Contains("STRAINER")
                    || upper.Contains("METER")
                    || upper.Contains("REGULATOR")
                    || upper.Contains("TRAP")
                    || upper.Contains("FILTER")
                    || upper.Contains("RELIEF")
                    || upper.Contains("CHECK")
                    || upper.Contains("GATE")
                    || upper.Contains("BALL")
                    || upper.Contains("BUTTERFLY")
                    || upper.Contains("GLOBE")
                    || upper.Contains("PRV")
                    || upper.Contains("CONTROL");
            }
            catch (Exception)
            {
                return true; // On error, include the element.
            }
        }

        /// <summary>
        /// Checks whether an element's bounding box overlaps the crop rectangle, measured in the
        /// view's own plane. Partially inside = true (the tag is pulled inside the crop later).
        /// Depth is deliberately ignored - a crop region does not control it, the view range does,
        /// and the view-scoped collector has already applied that. Anything unmeasurable is kept
        /// rather than dropped, matching the safe default used by the other filters here.
        /// </summary>
        internal static bool BoundingBoxOverlapsCropInViewPlane(
            BoundingBoxXYZ bbox,
            Transform modelToView,
            double cropMinX,
            double cropMinY,
            double cropMaxX,
            double cropMaxY)
        {
            try
            {
                if (bbox == null || modelToView == null)
                    return true;

                XYZ[] corners = GetBoundingBoxCorners(bbox);
                if (corners == null || corners.Length == 0)
                    return true;

                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;

                foreach (XYZ corner in corners)
                {
                    if (corner == null)
                        continue;

                    XYZ local = modelToView.OfPoint(corner);
                    if (local == null
                        || double.IsNaN(local.X) || double.IsInfinity(local.X)
                        || double.IsNaN(local.Y) || double.IsInfinity(local.Y))
                        return true;

                    minX = Math.Min(minX, local.X);
                    minY = Math.Min(minY, local.Y);
                    maxX = Math.Max(maxX, local.X);
                    maxY = Math.Max(maxY, local.Y);
                }

                if (minX > maxX || minY > maxY)
                    return true;

                if (maxX < cropMinX || minX > cropMaxX)
                    return false;
                if (maxY < cropMinY || minY > cropMaxY)
                    return false;

                return true;
            }
            catch (Exception)
            {
                return true; // On error, include the element.
            }
        }
    }
}
