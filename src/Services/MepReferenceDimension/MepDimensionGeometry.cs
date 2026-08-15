#region Metadata
/*
 * Tool Name     : Auto MEP Dimension
 * File Name     : MepDimensionGeometry.cs
 * Purpose       : All geometry for the Auto MEP Dimension workflow - building the measuring axis from a
 *                 run, deciding cheaply whether an element can reach that axis, and turning walls,
 *                 columns, beams, floors, grids, levels and other runs into ordered dimension
 *                 references, in the open project or inside a Revit link.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (Constants), AJTools.Models.Dimensioning,
 *                 AJTools.Services.Dimensioning (DimensionSource)
 *
 * Input         : A view, a DimensionSource (host or link), elements, and a DimensionAxis.
 * Output        : DimensionReferenceCandidate objects carrying host-space references.
 *
 * Notes         :
 * Generalised from DuctReferenceDimensionGeometry, which was already ~95% category-agnostic. Beyond
 * supporting every service category, links, grids and levels, these real defects were fixed:
 * - Coarse views produced nothing. The old code asked for view geometry, got a valid GeometryElement
 *   containing only lines (Revit draws MEP as single lines in Coarse), saw it was non-null and never
 *   ran its model-geometry fallback. Now the fallback triggers on "no usable solid", not "null".
 * - Sort position came from PlanarFace.Origin, which is the plane's parametric origin and can sit
 *   outside the face. On a face even slightly off-parallel that mis-positions the reference. The
 *   crossing point is now taken from the tessellated edge point nearest the measuring line.
 * - One failed edge threw away every point already tessellated for that face, collapsing the face to
 *   a single point and wrongly rejecting long walls. Failures are now per-edge.
 * - The stable-key fallback returned the element id, so every face on one element shared a key and
 *   de-duplication silently discarded all but one - the run then failed with a misleading message.
 *   A key that cannot be built is now empty and the candidate is dropped honestly.
 * - The search distance was read from view.CropBox even when the crop was off, where Revit returns a
 *   stale extent, so genuine references were culled. It is now gated on CropBoxActive.
 * - Round pipes, conduit and round ducts have no planar side faces at all, so the old face-only path
 *   found nothing. Round runs now fall back to their centreline reference.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release, replacing DuctReferenceDimensionGeometry.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.Dimensioning;
using AJTools.Services.Dimensioning;
using AJTools.Utils;

namespace AJTools.Services.MepReferenceDimension
{
    /// <summary>
    /// Geometry and reference extraction for the Auto MEP Dimension workflow.
    /// </summary>
    internal static class MepDimensionGeometry
    {
        /// <summary>1 mm - two coordinates closer than this are treated as the same position.</summary>
        internal const double CoordinateMergeTolerance = 1.0 * Constants.MM_TO_FEET;

        /// <summary>Fallback search reach when the view gives no usable extent, in feet.</summary>
        private const double DefaultSearchHalfLengthFeet = 250.0;

        /// <summary>Never search less than this, in feet.</summary>
        private const double MinimumSearchHalfLengthFeet = 25.0;

        /// <summary>Extra reach past the crop extent, in feet.</summary>
        private const double SearchPaddingFeet = 10.0;

        /// <summary>|cos| threshold for "these vectors point the same way" - about 8 degrees.</summary>
        private const double FaceNormalAlignmentTolerance = 0.01;

        /// <summary>Direction comparison tolerance for run-vs-axis alignment.</summary>
        internal const double DirectionAlignmentTolerance = 0.02;

        private const double NormalizeTolerance = 1e-9;

        // -----------------------------------------------------------------------------------------
        // Axis
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Builds the measuring axis from a linear run: origin at the run's midpoint, measuring across
        /// the run, within the view plane. <paramref name="toHost"/> converts link geometry into host
        /// space and is Transform.Identity for elements in the open project.
        /// </summary>
        internal static bool TryCreateAxis(
            View view,
            Element run,
            Transform toHost,
            double axisBandTolerance,
            out DimensionAxis axis,
            out string reason)
        {
            axis = null;
            reason = string.Empty;

            if (view == null)
            {
                reason = "No active view.";
                return false;
            }

            if (run == null)
            {
                reason = "No element was selected.";
                return false;
            }

            Curve curve = (run.Location as LocationCurve)?.Curve;
            if (curve == null)
            {
                reason = "That element has no straight run to measure from.";
                return false;
            }

            if (!TryGetCurveDirection(curve, out XYZ rawDirection))
            {
                reason = "The run's direction could not be worked out.";
                return false;
            }

            if (!TryGetCurveMidPoint(curve, out XYZ rawMidPoint))
            {
                reason = "The run's midpoint could not be worked out.";
                return false;
            }

            Transform transform = toHost ?? Transform.Identity;
            XYZ runDirection = TransformVector(transform, rawDirection);
            XYZ midPoint = TransformPoint(transform, rawMidPoint);

            if (runDirection == null || midPoint == null)
            {
                reason = "The run could not be placed in the current model's coordinates.";
                return false;
            }

            XYZ viewNormal = view.ViewDirection;
            if (viewNormal == null || viewNormal.GetLength() <= NormalizeTolerance)
                viewNormal = XYZ.BasisZ;
            viewNormal = viewNormal.Normalize();

            XYZ runOnView = ProjectVectorToPlane(runDirection, viewNormal);
            if (runOnView == null || runOnView.GetLength() <= NormalizeTolerance)
            {
                reason = "The run points straight into the view, so it has no length to measure from.";
                return false;
            }
            runOnView = runOnView.Normalize();

            XYZ dimensionDirection = viewNormal.CrossProduct(runOnView);
            if (dimensionDirection == null || dimensionDirection.GetLength() <= NormalizeTolerance)
            {
                reason = "A measuring direction across the run could not be built.";
                return false;
            }
            dimensionDirection = dimensionDirection.Normalize();

            XYZ origin = ProjectPointToViewPlane(view, midPoint, viewNormal);

            axis = new DimensionAxis
            {
                Origin = origin,
                DimensionDirection = dimensionDirection,
                RunDirection = runOnView,
                ViewNormal = viewNormal,
                OriginDimensionCoord = origin.DotProduct(dimensionDirection),
                OriginRunCoord = origin.DotProduct(runOnView),
                AxisBandTolerance = axisBandTolerance
            };

            axis.SearchHalfLength = ResolveSearchHalfLength(view, axis);
            return true;
        }

        /// <summary>
        /// Cheap broad-phase test: could this element's bounding box possibly reach the measuring axis?
        /// Fails open (returns true) when the box cannot be read, so nothing is lost silently.
        /// </summary>
        internal static bool MayIntersectAxisBand(Element element, View view, DimensionAxis axis, Transform toHost)
        {
            if (element == null || axis == null)
                return false;

            BoundingBoxXYZ box = null;
            try
            {
                // A host view cannot scope geometry owned by a link document.
                box = toHost == null || toHost.IsIdentity
                    ? (element.get_BoundingBox(view) ?? element.get_BoundingBox(null))
                    : element.get_BoundingBox(null);
            }
            catch
            {
                box = null;
            }

            if (box == null)
                return true;

            IList<XYZ> corners = GetBoundingBoxCorners(box, toHost);
            if (corners.Count == 0)
                return true;

            double minRun = corners.Min(p => p.DotProduct(axis.RunDirection));
            double maxRun = corners.Max(p => p.DotProduct(axis.RunDirection));
            if (axis.OriginRunCoord < minRun - axis.AxisBandTolerance ||
                axis.OriginRunCoord > maxRun + axis.AxisBandTolerance)
            {
                return false;
            }

            double minDim = corners.Min(p => p.DotProduct(axis.DimensionDirection));
            double maxDim = corners.Max(p => p.DotProduct(axis.DimensionDirection));

            return IntervalsOverlap(
                minDim,
                maxDim,
                axis.OriginDimensionCoord - axis.SearchHalfLength,
                axis.OriginDimensionCoord + axis.SearchHalfLength,
                axis.AxisBandTolerance);
        }

        // -----------------------------------------------------------------------------------------
        // Candidate collection
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Every usable reference on a solid element (wall, column, beam, floor, rectangular run):
        /// the faces whose normal runs along the measuring direction and which actually cross it.
        /// </summary>
        internal static IList<DimensionReferenceCandidate> CollectFaceCandidates(
            DimensionSource source,
            View view,
            Element element,
            DimensionAxis axis,
            DimensionTargetKind kind,
            bool isMeasuredRun,
            string seedElementKey)
        {
            List<DimensionReferenceCandidate> candidates = new List<DimensionReferenceCandidate>();
            if (source == null || element == null || axis == null)
                return candidates;

            GeometryElement geometry = GetGeometryWithReferences(element, source.IsLinked ? null : view);
            if (geometry == null)
                return candidates;

            CollectFaceCandidatesRecursive(
                geometry,
                source.ToHost ?? Transform.Identity,
                source,
                element,
                axis,
                kind,
                isMeasuredRun,
                seedElementKey,
                candidates);

            return candidates;
        }

        /// <summary>
        /// The centreline reference for a run. This is how round pipes, conduit and round ducts get
        /// dimensioned at all - they have no flat side faces to grab.
        /// </summary>
        internal static DimensionReferenceCandidate CollectCenterlineCandidate(
            DimensionSource source,
            Element element,
            DimensionAxis axis,
            DimensionTargetKind kind,
            bool isMeasuredRun,
            string seedElementKey)
        {
            if (source == null || element == null || axis == null)
                return null;

            LocationCurve locationCurve = element.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null)
                return null;

            Reference linkLocalReference = null;
            try
            {
                // Only usable when the location curve carries a real geometric reference; on most
                // MEP curves it does not, and the geometry pass below is what actually finds one.
                if (curve is Line)
                    linkLocalReference = curve.Reference;
            }
            catch
            {
                linkLocalReference = null;
            }

            if (linkLocalReference == null)
                linkLocalReference = FindCenterlineReferenceInGeometry(element, curve);

            if (linkLocalReference == null)
            {
                // Deliberately NOT falling back to new Reference(element): NewDimension accepts a
                // whole-element reference for a datum, but for a pipe or duct it throws "the references
                // are not geometric references" - and in a single-string chain that one bad reference
                // takes down every good reference sharing the array. Dropping the candidate gives the
                // caller's honest "no usable side faces or centreline" message instead.
                return null;
            }

            if (!TryGetCurveMidPoint(curve, out XYZ rawMid))
                return null;

            Transform transform = source.ToHost ?? Transform.Identity;
            XYZ midPoint = TransformPoint(transform, rawMid);
            if (midPoint == null)
                return null;

            double runCoord = midPoint.DotProduct(axis.RunDirection);
            if (Math.Abs(runCoord - axis.OriginRunCoord) > axis.SearchHalfLength)
                return null;

            double sortCoord = midPoint.DotProduct(axis.DimensionDirection);
            if (Math.Abs(sortCoord - axis.OriginDimensionCoord) > axis.SearchHalfLength)
                return null;

            return BuildCandidate(
                source,
                element,
                linkLocalReference,
                sortCoord,
                0.0,
                kind,
                isMeasuredRun,
                seedElementKey);
        }

        /// <summary>
        /// The reference for a grid or level. Datums carry no solid geometry, so they are referenced
        /// as whole elements and positioned from their curve or elevation.
        /// </summary>
        /// <remarks>
        /// A datum is only a usable end for this measurement when it runs SQUARE to the measuring
        /// direction - then every point on it projects to the same coordinate and the single
        /// representative point is exact. A grid running across the measurement projects to the middle
        /// of its own drawn length, a number with no relation to the run, which then routinely beats
        /// the real wall as "nearest" and makes Revit reject the whole chain. A level has the same
        /// problem in plan, where its elevation is multiplied out entirely.
        /// </remarks>
        internal static DimensionReferenceCandidate CollectDatumCandidate(
            DimensionSource source,
            View view,
            Element datum,
            DimensionAxis axis,
            DimensionTargetKind kind,
            string seedElementKey)
        {
            if (source == null || datum == null || axis == null)
                return null;

            Transform toHost = source.ToHost ?? Transform.Identity;

            if (!IsDatumUsableForAxis(datum, view, toHost, axis))
                return null;

            if (!TryGetDatumPoint(datum, view, toHost, out XYZ point))
                return null;

            // The datum must also reach the run's station along the run, the same test the centreline
            // path applies - otherwise a grid far up the building still counts as in range.
            double runCoord = point.DotProduct(axis.RunDirection);
            if (datum is Grid && Math.Abs(runCoord - axis.OriginRunCoord) > axis.SearchHalfLength)
                return null;

            double sortCoord = point.DotProduct(axis.DimensionDirection);
            if (Math.Abs(sortCoord - axis.OriginDimensionCoord) > axis.SearchHalfLength)
                return null;

            Reference reference;
            try
            {
                reference = new Reference(datum);
            }
            catch
            {
                return null;
            }

            return BuildCandidate(
                source,
                datum,
                reference,
                sortCoord,
                0.0,
                kind,
                false,
                seedElementKey);
        }

        /// <summary>
        /// True when this datum runs square to the measuring direction, so a single point on it
        /// describes its position exactly. Levels are usable only when the measurement is vertical
        /// (a section or elevation); grids only when they run parallel to the measured run.
        /// </summary>
        private static bool IsDatumUsableForAxis(Element datum, View view, Transform toHost, DimensionAxis axis)
        {
            if (datum is Level)
            {
                // In a plan the measuring direction is horizontal, so a level's elevation drops out of
                // the projection entirely and every level lands on the same meaningless coordinate.
                return Math.Abs(axis.DimensionDirection.DotProduct(XYZ.BasisZ)) >= 1.0 - DirectionAlignmentTolerance;
            }

            if (!(datum is Grid grid))
                return false;

            Curve curve = TryGetGridCurve(grid, view);
            if (curve == null)
                return false;

            if (!TryGetCurveDirection(curve, out XYZ direction))
                return false;

            XYZ hostDirection = TransformVector(toHost, direction);
            XYZ projected = ProjectVectorToPlane(hostDirection, axis.ViewNormal);
            if (projected == null || projected.GetLength() <= NormalizeTolerance)
                return false;

            return AreParallel(projected, axis.RunDirection, DirectionAlignmentTolerance);
        }

        /// <summary>
        /// A grid's curve as drawn in this view where that is available, falling back to its model curve.
        /// </summary>
        private static Curve TryGetGridCurve(Grid grid, View view)
        {
            if (view != null)
            {
                try
                {
                    IList<Curve> curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                    Curve inView = curves?.FirstOrDefault();
                    if (inView != null)
                        return inView;
                }
                catch
                {
                    // Falls through to the model curve.
                }
            }

            try
            {
                return grid.Curve;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the run's centreline reference by asking for geometry WITH non-visible objects, which
        /// is what surfaces an MEP centreline at all - it is not part of the visible solid geometry.
        /// This is the route QuickParallelDimensionService already uses successfully.
        /// </summary>
        private static Reference FindCenterlineReferenceInGeometry(Element element, Curve locationCurve)
        {
            if (element == null || locationCurve == null)
                return null;

            if (!TryGetCurveDirection(locationCurve, out XYZ runDirection))
                return null;

            GeometryElement geometry;
            try
            {
                geometry = element.get_Geometry(new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true,
                    DetailLevel = ViewDetailLevel.Fine
                });
            }
            catch
            {
                return null;
            }

            if (geometry == null)
                return null;

            if (!TryGetCurveMidPoint(locationCurve, out XYZ anchor))
                anchor = null;

            Reference best = null;
            double bestDistance = double.MaxValue;

            CollectCenterlineReferences(geometry, runDirection, anchor, ref best, ref bestDistance);
            return best;
        }

        private static void CollectCenterlineReferences(
            GeometryElement geometry,
            XYZ runDirection,
            XYZ anchor,
            ref Reference best,
            ref double bestDistance)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is GeometryInstance instance)
                {
                    GeometryElement nested = null;
                    try
                    {
                        nested = instance.GetInstanceGeometry();
                    }
                    catch
                    {
                        nested = null;
                    }

                    if (nested != null)
                        CollectCenterlineReferences(nested, runDirection, anchor, ref best, ref bestDistance);

                    continue;
                }

                if (!(obj is Curve curve))
                    continue;

                Reference reference;
                try
                {
                    reference = curve.Reference;
                }
                catch
                {
                    continue;
                }

                if (reference == null)
                    continue;

                if (!TryGetCurveDirection(curve, out XYZ direction))
                    continue;

                if (!AreParallel(direction, runDirection, DirectionAlignmentTolerance))
                    continue;

                double distance = 0.0;
                if (anchor != null && TryGetCurveMidPoint(curve, out XYZ mid))
                    distance = mid.DistanceTo(anchor);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = reference;
                }
            }
        }

        /// <summary>
        /// A representative point on a grid or level, in host coordinates. For a grid this is the
        /// midpoint of its curve; for a level it is a point at its elevation.
        /// </summary>
        internal static bool TryGetDatumPoint(Element datum, View view, Transform toHost, out XYZ point)
        {
            point = null;

            Level level = datum as Level;
            if (level != null)
            {
                try
                {
                    point = TransformPoint(toHost, new XYZ(0.0, 0.0, level.ProjectElevation));
                }
                catch
                {
                    point = null;
                }

                return point != null;
            }

            Grid grid = datum as Grid;
            if (grid == null)
                return false;

            Curve curve = null;

            if (view != null)
            {
                try
                {
                    IList<Curve> curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                    curve = curves?.FirstOrDefault();
                }
                catch
                {
                    curve = null;
                }
            }

            if (curve == null)
            {
                try
                {
                    curve = grid.Curve;
                }
                catch
                {
                    curve = null;
                }
            }

            if (curve == null)
                return false;

            if (!TryGetCurveMidPoint(curve, out XYZ rawMid))
                return false;

            point = TransformPoint(toHost, rawMid);
            return point != null;
        }

        // -----------------------------------------------------------------------------------------
        // Dimension line
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Places the dimension line across the plan's references, on the line through the run's midpoint.
        /// </summary>
        /// <remarks>
        /// The line spans exactly the outermost references, with only a hair of slack so it can never be
        /// degenerate. There is deliberately no user "overhang" here: NewDimension reads this line for
        /// POSITION and DIRECTION only, then draws the dimension between the references itself. How far
        /// the line runs on past the last witness line is a property of the Dimension Type in Revit
        /// ("witness line extension"), so extending it from code changes nothing on the sheet - and
        /// padding each piece of a separate-segments chain made adjacent pieces overlap each other.
        /// </remarks>
        internal static bool TryCreateDimensionLine(DimensionPlan plan, double rowOffsetFeet, out string reason)
        {
            reason = string.Empty;

            if (plan?.Axis == null || plan.References == null || plan.References.Count < 2)
            {
                reason = "Fewer than two references were found.";
                return false;
            }

            double minCoord = plan.References.Min(r => r.SortCoord);
            double maxCoord = plan.References.Max(r => r.SortCoord);

            // Just enough to guarantee Line.CreateBound never sees a zero-length line.
            double slack = Constants.MIN_DISTANCE_TOLERANCE;

            // Row-per-run mode slides each row along the run so they stack instead of overprinting.
            XYZ origin = plan.Axis.Origin + (plan.Axis.RunDirection * rowOffsetFeet);

            XYZ start = origin +
                        (plan.Axis.DimensionDirection * (minCoord - plan.Axis.OriginDimensionCoord - slack));
            XYZ end = origin +
                      (plan.Axis.DimensionDirection * (maxCoord - plan.Axis.OriginDimensionCoord + slack));

            if (!DimensionFactory.TryCreateLine(start, end, out Line line, out reason))
                return false;

            plan.DimensionLine = line;
            return true;
        }

        // -----------------------------------------------------------------------------------------
        // Shared maths
        // -----------------------------------------------------------------------------------------

        /// <summary>True when two vectors are parallel or anti-parallel within tolerance.</summary>
        internal static bool AreParallel(XYZ a, XYZ b, double tolerance)
        {
            if (a == null || b == null)
                return false;

            if (a.GetLength() <= NormalizeTolerance || b.GetLength() <= NormalizeTolerance)
                return false;

            return Math.Abs(a.Normalize().DotProduct(b.Normalize())) >= 1.0 - tolerance;
        }

        internal static bool IntervalsOverlap(double minA, double maxA, double minB, double maxB, double tolerance)
        {
            return maxA >= minB - tolerance && maxB >= minA - tolerance;
        }

        /// <summary>Reads a bounded line's extent along the axis - used to fingerprint existing dimensions.</summary>
        internal static bool TryGetLineIntervalAlongAxis(
            Curve curve,
            DimensionAxis axis,
            out double minCoord,
            out double maxCoord,
            out double runCoord,
            out XYZ direction)
        {
            minCoord = 0.0;
            maxCoord = 0.0;
            runCoord = 0.0;
            direction = null;

            Line line = curve as Line;
            if (line == null || axis == null || !line.IsBound)
                return false;

            try
            {
                XYZ start = line.GetEndPoint(0);
                XYZ end = line.GetEndPoint(1);

                XYZ raw = end - start;
                direction = ProjectVectorToPlane(raw, axis.ViewNormal);
                if (direction == null || direction.GetLength() <= NormalizeTolerance)
                    return false;

                direction = direction.Normalize();

                double a = start.DotProduct(axis.DimensionDirection);
                double b = end.DotProduct(axis.DimensionDirection);
                minCoord = Math.Min(a, b);
                maxCoord = Math.Max(a, b);
                runCoord = ((start + end) * 0.5).DotProduct(axis.RunDirection);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static XYZ ProjectVectorToPlane(XYZ vector, XYZ planeNormal)
        {
            if (vector == null || planeNormal == null)
                return null;

            if (planeNormal.GetLength() <= NormalizeTolerance)
                return vector;

            XYZ normal = planeNormal.Normalize();
            return vector - (normal * vector.DotProduct(normal));
        }

        internal static bool TryGetCurveDirection(Curve curve, out XYZ direction)
        {
            direction = null;
            if (curve == null)
                return false;

            try
            {
                XYZ tangent = curve.ComputeDerivatives(0.5, true)?.BasisX;
                if (tangent != null && tangent.GetLength() > NormalizeTolerance)
                {
                    direction = tangent.Normalize();
                    return true;
                }
            }
            catch
            {
                // Fall through to the endpoint route.
            }

            if (!curve.IsBound)
                return false;

            try
            {
                XYZ vector = curve.GetEndPoint(1) - curve.GetEndPoint(0);
                if (vector.GetLength() <= NormalizeTolerance)
                    return false;

                direction = vector.Normalize();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The curve's midpoint. Unlike the original, total failure reports false rather than silently
        /// returning the project origin, which used to place the whole axis at 0,0,0 while reporting success.
        /// </summary>
        internal static bool TryGetCurveMidPoint(Curve curve, out XYZ midPoint)
        {
            midPoint = null;
            if (curve == null)
                return false;

            try
            {
                midPoint = curve.Evaluate(0.5, true);
                if (midPoint != null)
                    return true;
            }
            catch
            {
                midPoint = null;
            }

            if (!curve.IsBound)
                return false;

            try
            {
                midPoint = (curve.GetEndPoint(0) + curve.GetEndPoint(1)) * 0.5;
                return midPoint != null;
            }
            catch
            {
                midPoint = null;
                return false;
            }
        }

        // -----------------------------------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------------------------------

        private static void CollectFaceCandidatesRecursive(
            GeometryElement geometry,
            Transform transform,
            DimensionSource source,
            Element element,
            DimensionAxis axis,
            DimensionTargetKind kind,
            bool isMeasuredRun,
            string seedElementKey,
            IList<DimensionReferenceCandidate> candidates)
        {
            if (geometry == null)
                return;

            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid)
                {
                    CollectSolidFaceCandidates(
                        solid, transform, source, element, axis, kind, isMeasuredRun, seedElementKey, candidates);
                    continue;
                }

                if (!(obj is GeometryInstance instance))
                    continue;

                Transform nextTransform = transform;
                try
                {
                    if (instance.Transform != null)
                        nextTransform = transform.Multiply(instance.Transform);
                }
                catch
                {
                    nextTransform = transform;
                }

                GeometryElement symbolGeometry = null;
                try
                {
                    symbolGeometry = instance.GetSymbolGeometry();
                }
                catch
                {
                    symbolGeometry = null;
                }

                if (symbolGeometry != null)
                {
                    CollectFaceCandidatesRecursive(
                        symbolGeometry, nextTransform, source, element, axis, kind,
                        isMeasuredRun, seedElementKey, candidates);
                    continue;
                }

                GeometryElement instanceGeometry = null;
                try
                {
                    instanceGeometry = instance.GetInstanceGeometry();
                }
                catch
                {
                    instanceGeometry = null;
                }

                if (instanceGeometry != null)
                {
                    // GetInstanceGeometry is already positioned, so the instance transform is not reapplied.
                    CollectFaceCandidatesRecursive(
                        instanceGeometry, transform, source, element, axis, kind,
                        isMeasuredRun, seedElementKey, candidates);
                }
            }
        }

        private static void CollectSolidFaceCandidates(
            Solid solid,
            Transform transform,
            DimensionSource source,
            Element element,
            DimensionAxis axis,
            DimensionTargetKind kind,
            bool isMeasuredRun,
            string seedElementKey,
            IList<DimensionReferenceCandidate> candidates)
        {
            if (solid?.Faces == null || solid.Faces.Size == 0)
                return;

            foreach (Face face in solid.Faces)
            {
                if (!(face is PlanarFace planarFace) || planarFace.Reference == null)
                    continue;

                DimensionReferenceCandidate candidate = TryCreateCandidateFromFace(
                    planarFace, transform, source, element, axis, kind, isMeasuredRun, seedElementKey);

                if (candidate != null)
                    candidates.Add(candidate);
            }
        }

        private static DimensionReferenceCandidate TryCreateCandidateFromFace(
            PlanarFace face,
            Transform transform,
            DimensionSource source,
            Element element,
            DimensionAxis axis,
            DimensionTargetKind kind,
            bool isMeasuredRun,
            string seedElementKey)
        {
            XYZ faceNormal = TransformVector(transform, face.FaceNormal);
            XYZ normalOnView = ProjectVectorToPlane(faceNormal, axis.ViewNormal);
            if (normalOnView == null || normalOnView.GetLength() <= NormalizeTolerance)
                return null;

            normalOnView = normalOnView.Normalize();
            if (!AreParallel(normalOnView, axis.DimensionDirection, FaceNormalAlignmentTolerance))
                return null;

            IList<XYZ> points = GetPlanarFacePoints(face, transform);
            if (points.Count == 0)
                return null;

            double minRunCoord = points.Min(p => p.DotProduct(axis.RunDirection));
            double maxRunCoord = points.Max(p => p.DotProduct(axis.RunDirection));
            if (axis.OriginRunCoord < minRunCoord - axis.AxisBandTolerance ||
                axis.OriginRunCoord > maxRunCoord + axis.AxisBandTolerance)
            {
                return null;
            }

            // Take the crossing position from the tessellated point closest to the measuring line,
            // not from face.Origin - the plane's parametric origin can sit outside the face entirely.
            XYZ crossingPoint = points
                .OrderBy(p => Math.Abs(p.DotProduct(axis.RunDirection) - axis.OriginRunCoord))
                .First();

            double sortCoord = crossingPoint.DotProduct(axis.DimensionDirection);
            if (Math.Abs(sortCoord - axis.OriginDimensionCoord) > axis.SearchHalfLength)
                return null;

            double axisOffset = 0.0;
            if (axis.OriginRunCoord < minRunCoord)
                axisOffset = minRunCoord - axis.OriginRunCoord;
            else if (axis.OriginRunCoord > maxRunCoord)
                axisOffset = axis.OriginRunCoord - maxRunCoord;

            return BuildCandidate(
                source, element, face.Reference, sortCoord, axisOffset, kind, isMeasuredRun, seedElementKey);
        }

        /// <summary>
        /// Turns a source-local reference into a candidate carrying a host-space reference and a stable
        /// key built against the host document. Returns null when the reference cannot cross into the
        /// host - which is normal for some linked element kinds and is reported, not hidden.
        /// </summary>
        private static DimensionReferenceCandidate BuildCandidate(
            DimensionSource source,
            Element element,
            Reference sourceReference,
            double sortCoord,
            double axisOffset,
            DimensionTargetKind kind,
            bool isMeasuredRun,
            string seedElementKey)
        {
            if (source == null || element == null || sourceReference == null)
                return null;

            Reference hostReference = source.ToHostReference(sourceReference);
            if (hostReference == null)
                return null;

            string stableKey = DimensionSource.GetStableKey(source.HostDocument, hostReference);
            if (string.IsNullOrWhiteSpace(stableKey))
                return null;

            string elementKey = source.BuildKey(element.Id);

            return new DimensionReferenceCandidate
            {
                ElementKey = elementKey,
                ElementId = element.Id,
                HostReference = hostReference,
                StableKey = stableKey,
                SortCoord = sortCoord,
                AxisOffset = axisOffset,
                Kind = kind,
                IsMeasuredRun = isMeasuredRun,
                IsSeedRun = !string.IsNullOrEmpty(seedElementKey) &&
                            string.Equals(elementKey, seedElementKey, StringComparison.Ordinal),
                SourceName = source.DisplayName
            };
        }

        /// <summary>
        /// Geometry with references. When <paramref name="view"/> is supplied the view's own detail
        /// level is used; a Coarse view draws MEP as single lines with no solids, so the model-geometry
        /// pass runs whenever no usable solid came back - not merely when the result was null.
        /// </summary>
        private static GeometryElement GetGeometryWithReferences(Element element, View view)
        {
            GeometryElement geometry = null;

            if (view != null)
            {
                try
                {
                    geometry = element.get_Geometry(new Options
                    {
                        ComputeReferences = true,
                        IncludeNonVisibleObjects = false,
                        View = view
                    });
                }
                catch
                {
                    geometry = null;
                }

                if (ContainsUsableSolid(geometry))
                    return geometry;
            }

            try
            {
                // View and DetailLevel are mutually exclusive on one Options object.
                GeometryElement modelGeometry = element.get_Geometry(new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = false,
                    DetailLevel = ViewDetailLevel.Fine
                });

                if (ContainsUsableSolid(modelGeometry))
                    return modelGeometry;

                return modelGeometry ?? geometry;
            }
            catch
            {
                return geometry;
            }
        }

        private static bool ContainsUsableSolid(GeometryElement geometry)
        {
            if (geometry == null)
                return false;

            try
            {
                foreach (GeometryObject obj in geometry)
                {
                    if (obj is Solid solid && solid.Faces != null && solid.Faces.Size > 0)
                        return true;

                    if (!(obj is GeometryInstance instance))
                        continue;

                    GeometryElement nested = null;
                    try
                    {
                        nested = instance.GetSymbolGeometry() ?? instance.GetInstanceGeometry();
                    }
                    catch
                    {
                        nested = null;
                    }

                    if (ContainsUsableSolid(nested))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Tessellated points around a face, in host coordinates. A failing edge is skipped on its own
        /// - the original cleared every point collected so far, collapsing the face to a single point.
        /// </summary>
        private static IList<XYZ> GetPlanarFacePoints(PlanarFace face, Transform transform)
        {
            List<XYZ> points = new List<XYZ>();

            try
            {
                foreach (EdgeArray loop in face.EdgeLoops)
                {
                    foreach (Edge edge in loop)
                    {
                        try
                        {
                            IList<XYZ> tessellated = edge.Tessellate();
                            if (tessellated == null)
                                continue;

                            foreach (XYZ point in tessellated)
                            {
                                XYZ transformed = TransformPoint(transform, point);
                                if (transformed != null)
                                    points.Add(transformed);
                            }
                        }
                        catch
                        {
                            // Skip this edge only; keep everything already collected.
                        }
                    }
                }
            }
            catch
            {
                // Keep whatever was collected before the loop failed.
            }

            if (points.Count == 0)
            {
                XYZ origin = TransformPoint(transform, face.Origin);
                if (origin != null)
                    points.Add(origin);
            }

            return points;
        }

        /// <summary>
        /// How far along the measuring direction to look. Read from the crop box only when the crop is
        /// actually on - Revit still returns a CropBox when it is off, but it is a stale extent that
        /// produced a search reach far too short to find genuine references.
        /// </summary>
        private static double ResolveSearchHalfLength(View view, DimensionAxis axis)
        {
            if (view == null || axis == null)
                return DefaultSearchHalfLengthFeet;

            bool cropActive;
            try
            {
                cropActive = view.CropBoxActive;
            }
            catch
            {
                cropActive = false;
            }

            if (!cropActive)
                return DefaultSearchHalfLengthFeet;

            try
            {
                IList<XYZ> corners = GetBoundingBoxCorners(view.CropBox, null);
                if (corners.Count == 0)
                    return DefaultSearchHalfLengthFeet;

                double minCoord = corners.Min(p => p.DotProduct(axis.DimensionDirection));
                double maxCoord = corners.Max(p => p.DotProduct(axis.DimensionDirection));
                double halfLength = Math.Max(
                    Math.Abs(maxCoord - axis.OriginDimensionCoord),
                    Math.Abs(axis.OriginDimensionCoord - minCoord));

                return Math.Max(MinimumSearchHalfLengthFeet, halfLength + SearchPaddingFeet);
            }
            catch
            {
                return DefaultSearchHalfLengthFeet;
            }
        }

        private static IList<XYZ> GetBoundingBoxCorners(BoundingBoxXYZ box, Transform outerTransform)
        {
            List<XYZ> corners = new List<XYZ>();
            if (box == null)
                return corners;

            try
            {
                Transform boxTransform = box.Transform ?? Transform.Identity;
                double[] xs = { box.Min.X, box.Max.X };
                double[] ys = { box.Min.Y, box.Max.Y };
                double[] zs = { box.Min.Z, box.Max.Z };

                foreach (double x in xs)
                {
                    foreach (double y in ys)
                    {
                        foreach (double z in zs)
                        {
                            XYZ corner = boxTransform.OfPoint(new XYZ(x, y, z));
                            if (outerTransform != null && !outerTransform.IsIdentity)
                                corner = outerTransform.OfPoint(corner);

                            corners.Add(corner);
                        }
                    }
                }
            }
            catch
            {
                return corners;
            }

            return corners;
        }

        private static XYZ ProjectPointToViewPlane(View view, XYZ point, XYZ viewNormal)
        {
            if (point == null)
                return null;

            XYZ origin = view?.Origin ?? XYZ.Zero;
            if (viewNormal == null || viewNormal.GetLength() <= NormalizeTolerance)
                return point;

            XYZ normal = viewNormal.Normalize();
            double distance = (point - origin).DotProduct(normal);
            return point - (normal * distance);
        }

        /// <summary>Transforms a point, returning null rather than the project origin when it cannot.</summary>
        private static XYZ TransformPoint(Transform transform, XYZ point)
        {
            if (point == null)
                return null;

            if (transform == null || transform.IsIdentity)
                return point;

            try
            {
                return transform.OfPoint(point);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Transforms a vector, returning null rather than the zero vector when it cannot.</summary>
        private static XYZ TransformVector(Transform transform, XYZ vector)
        {
            if (vector == null)
                return null;

            if (transform == null || transform.IsIdentity)
                return vector;

            try
            {
                return transform.OfVector(vector);
            }
            catch
            {
                return null;
            }
        }
    }
}
