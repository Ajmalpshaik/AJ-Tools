#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 2, Element Measurements
 * File Name     : SmartTagElementGeometry.cs
 * Purpose       : Step 2 measurements of one element: midpoint, which way it runs in the view, length, width
 *                 (so the tag gap is measured from the element's edge) and boxes. Shared with Create Tags, Stack
 *                 Tags and Fix Tag Clash so every tagging tool measures the same way.
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
 * Input         : One element and the view.
 * Output        : Points, directions and sizes in Revit internal units (feet).
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
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using AJTools.Models.SmartTag;
using AJTools.Services.TagClash;
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Measurements of one element: midpoint, run direction, length, width and boxes.
    /// </summary>
    internal static class SmartTagElementGeometry
    {
        /// <summary>
        /// Returns the midpoint of an MEP element.
        /// For curve elements: midpoint of the location curve.
        /// For equipment/accessories: centre of the bounding box.
        /// </summary>
        internal static XYZ GetElementMidpoint(Element elem, View view)
        {
            try
            {
                // Curve-based elements (ducts, pipes, cable trays): use curve midpoint.
                LocationCurve locCurve = elem.Location as LocationCurve;
                if (locCurve != null && locCurve.Curve != null)
                {
                    return locCurve.Curve.Evaluate(0.5, true);
                }

                // Point-based elements (equipment, accessories): use location point.
                LocationPoint locPoint = elem.Location as LocationPoint;
                if (locPoint != null)
                {
                    return locPoint.Point;
                }

                // Fallback: bounding box centre.
                BoundingBoxXYZ bb = elem.get_BoundingBox(view);
                return GetBoundingBoxCenter(bb);
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// Determines the orientation of an MEP element in the active view.
        /// Horizontal ducts/pipes → prefer tag above/below.
        /// Vertical risers → prefer tag left/right.
        /// </summary>
        internal static ElementOrientation GetElementOrientation(Element elem, View view)
        {
            try
            {
                LocationCurve locCurve = elem.Location as LocationCurve;
                if (locCurve == null || locCurve.Curve == null)
                    return ElementOrientation.Other; // Equipment/accessories with no curve direction.

                return GetCurveOrientation(locCurve.Curve, view);
            }
            catch (Exception)
            {
                return ElementOrientation.Other;
            }
        }

        /// <summary>
        /// The orientation rule of GetElementOrientation, for a run's curve already in this model's
        /// coordinates - which is how a linked run arrives, so a link turned in plan is classified by
        /// how it actually sits in the view.
        /// </summary>
        internal static ElementOrientation GetCurveOrientation(Curve curve, View view)
        {
            try
            {
                if (curve == null || view == null)
                    return ElementOrientation.Other;

                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                XYZ direction = (end - start).Normalize();

                if (direction == null)
                    return ElementOrientation.Other;

                // Section/elevation: vertical means mostly along global Z.
                ViewType vt = view.ViewType;
                if (vt == ViewType.Section || vt == ViewType.Elevation)
                {
                    if (Math.Abs(direction.Z) > 0.7)
                        return ElementOrientation.Vertical;
                    return ElementOrientation.Horizontal;
                }

                // Plan views: classify against view axes so rotated plans are handled correctly.
                XYZ viewRight = view.RightDirection;
                XYZ viewUp = view.UpDirection;
                XYZ viewNormal = view.ViewDirection;

                if (viewRight == null || viewUp == null || viewNormal == null)
                {
                    if (Math.Abs(direction.Z) > 0.7)
                        return ElementOrientation.Vertical;
                    return ElementOrientation.Horizontal;
                }

                viewRight = viewRight.Normalize();
                viewUp = viewUp.Normalize();
                viewNormal = viewNormal.Normalize();

                // True risers in plan (mostly perpendicular to the plan plane).
                double normalComponent = Math.Abs(direction.DotProduct(viewNormal));
                if (normalComponent > 0.7)
                    return ElementOrientation.Vertical;

                // Remove out-of-plane component and compare in-plane axis alignment.
                XYZ inPlane = direction - viewNormal.Multiply(direction.DotProduct(viewNormal));
                if (inPlane.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                    return ElementOrientation.Other;

                inPlane = inPlane.Normalize();
                double alongRight = Math.Abs(inPlane.DotProduct(viewRight));
                double alongUp = Math.Abs(inPlane.DotProduct(viewUp));

                if (alongRight >= alongUp)
                    return ElementOrientation.Horizontal;

                return ElementOrientation.Vertical;
            }
            catch (Exception)
            {
                return ElementOrientation.Other;
            }
        }

        /// <summary>
        /// Returns the length of an MEP curve element in feet.
        /// </summary>
        internal static double GetCurveLength(MEPCurve mepCurve)
        {
            try
            {
                // Try the Length parameter first (most reliable).
                Parameter lengthParam = mepCurve.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (lengthParam != null && lengthParam.HasValue)
                    return lengthParam.AsDouble();

                // Fallback: compute from the location curve.
                LocationCurve locCurve = mepCurve.Location as LocationCurve;
                if (locCurve != null && locCurve.Curve != null)
                    return locCurve.Curve.Length;
            }
            catch (Exception)
            {
                // Return -1 to indicate unknown — element won't be filtered by length.
            }
            return -1;
        }

        /// <summary>
        /// Reads a run's cross-section in mm. A round section reports its diameter as BOTH width and
        /// height (Ajmal's call, 2026-08-17), so a 150mm pipe clears a 100x100 minimum.
        /// </summary>
        /// <returns>False when neither a diameter nor a width/height pair could be read - the caller
        /// then applies no size filter at all rather than skipping an element it could not measure.</returns>
        internal static bool TryGetCrossSectionMm(MEPCurve mepCurve, out double widthMm, out double heightMm)
        {
            widthMm = 0.0;
            heightMm = 0.0;

            if (mepCurve == null)
                return false;

            try
            {
                // Round first, and it takes TWO parameters, not one: a round DUCT answers to
                // RBS_CURVE_DIAMETER_PARAM while a PIPE answers to RBS_PIPE_DIAMETER_PARAM. Reading
                // only the curve one measures every duct and no pipe at all - which would leave the
                // pipe size filter silently doing nothing, the exact bug the old hardcoded
                // MinPipeDiameter = 0 already had. Round is checked before width/height because a
                // round duct still carries width/height parameters that do not describe it.
                double diameterMm = ReadParameterMm(mepCurve, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                if (diameterMm <= 0.0)
                    diameterMm = ReadParameterMm(mepCurve, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);

                if (diameterMm > 0.0)
                {
                    widthMm = diameterMm;
                    heightMm = diameterMm;
                    return true;
                }

                widthMm = ReadParameterMm(mepCurve, BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                heightMm = ReadParameterMm(mepCurve, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

                // A cable tray keeps its size in its own pair of parameters, not the duct ones.
                // Without these the width/height minimums the settings window says apply to cable
                // trays never reached a single tray.
                if (widthMm <= 0.0 || heightMm <= 0.0)
                {
                    widthMm = ReadParameterMm(mepCurve, BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                    heightMm = ReadParameterMm(mepCurve, BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                }

                return widthMm > 0.0 && heightMm > 0.0;
            }
            catch (Exception)
            {
                // Unmeasurable - say so, and let the caller tag it rather than drop it.
                return false;
            }
        }

        /// <summary>
        /// Reads one length parameter in mm, or 0 when it is absent, empty or unreadable.
        /// </summary>
        internal static double ReadParameterMm(Element element, BuiltInParameter parameter)
        {
            try
            {
                Parameter found = element.get_Parameter(parameter);
                if (found == null || !found.HasValue)
                    return 0.0;

                return found.AsDouble() / Constants.MM_TO_FEET;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        /// <summary>
        /// The duct's width in feet - its diameter for a round duct. Returns -1 when neither can be
        /// read, so callers must clamp before using it as a distance.
        /// </summary>
        internal static double GetDuctWidth(Duct duct)
        {
            try
            {
                // Round duct: use diameter.
                Parameter diamParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                if (diamParam != null && diamParam.HasValue)
                    return diamParam.AsDouble();

                // Rectangular duct: use width.
                Parameter widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                if (widthParam != null && widthParam.HasValue)
                    return widthParam.AsDouble();
            }
            catch (Exception) { }
            return -1;
        }

        /// <summary>
        /// How wide the element is across its run, in feet. The placement engine adds half of this to
        /// every tag offset, so a tag's gap is measured from the element's EDGE - "150 mm from its
        /// edge" for a tag with no leader - instead of from its centre.
        /// </summary>
        /// <remarks>
        /// - Duct: exactly the value it always had (GetDuctWidth).
        /// - Pipe and cable tray: their own size, read the same way - a pipe's outside diameter, a
        ///   tray's width. Parameters rather than the view box, because a run lying at an angle in the
        ///   view has a box far wider than the run itself.
        /// - Duct and pipe accessories: the view box measured ACROSS the run they sit in, the run read
        ///   from their two end connectors. Across is where a no-leader tag ends up - along the run it
        ///   would sit on the accessory's own duct or pipe.
        /// - Equipment, and an accessory whose run is seen end-on (a riser in plan): the narrower side
        ///   of the view box. The tag then sits the full gap off the narrow sides, and on the long
        ///   sides a position that would land on the element itself is rejected.
        /// Anything that cannot be read gives 0, which measures from the centre as before.
        /// </remarks>
        /// <param name="linkTransform">
        /// For an element inside a linked model, the link-to-host transform; null for this model's own
        /// elements. Only the accessory run direction needs it - parameters read the same either way.
        /// </param>
        internal static double GetHostWidth(
            Element elem,
            BuiltInCategory category,
            View view,
            BoundingBoxXYZ viewBox,
            Transform linkTransform = null)
        {
            switch (category)
            {
                case BuiltInCategory.OST_DuctCurves:
                    return elem is Duct duct ? GetDuctWidth(duct) : 0.0;

                case BuiltInCategory.OST_PipeCurves:
                    return ReadFirstLengthFeet(
                        elem,
                        BuiltInParameter.RBS_PIPE_OUTER_DIAMETER,
                        BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);

                case BuiltInCategory.OST_CableTray:
                    return ReadFirstLengthFeet(elem, BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);

                default:
                    return GetViewBoxHostWidth(elem, category, view, viewBox, linkTransform);
            }
        }

        /// <summary>
        /// The first of the given length parameters that holds a positive value, in feet; 0 if none.
        /// </summary>
        internal static double ReadFirstLengthFeet(Element element, params BuiltInParameter[] parameters)
        {
            foreach (BuiltInParameter parameter in parameters)
            {
                try
                {
                    Parameter found = element.get_Parameter(parameter);
                    if (found == null || !found.HasValue || found.StorageType != StorageType.Double)
                        continue;

                    double value = found.AsDouble();
                    if (value > 0.0)
                        return value;
                }
                catch (Exception)
                {
                    // Try the next parameter.
                }
            }

            return 0.0;
        }

        /// <summary>
        /// HostWidth for an accessory or a piece of equipment, measured from its box in the view -
        /// see GetHostWidth for which side is used and why.
        /// </summary>
        internal static double GetViewBoxHostWidth(
            Element elem, BuiltInCategory category, View view, BoundingBoxXYZ viewBox, Transform linkTransform)
        {
            try
            {
                XYZ viewRight = TagViewGeometry.GetViewRight(view);
                XYZ viewUp = TagViewGeometry.GetViewUp(view);
                AnnotationBox box = TagViewGeometry.ToViewBox(viewBox, viewRight, viewUp);
                if (box == null)
                    return 0.0;

                double sizeAlongRight = box.MaxX - box.MinX;
                double sizeAlongUp = box.MaxY - box.MinY;

                XYZ run;
                if ((category == BuiltInCategory.OST_DuctAccessory || category == BuiltInCategory.OST_PipeAccessory)
                    && TryGetInlineRunDirection(elem, out run))
                {
                    // A linked accessory's connectors are in the link's coordinates; turn the run into
                    // this model's before comparing it with the view.
                    if (linkTransform != null)
                        run = linkTransform.OfVector(run);

                    // A run lying left-right in the view is crossed up-down, and the other way round.
                    if (!IsSeenEndOn(run, view))
                    {
                        return Math.Abs(run.DotProduct(viewRight)) >= Math.Abs(run.DotProduct(viewUp))
                            ? sizeAlongUp
                            : sizeAlongRight;
                    }
                }

                return Math.Min(sizeAlongRight, sizeAlongUp);
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        /// <summary>
        /// The direction of the run an inline fitting sits in, from its two end connectors. False when
        /// it does not have exactly two (a three-way valve has no single run to measure across).
        /// </summary>
        internal static bool TryGetInlineRunDirection(Element elem, out XYZ direction)
        {
            direction = null;

            try
            {
                ConnectorManager manager = (elem as FamilyInstance)?.MEPModel?.ConnectorManager;
                if (manager == null)
                    return false;

                var ends = new List<XYZ>();
                foreach (Connector connector in manager.Connectors)
                {
                    if (connector == null || !connector.IsValidObject || connector.ConnectorType != ConnectorType.End)
                        continue;

                    ends.Add(connector.Origin);
                }

                if (ends.Count != 2 || ends[0] == null || ends[1] == null)
                    return false;

                XYZ between = ends[1] - ends[0];
                if (between.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                    return false;

                direction = between.Normalize();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True when a run points mostly into or out of the view - the same 0.7 test
        /// GetElementOrientation uses for a riser.
        /// </summary>
        internal static bool IsSeenEndOn(XYZ runDirection, View view)
        {
            XYZ viewNormal = view?.ViewDirection;
            if (runDirection == null || viewNormal == null || viewNormal.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                return false;

            return Math.Abs(runDirection.DotProduct(viewNormal.Normalize())) > 0.7;
        }

        internal static Outline TryCreateOutline(BoundingBoxXYZ bbox)
        {
            if (bbox == null || bbox.Min == null || bbox.Max == null)
                return null;

            try
            {
                XYZ[] corners = GetBoundingBoxCorners(bbox);
                if (corners == null || corners.Length == 0)
                    return null;

                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double minZ = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;
                double maxZ = double.MinValue;

                foreach (XYZ corner in corners)
                {
                    if (corner == null)
                        continue;

                    minX = Math.Min(minX, corner.X);
                    minY = Math.Min(minY, corner.Y);
                    minZ = Math.Min(minZ, corner.Z);
                    maxX = Math.Max(maxX, corner.X);
                    maxY = Math.Max(maxY, corner.Y);
                    maxZ = Math.Max(maxZ, corner.Z);
                }

                if (minX > maxX || minY > maxY || minZ > maxZ)
                    return null;

                return new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static XYZ[] GetBoundingBoxCorners(BoundingBoxXYZ bbox)
        {
            if (bbox == null || bbox.Min == null || bbox.Max == null)
                return null;

            Transform transform = bbox.Transform ?? Transform.Identity;
            XYZ min = bbox.Min;
            XYZ max = bbox.Max;

            var corners = new[]
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

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = transform.OfPoint(corners[i]);
            }

            return corners;
        }

        internal static XYZ GetBoundingBoxCenter(BoundingBoxXYZ bbox)
        {
            Outline outline = TryCreateOutline(bbox);
            if (outline == null)
                return null;

            XYZ min = outline.MinimumPoint;
            XYZ max = outline.MaximumPoint;
            return (min + max) * 0.5;
        }

#if DEBUG
        internal static readonly object GeometryCheckSync = new object();
        internal static bool _geometryChecksCompleted;
#endif

#if DEBUG
        internal static void RunGeometryRegressionChecksOnce()
        {
            lock (GeometryCheckSync)
            {
                if (_geometryChecksCompleted)
                    return;

                _geometryChecksCompleted = true;
            }

            string error;
            if (!TryRunGeometryRegressionChecks(out error))
            {
                SmartTagTelemetryTracker.RecordDiagnostic("Geometry regression check failed: " + error);
                return;
            }

            SmartTagTelemetryTracker.RecordDiagnostic("Geometry regression check passed.");
        }

        internal static bool TryRunGeometryRegressionChecks(out string error)
        {
            error = null;
            const double tol = 1e-6;

            try
            {
                // Case 1: pure translation.
                var translated = new BoundingBoxXYZ
                {
                    Min = new XYZ(0, 0, 0),
                    Max = new XYZ(2, 4, 0),
                    Transform = Transform.CreateTranslation(new XYZ(10, -3, 5))
                };

                Outline translatedOutline = TryCreateOutline(translated);
                if (translatedOutline == null)
                {
                    error = "Translated bounding box outline is null.";
                    return false;
                }

                if (!NearlyEqual(translatedOutline.MinimumPoint, new XYZ(10, -3, 5), tol)
                    || !NearlyEqual(translatedOutline.MaximumPoint, new XYZ(12, 1, 5), tol))
                {
                    error = "Translated bounding box outline did not match expected extents.";
                    return false;
                }

                // Case 2: 90° rotation about Z at origin.
                var rotated = new BoundingBoxXYZ
                {
                    Min = new XYZ(0, 0, 0),
                    Max = new XYZ(2, 1, 0),
                    Transform = Transform.CreateRotationAtPoint(XYZ.BasisZ, 0.5 * Math.PI, XYZ.Zero)
                };

                Outline rotatedOutline = TryCreateOutline(rotated);
                XYZ rotatedCenter = GetBoundingBoxCenter(rotated);
                if (rotatedOutline == null || rotatedCenter == null)
                {
                    error = "Rotated bounding box outline or center is null.";
                    return false;
                }

                if (!NearlyEqual(rotatedOutline.MinimumPoint, new XYZ(-1, 0, 0), tol)
                    || !NearlyEqual(rotatedOutline.MaximumPoint, new XYZ(0, 2, 0), tol))
                {
                    error = "Rotated bounding box outline did not match expected extents.";
                    return false;
                }

                if (!NearlyEqual(rotatedCenter, new XYZ(-0.5, 1, 0), tol))
                {
                    error = "Rotated bounding box center did not match expected position.";
                    return false;
                }

                string engineError;
                if (!SmartTagViewPlane.TryRunGeometryRegressionChecks(out engineError))
                {
                    error = "Placement engine check failed: " + engineError;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool NearlyEqual(XYZ a, XYZ b, double tolerance)
        {
            if (a == null || b == null)
                return false;

            return Math.Abs(a.X - b.X) <= tolerance
                && Math.Abs(a.Y - b.Y) <= tolerance
                && Math.Abs(a.Z - b.Z) <= tolerance;
        }
#endif
    }
}
