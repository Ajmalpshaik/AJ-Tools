// Tool Name: Quick Parallel Dimension Service
// Description: Creates quick dimension strings for selected parallel line-based elements.
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2026-03-29
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Utils

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Services.QuickDimension
{
    /// <summary>
    /// Reference strategy used by quick parallel dimension commands.
    /// </summary>
    internal enum QuickDimensionReferenceMode
    {
        Centerline,
        FaceEdge
    }

    /// <summary>
    /// Service that mirrors the pyRevit quick dimension workflow for parallel elements.
    /// </summary>
    internal static class QuickParallelDimensionService
    {
        private const string CenterlineTitle = "Quick Parallel Dimension - Center Line";
        private const string FaceEdgeTitle = "Quick Parallel Dimension - Face/Edge";
        private const double FaceCoordTolerance = 1e-4;

        private static readonly Options GeometryOptions = new Options
        {
            ComputeReferences = true,
            IncludeNonVisibleObjects = true
        };

        /// <summary>
        /// Backward-compatible entry point. Defaults to centerline mode.
        /// </summary>
        internal static Result Execute(ExternalCommandData data)
        {
            return Execute(data, QuickDimensionReferenceMode.Centerline);
        }

        /// <summary>
        /// Executes quick dimension creation for selected or picked parallel elements.
        /// </summary>
        internal static Result Execute(ExternalCommandData data, QuickDimensionReferenceMode mode)
        {
            string title = GetTitle(mode);

            try
            {
                UIDocument uidoc = data.Application.ActiveUIDocument;
                if (uidoc == null)
                    return Fail(title, "Open a project view before running this command.");

                Document doc = uidoc.Document;
                View view = doc.ActiveView;
                if (view == null || view.IsTemplate)
                    return Fail(title, "Please run this tool in a normal project view.");

                // The whole workflow (sketch-plane setup + the final dimension) is wrapped in one
                // TransactionGroup so a cancelled/failed run rolls back the sketch-plane change too,
                // instead of leaving a stray "Set Sketch Plane" undo entry with nothing dimensioned.
                using (TransactionGroup group = new TransactionGroup(doc, title))
                {
                    group.Start();

                    if (!EnsureSketchPlane(doc, view))
                    {
                        group.RollBack();
                        return Fail(title, "Could not prepare the active view for point picking.");
                    }

                    IList<Element> selected = GetOrPickElements(uidoc, doc, mode);
                    if (selected == null || selected.Count == 0)
                    {
                        group.RollBack();
                        return Result.Cancelled;
                    }

                    if (mode == QuickDimensionReferenceMode.Centerline && selected.Count < 2)
                    {
                        group.RollBack();
                        return Fail(title, "Select at least two parallel elements.");
                    }

                    if (mode == QuickDimensionReferenceMode.FaceEdge && selected.Count < 1)
                    {
                        group.RollBack();
                        return Fail(title, "Select at least one element.");
                    }

                    if (!TryResolveLeadDirection(uidoc, doc, selected, out XYZ leadDirection))
                    {
                        group.RollBack();
                        return Result.Cancelled;
                    }

                    XYZ placementPoint = uidoc.Selection.PickPoint(
                        ObjectSnapTypes.None,
                        "Pick a point to place the quick dimension line");

                    if (!TryBuildDimensionLine(
                        view,
                        placementPoint,
                        selected,
                        leadDirection,
                        out Line dimLine,
                        out XYZ sortDirection))
                    {
                        group.RollBack();
                        return Fail(title, "Failed to create a valid dimension line from the picked point.");
                    }

                    if (!TryBuildReferenceArray(
                        doc,
                        view,
                        selected,
                        leadDirection,
                        sortDirection,
                        placementPoint,
                        mode,
                        out ReferenceArray referenceArray,
                        out int skippedCount))
                    {
                        group.RollBack();
                        return Fail(
                            title,
                            mode == QuickDimensionReferenceMode.FaceEdge
                                ? "Could not find two opposite face/edge references on enough elements. Try straight ducts/pipes or simpler linear geometry."
                                : "Could not extract valid references. Try selecting simple parallel line-based elements.");
                    }

                    using (Transaction tx = new Transaction(doc, title))
                    {
                        tx.Start();
                        doc.Create.NewDimension(view, dimLine, referenceArray);
                        tx.Commit();
                    }

                    group.Assimilate();

                    if (skippedCount > 0)
                    {
                        DialogHelper.ShowInfo(
                            title,
                            $"Dimension created. {skippedCount} selected element(s) were skipped because valid references were not found for {GetModeLabel(mode)}.");
                    }

                    return Result.Succeeded;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                return Fail(title, "An error occurred:\n" + ex.Message);
            }
        }

        private static string GetTitle(QuickDimensionReferenceMode mode)
        {
            return mode == QuickDimensionReferenceMode.FaceEdge ? FaceEdgeTitle : CenterlineTitle;
        }

        private static string GetModeLabel(QuickDimensionReferenceMode mode)
        {
            return mode == QuickDimensionReferenceMode.FaceEdge ? "face/edge mode" : "center line mode";
        }

        private static bool EnsureSketchPlane(Document doc, View view)
        {
            if (view.SketchPlane != null)
                return true;

            try
            {
                using (Transaction tx = new Transaction(doc, "Set Sketch Plane"))
                {
                    tx.Start();
                    Plane plane = Plane.CreateByOriginAndBasis(view.Origin, view.RightDirection, view.UpDirection);
                    SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
                    view.SketchPlane = sketchPlane;
                    tx.Commit();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IList<Element> GetOrPickElements(
            UIDocument uidoc,
            Document doc,
            QuickDimensionReferenceMode mode)
        {
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds != null && selectedIds.Count > 0)
            {
                return selectedIds
                    .Select(doc.GetElement)
                    .Where(e => e != null)
                    .ToList();
            }

            string prompt = mode == QuickDimensionReferenceMode.FaceEdge
                ? "Select parallel elements to dimension by both faces/edges"
                : "Select parallel elements to dimension by center line";

            IList<Reference> picked = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new AnyElementSelectionFilter(),
                prompt);

            return picked
                .Select(r => doc.GetElement(r))
                .Where(e => e != null)
                .ToList();
        }

        private static bool TryResolveLeadDirection(
            UIDocument uidoc,
            Document doc,
            IEnumerable<Element> selected,
            out XYZ direction)
        {
            direction = null;

            foreach (Element element in selected)
            {
                if (TryGetLinearDirection(element, out direction))
                    return true;
            }

            Reference guideRef = uidoc.Selection.PickObject(
                ObjectType.Element,
                new LinearElementSelectionFilter(),
                "No line direction found in selection. Pick a parallel line element.");

            Element guideElement = doc.GetElement(guideRef);
            return TryGetLinearDirection(guideElement, out direction);
        }

        private static bool TryBuildDimensionLine(
            View view,
            XYZ placementPoint,
            IEnumerable<Element> elements,
            XYZ leadDirection,
            out Line line,
            out XYZ sortDirection)
        {
            line = null;
            sortDirection = null;

            XYZ viewNormal = view.ViewDirection;
            if (viewNormal == null || viewNormal.IsZeroLength())
                viewNormal = XYZ.BasisZ;

            viewNormal = viewNormal.Normalize();

            XYZ leadOnPlane = leadDirection - (viewNormal * leadDirection.DotProduct(viewNormal));
            if (leadOnPlane.IsZeroLength())
                leadOnPlane = view.RightDirection;

            if (leadOnPlane == null || leadOnPlane.IsZeroLength())
                return false;

            leadOnPlane = leadOnPlane.Normalize();

            XYZ dimDirection = viewNormal.CrossProduct(leadOnPlane);
            if (dimDirection.IsZeroLength())
                dimDirection = new XYZ(-leadOnPlane.Y, leadOnPlane.X, leadOnPlane.Z);

            if (dimDirection.IsZeroLength())
                return false;

            dimDirection = dimDirection.Normalize();
            sortDirection = dimDirection;

            List<double> coords = new List<double>();
            foreach (Element element in elements)
            {
                if (TryGetLinearDirection(element, out XYZ elementDirection) &&
                    !IsParallel(elementDirection, leadDirection))
                {
                    continue;
                }

                XYZ anchor = GetElementAnchor(element, view);
                if (anchor != null)
                    coords.Add(anchor.DotProduct(dimDirection));
            }

            double originCoord = placementPoint.DotProduct(dimDirection);
            double min = originCoord - 10.0;
            double max = originCoord + 10.0;

            if (coords.Count >= 2)
            {
                min = coords.Min();
                max = coords.Max();
            }

            double span = Math.Max(max - min, 5.0);
            double padding = Math.Max(2.0, span * 0.15);

            XYZ start = placementPoint + (dimDirection * (min - originCoord - padding));
            XYZ end = placementPoint + (dimDirection * (max - originCoord + padding));

            if (start.DistanceTo(end) < Constants.MIN_DISTANCE_TOLERANCE)
                end = start + (dimDirection * 10.0);

            line = Line.CreateBound(start, end);
            return true;
        }

        private static bool TryBuildReferenceArray(
            Document doc,
            View view,
            IEnumerable<Element> elements,
            XYZ leadDirection,
            XYZ sortDirection,
            XYZ placementPoint,
            QuickDimensionReferenceMode mode,
            out ReferenceArray referenceArray,
            out int skippedCount)
        {
            referenceArray = new ReferenceArray();
            skippedCount = 0;

            List<ReferenceCandidate> allCandidates = new List<ReferenceCandidate>();
            HashSet<string> seenStableRefs = new HashSet<string>();

            foreach (Element element in elements)
            {
                if (TryGetLinearDirection(element, out XYZ elementDirection) &&
                    !IsParallel(elementDirection, leadDirection))
                {
                    skippedCount++;
                    continue;
                }

                if (mode == QuickDimensionReferenceMode.FaceEdge)
                {
                    if (!TryGetFaceEdgeReferenceCandidates(
                        element,
                        leadDirection,
                        sortDirection,
                        placementPoint,
                        out List<ReferenceCandidate> faceCandidates))
                    {
                        skippedCount++;
                        continue;
                    }

                    int added = 0;
                    foreach (ReferenceCandidate candidate in faceCandidates)
                    {
                        if (TryAppendCandidate(doc, seenStableRefs, allCandidates, candidate.Ref, candidate.SortCoord))
                            added++;
                    }

                    if (added < 2)
                        skippedCount++;
                }
                else
                {
                    if (!TryGetCenterlineReferenceForElement(
                        element,
                        view,
                        leadDirection,
                        placementPoint,
                        out Reference reference))
                    {
                        skippedCount++;
                        continue;
                    }

                    XYZ anchor = GetCenterlineAnchor(element, view) ?? new XYZ();
                    if (!TryAppendCandidate(doc, seenStableRefs, allCandidates, reference, anchor.DotProduct(sortDirection)))
                        skippedCount++;
                }
            }

            foreach (ReferenceCandidate candidate in allCandidates.OrderBy(c => c.SortCoord))
            {
                referenceArray.Append(candidate.Ref);
            }

            return referenceArray.Size >= 2;
        }

        private static bool TryAppendCandidate(
            Document doc,
            HashSet<string> seenStableRefs,
            ICollection<ReferenceCandidate> allCandidates,
            Reference reference,
            double sortCoord)
        {
            if (reference == null)
                return false;

            string stable = GetReferenceStableKey(doc, reference);
            if (seenStableRefs.Contains(stable))
                return false;

            seenStableRefs.Add(stable);
            allCandidates.Add(new ReferenceCandidate
            {
                Ref = reference,
                SortCoord = sortCoord
            });
            return true;
        }

        private static bool TryGetFaceEdgeReferenceCandidates(
            Element element,
            XYZ leadDirection,
            XYZ sortDirection,
            XYZ placementPoint,
            out List<ReferenceCandidate> candidates)
        {
            candidates = new List<ReferenceCandidate>();

            if (!TryCollectLineReferenceCandidates(element, placementPoint, out List<LineReferenceCandidate> lineCandidates))
                return false;

            List<LineReferenceCandidate> aligned = lineCandidates
                .Where(c => IsParallel(c.Direction, leadDirection))
                .ToList();

            if (aligned.Count < 2)
                return false;

            foreach (LineReferenceCandidate candidate in aligned)
            {
                candidate.SortCoord = candidate.Anchor.DotProduct(sortDirection);
            }

            double minCoord = aligned.Min(c => c.SortCoord);
            double maxCoord = aligned.Max(c => c.SortCoord);
            if (maxCoord - minCoord <= Constants.MIN_DISTANCE_TOLERANCE)
                return false;

            LineReferenceCandidate minFace = aligned
                .Where(c => Math.Abs(c.SortCoord - minCoord) <= FaceCoordTolerance)
                .OrderBy(c => c.DistanceToPick)
                .FirstOrDefault();

            LineReferenceCandidate maxFace = aligned
                .Where(c => Math.Abs(c.SortCoord - maxCoord) <= FaceCoordTolerance)
                .OrderBy(c => c.DistanceToPick)
                .FirstOrDefault();

            if (minFace == null || maxFace == null || minFace.Ref == null || maxFace.Ref == null)
                return false;

            if (Math.Abs(minFace.SortCoord - maxFace.SortCoord) <= Constants.MIN_DISTANCE_TOLERANCE)
                return false;

            candidates.Add(new ReferenceCandidate { Ref = minFace.Ref, SortCoord = minFace.SortCoord });
            candidates.Add(new ReferenceCandidate { Ref = maxFace.Ref, SortCoord = maxFace.SortCoord });
            return true;
        }

        private static bool TryGetCenterlineReferenceForElement(
            Element element,
            View view,
            XYZ leadDirection,
            XYZ placementPoint,
            out Reference reference)
        {
            reference = null;

            if (element == null)
                return false;

            // First preference: explicit location-curve reference (true element axis).
            if (TryGetLocationCurveReference(element, leadDirection, out reference))
                return true;

            XYZ axisAnchor = GetCenterlineAnchor(element, view) ?? placementPoint;

            // Second preference: non-edge curve references nearest to the element axis.
            if (TryGetCenterlineCurveReferenceFromGeometry(
                element,
                leadDirection,
                axisAnchor,
                out reference))
            {
                return true;
            }

            if (element is FamilyInstance familyInstance &&
                TryGetFamilyInstanceReference(familyInstance, leadDirection, out reference))
            {
                return true;
            }

            try
            {
                reference = new Reference(element);
                return true;
            }
            catch
            {
                reference = null;
                return false;
            }
        }

        private static bool TryGetLocationCurveReference(
            Element element,
            XYZ leadDirection,
            out Reference reference)
        {
            reference = null;
            if (!(element?.Location is LocationCurve locationCurve))
                return false;

            if (!(locationCurve.Curve is Line locationLine))
                return false;

            if (!IsParallel(locationLine.Direction, leadDirection))
                return false;

            try
            {
                if (locationCurve.Curve.Reference != null)
                {
                    reference = locationCurve.Curve.Reference;
                    return true;
                }
            }
            catch
            {
                // Ignore and continue with geometry-based centerline detection.
            }

            return false;
        }

        private static bool TryGetCenterlineCurveReferenceFromGeometry(
            Element element,
            XYZ leadDirection,
            XYZ axisAnchor,
            out Reference reference)
        {
            reference = null;

            if (!TryCollectCenterlineCurveCandidates(element, out List<LineReferenceCandidate> candidates))
                return false;

            List<LineReferenceCandidate> aligned = candidates
                .Where(c => IsParallel(c.Direction, leadDirection))
                .ToList();

            if (aligned.Count == 0)
                return false;

            LineReferenceCandidate best = aligned
                .OrderBy(c => c.Anchor.DistanceTo(axisAnchor))
                .FirstOrDefault();

            reference = best?.Ref;
            return reference != null;
        }

        private static bool TryGetFamilyInstanceReference(
            FamilyInstance familyInstance,
            XYZ leadDirection,
            out Reference reference)
        {
            reference = null;

            if (familyInstance.Location is LocationPoint locationPoint)
            {
                if (TryGetPointReferenceAtLocation(familyInstance, locationPoint.Point, out reference))
                    return true;
            }

            foreach (FamilyInstanceReferenceType type in Enum.GetValues(typeof(FamilyInstanceReferenceType)))
            {
                IList<Reference> refs;
                try
                {
                    refs = familyInstance.GetReferences(type);
                }
                catch
                {
                    continue;
                }

                if (refs == null || refs.Count == 0)
                    continue;

                Reference fallback = null;
                foreach (Reference candidate in refs)
                {
                    GeometryObject geom;
                    try
                    {
                        geom = familyInstance.GetGeometryObjectFromReference(candidate);
                    }
                    catch
                    {
                        geom = null;
                    }

                    if (geom is Edge edge && edge.AsCurve() is Line line)
                    {
                        if (IsParallel(line.Direction, leadDirection))
                        {
                            reference = candidate;
                            return true;
                        }

                        if (fallback == null)
                            fallback = candidate;
                    }
                    else if (fallback == null)
                    {
                        fallback = candidate;
                    }
                }

                if (fallback != null)
                {
                    reference = fallback;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetPointReferenceAtLocation(
            FamilyInstance familyInstance,
            XYZ location,
            out Reference reference)
        {
            reference = null;

            GeometryElement geometry;
            try
            {
                geometry = familyInstance.get_Geometry(GeometryOptions);
            }
            catch
            {
                geometry = null;
            }

            if (geometry == null)
                return false;

            return TryFindPointReferenceRecursive(geometry, location, out reference);
        }

        private static bool TryFindPointReferenceRecursive(
            GeometryElement geometry,
            XYZ location,
            out Reference reference)
        {
            reference = null;
            if (geometry == null)
                return false;

            foreach (GeometryObject obj in geometry)
            {
                if (obj is Point point && point.Reference != null && point.Coord.IsAlmostEqualTo(location))
                {
                    reference = point.Reference;
                    return true;
                }

                if (obj is GeometryInstance instance)
                {
                    if (TryFindPointReferenceRecursive(instance.GetInstanceGeometry(), location, out reference))
                        return true;
                }
            }

            return false;
        }



        private static bool TryCollectLineReferenceCandidates(
            Element element,
            XYZ placementPoint,
            out List<LineReferenceCandidate> candidates)
        {
            candidates = new List<LineReferenceCandidate>();
            if (element == null)
                return false;

            GeometryElement geometry;
            try
            {
                geometry = element.get_Geometry(GeometryOptions);
            }
            catch
            {
                geometry = null;
            }

            if (geometry == null)
                return false;

            CollectLineReferenceCandidates(geometry, placementPoint, candidates);
            return candidates.Count > 0;
        }

        private static bool TryCollectCenterlineCurveCandidates(
            Element element,
            out List<LineReferenceCandidate> candidates)
        {
            candidates = new List<LineReferenceCandidate>();
            if (element == null)
                return false;

            GeometryElement geometry;
            try
            {
                geometry = element.get_Geometry(GeometryOptions);
            }
            catch
            {
                geometry = null;
            }

            if (geometry == null)
                return false;

            CollectCenterlineCurveCandidates(geometry, candidates);
            return candidates.Count > 0;
        }

        private static void CollectLineReferenceCandidates(
            GeometryElement geometry,
            XYZ placementPoint,
            IList<LineReferenceCandidate> candidates)
        {
            if (geometry == null)
                return;

            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid)
                {
                    if (solid.Edges == null || solid.Edges.Size == 0)
                        continue;

                    foreach (Edge edge in solid.Edges)
                    {
                        Line line = edge.AsCurve() as Line;
                        if (line == null || edge.Reference == null)
                            continue;

                        candidates.Add(new LineReferenceCandidate
                        {
                            Ref = edge.Reference,
                            Direction = line.Direction.Normalize(),
                            Anchor = GetLineAnchor(line),
                            DistanceToPick = DistanceToLine(placementPoint, line)
                        });
                    }
                }
                else if (obj is Curve curve)
                {
                    Line line = curve as Line;
                    if (line == null || curve.Reference == null)
                        continue;

                    candidates.Add(new LineReferenceCandidate
                    {
                        Ref = curve.Reference,
                        Direction = line.Direction.Normalize(),
                        Anchor = GetLineAnchor(line),
                        DistanceToPick = DistanceToLine(placementPoint, line)
                    });
                }
                else if (obj is GeometryInstance instance)
                {
                    CollectLineReferenceCandidates(instance.GetInstanceGeometry(), placementPoint, candidates);
                }
            }
        }

        private static void CollectCenterlineCurveCandidates(
            GeometryElement geometry,
            IList<LineReferenceCandidate> candidates)
        {
            if (geometry == null)
                return;

            foreach (GeometryObject obj in geometry)
            {
                if (obj is Curve curve)
                {
                    Line line = curve as Line;
                    if (line == null || curve.Reference == null)
                        continue;

                    candidates.Add(new LineReferenceCandidate
                    {
                        Ref = curve.Reference,
                        Direction = line.Direction.Normalize(),
                        Anchor = GetLineAnchor(line),
                        DistanceToPick = 0.0
                    });
                }
                else if (obj is GeometryInstance instance)
                {
                    CollectCenterlineCurveCandidates(instance.GetInstanceGeometry(), candidates);
                }
            }
        }

        private static XYZ GetLineAnchor(Line line)
        {
            if (line == null)
                return new XYZ();

            try
            {
                if (line.IsBound)
                    return (line.GetEndPoint(0) + line.GetEndPoint(1)) * 0.5;
            }
            catch
            {
                // Fall back to line origin for unbound/invalid lines.
            }

            return line.Origin;
        }

        private static bool TryGetLinearDirection(Element element, out XYZ direction)
        {
            direction = null;
            if (element == null)
                return false;

            if (element.Location is LocationCurve locationCurve)
            {
                XYZ dir = locationCurve.Curve.GetCurveDirection();
                if (dir != null && !dir.IsZeroLength())
                {
                    direction = dir.Normalize();
                    return true;
                }
            }

            if (element is CurveElement curveElement)
            {
                XYZ dir = curveElement.GeometryCurve.GetCurveDirection();
                if (dir != null && !dir.IsZeroLength())
                {
                    direction = dir.Normalize();
                    return true;
                }
            }

            return false;
        }

        private static XYZ GetElementAnchor(Element element, View view)
        {
            if (element == null)
                return null;

            if (element.Location is LocationPoint locationPoint)
                return locationPoint.Point;

            if (element.Location is LocationCurve locationCurve)
            {
                Curve c = locationCurve.Curve;
                if (c != null)
                    return c.Evaluate(0.5, true);
            }

            if (element is CurveElement curveElement && curveElement.GeometryCurve != null)
                return curveElement.GeometryCurve.Evaluate(0.5, true);

            BoundingBoxXYZ box = element.get_BoundingBox(view) ?? element.get_BoundingBox(null);
            if (box != null)
                return (box.Min + box.Max) * 0.5;

            return null;
        }

        private static XYZ GetCenterlineAnchor(Element element, View view)
        {
            if (element?.Location is LocationCurve locationCurve && locationCurve.Curve != null)
            {
                try
                {
                    return locationCurve.Curve.Evaluate(0.5, true);
                }
                catch
                {
                    // Fall back to generic anchor.
                }
            }

            return GetElementAnchor(element, view);
        }

        private static double DistanceToLine(XYZ point, Line line)
        {
            try
            {
                IntersectionResult result = line.Project(point);
                if (result != null)
                    return result.XYZPoint.DistanceTo(point);
            }
            catch
            {
                // Ignore projection failures and use endpoint fallback.
            }

            return point.DistanceTo(line.GetEndPoint(0));
        }

        private static bool IsParallel(XYZ a, XYZ b)
        {
            if (a == null || b == null || a.IsZeroLength() || b.IsZeroLength())
                return false;

            XYZ cross = a.Normalize().CrossProduct(b.Normalize());
            return cross.GetLength() <= Constants.PARALLEL_TOLERANCE;
        }

        private static string GetReferenceStableKey(Document doc, Reference reference)
        {
            if (reference == null)
                return string.Empty;

            try
            {
                return reference.ConvertToStableRepresentation(doc);
            }
            catch
            {
                return AJTools.Utils.ElementIdHelper.GetIntegerValue(reference.ElementId).ToString();
            }
        }

        private static Result Fail(string title, string message)
        {
            DialogHelper.ShowError(title, message);
            return Result.Failed;
        }

        private class ReferenceCandidate
        {
            public Reference Ref { get; set; }
            public double SortCoord { get; set; }
        }

        private class LineReferenceCandidate
        {
            public Reference Ref { get; set; }
            public XYZ Direction { get; set; }
            public XYZ Anchor { get; set; }
            public double SortCoord { get; set; }
            public double DistanceToPick { get; set; }
        }

        private class AnyElementSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem != null;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }

        private class LinearElementSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return TryGetLinearDirection(elem, out _);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}
