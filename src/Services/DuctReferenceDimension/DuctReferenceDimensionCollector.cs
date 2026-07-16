// Tool Name: Duct Reference Dimension Collector
// Description: Builds segmented duct-to-reference dimension plans along one perpendicular documentation path.
// Author: Ajmal P.S.
// Version: 1.2.0
// Last Updated: 2026-05-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.DuctReferenceDimension
{
    internal sealed class DuctReferenceDimensionCollector
    {
        private readonly ElementIdIntegerComparer _elementIdComparer = new ElementIdIntegerComparer();

        internal DuctDimensionBatchBuildResult BuildSegmentPlans(
            Document doc,
            View view,
            Element selectedDuct,
            ISet<ElementId> processedDuctIds,
            ISet<ElementId> ignoredDuctIds = null)
        {
            DuctDimensionBatchBuildResult result = new DuctDimensionBatchBuildResult
            {
                Plans = new List<DuctDimensionPlan>(),
                Failures = new List<DuctDimensionFailure>()
            };

            if (doc == null || view == null || selectedDuct == null)
            {
                result.Failures.Add(CreateFailure(null, "Missing document, active view, or selected duct."));
                return result;
            }

            if (!DuctReferenceDimensionGeometry.TryCreateAxisFromDuct(view, selectedDuct, out DuctDimensionAxis axis, out string axisReason))
            {
                result.Failures.Add(CreateFailure(selectedDuct.Id, axisReason));
                return result;
            }

            List<DuctReferenceCandidate> candidates = CollectPathReferenceCandidates(
                doc,
                view,
                selectedDuct,
                axis,
                processedDuctIds,
                ignoredDuctIds);

            List<DuctReferenceCandidate> uniqueReferences = BuildUniqueReferenceList(candidates);
            List<DuctFacePair> ductPairs = BuildDuctFacePairs(uniqueReferences, axis)
                .ToList();

            DuctFacePair selectedPair = ductPairs.FirstOrDefault(
                p => p.DuctId != null && p.DuctId.IntValue() == selectedDuct.Id.IntValue());

            if (selectedPair == null)
            {
                result.Failures.Add(CreateFailure(selectedDuct.Id, "Valid opposite duct side face references were not found."));
                return result;
            }

            List<DuctReferenceCandidate> referenceFaces = uniqueReferences
                .Where(IsValidReferenceTarget)
                .ToList();

            if (!TryFindNearestReferenceFace(
                selectedPair,
                referenceFaces,
                out DuctReferenceCandidate referenceFace,
                out ReferenceSide referenceSide,
                out string referenceReason))
            {
                result.Failures.Add(CreateFailure(selectedDuct.Id, referenceReason));
                return result;
            }

            List<DuctFacePair> orderedDucts = BuildOrderedDuctsBetweenReferenceAndSelected(
                ductPairs,
                selectedPair,
                referenceFace,
                referenceSide);

            if (orderedDucts.Count == 0)
            {
                result.Failures.Add(CreateFailure(selectedDuct.Id, "No ducts were found between the reference face and selected duct."));
                return result;
            }

            DuctReferenceCandidate previousFace = referenceFace;
            foreach (DuctFacePair ductPair in orderedDucts)
            {
                DuctReferenceCandidate currentNearFace = ductPair.NearFace;
                if (currentNearFace == null || previousFace == null)
                    continue;

                if (System.Math.Abs(currentNearFace.SortCoord - previousFace.SortCoord) <=
                    DuctReferenceDimensionGeometry.CoordinateMergeTolerance)
                {
                    previousFace = ductPair.OppositeFace;
                    continue;
                }

                DuctDimensionPlan plan = new DuctDimensionPlan
                {
                    SelectedDuctId = ductPair.DuctId,
                    Axis = axis,
                    References = new List<DuctReferenceCandidate> { previousFace, currentNearFace }
                        .OrderBy(c => c.SortCoord)
                        .ToList(),
                    CoveredDuctIds = new List<ElementId> { ductPair.DuctId }
                };

                if (DuctReferenceDimensionGeometry.TryCreateDimensionLine(view, plan, out _))
                    result.Plans.Add(plan);
                else
                    result.Failures.Add(CreateFailure(ductPair.DuctId, "Could not create a dimension line for this segment."));

                previousFace = ductPair.OppositeFace;
            }

            if (result.Plans.Count == 0 && result.Failures.Count == 0)
                result.Failures.Add(CreateFailure(selectedDuct.Id, "No valid dimension segments were found."));

            return result;
        }

        private List<DuctReferenceCandidate> CollectPathReferenceCandidates(
            Document doc,
            View view,
            Element selectedDuct,
            DuctDimensionAxis axis,
            ISet<ElementId> processedDuctIds,
            ISet<ElementId> ignoredDuctIds)
        {
            List<DuctReferenceCandidate> candidates = new List<DuctReferenceCandidate>();
            Dictionary<int, Element> elementsById = new Dictionary<int, Element>();

            AddElement(elementsById, selectedDuct);
            AddVisibleElementsByCategory(doc, view, BuiltInCategory.OST_Walls, axis, elementsById);
            AddVisibleElementsByCategory(doc, view, BuiltInCategory.OST_StructuralColumns, axis, elementsById);
            AddVisibleElementsByCategory(doc, view, BuiltInCategory.OST_StructuralFraming, axis, elementsById);
            AddVisibleDuctsOnPath(doc, view, selectedDuct, axis, processedDuctIds, ignoredDuctIds, elementsById);

            foreach (Element element in elementsById.Values)
            {
                if (!TryGetTargetType(element, out DuctReferenceTargetType targetType))
                    continue;

                candidates.AddRange(
                    DuctReferenceDimensionGeometry.CollectFaceReferenceCandidates(
                        doc,
                        view,
                        element,
                        axis,
                        targetType,
                        selectedDuct.Id));
            }

            return candidates;
        }

        private void AddVisibleDuctsOnPath(
            Document doc,
            View view,
            Element selectedDuct,
            DuctDimensionAxis axis,
            ISet<ElementId> processedDuctIds,
            ISet<ElementId> ignoredDuctIds,
            IDictionary<int, Element> elementsById)
        {
            IEnumerable<Element> ducts = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DuctCurves)
                .WhereElementIsNotElementType();

            foreach (Element duct in ducts)
            {
                if (duct == null)
                    continue;

                bool isSelected = _elementIdComparer.Equals(duct.Id, selectedDuct.Id);
                if (!isSelected && ignoredDuctIds != null && ignoredDuctIds.Contains(duct.Id))
                    continue;

                if (!isSelected && !DuctReferenceDimensionGeometry.MayIntersectAxisBand(duct, view, axis))
                    continue;

                if (!isSelected && !IsDuctParallelToAxis(duct, axis, out _))
                    continue;

                AddElement(elementsById, duct);
            }
        }

        private static void AddVisibleElementsByCategory(
            Document doc,
            View view,
            BuiltInCategory category,
            DuctDimensionAxis axis,
            IDictionary<int, Element> elementsById)
        {
            IEnumerable<Element> elements = new FilteredElementCollector(doc, view.Id)
                .OfCategory(category)
                .WhereElementIsNotElementType();

            foreach (Element element in elements)
            {
                if (element == null)
                    continue;

                if (!DuctReferenceDimensionGeometry.MayIntersectAxisBand(element, view, axis))
                    continue;

                AddElement(elementsById, element);
            }
        }

        private static IEnumerable<DuctFacePair> BuildDuctFacePairs(
            IEnumerable<DuctReferenceCandidate> candidates,
            DuctDimensionAxis axis)
        {
            if (candidates == null)
                yield break;

            foreach (IGrouping<int, DuctReferenceCandidate> group in candidates
                .Where(c => c.IsDuct && c.ElementId != null)
                .GroupBy(c => c.ElementId.IntValue()))
            {
                List<DuctReferenceCandidate> ordered = group
                    .OrderBy(c => c.SortCoord)
                    .ToList();

                if (ordered.Count < 2)
                    continue;

                DuctReferenceCandidate minFace = ordered.First();
                DuctReferenceCandidate maxFace = ordered.Last();
                if (maxFace.SortCoord - minFace.SortCoord <= DuctReferenceDimensionGeometry.CoordinateMergeTolerance)
                    continue;

                yield return new DuctFacePair
                {
                    DuctId = minFace.ElementId,
                    MinFace = minFace,
                    MaxFace = maxFace,
                    CenterCoord = (minFace.SortCoord + maxFace.SortCoord) * 0.5,
                    Axis = axis
                };
            }
        }

        private static bool TryFindNearestReferenceFace(
            DuctFacePair selectedPair,
            IEnumerable<DuctReferenceCandidate> referenceFaces,
            out DuctReferenceCandidate referenceFace,
            out ReferenceSide referenceSide,
            out string reason)
        {
            referenceFace = null;
            referenceSide = ReferenceSide.Negative;
            reason = string.Empty;

            if (selectedPair == null || referenceFaces == null)
            {
                reason = "Missing selected duct or reference face data.";
                return false;
            }

            double bestDistance = double.MaxValue;
            foreach (DuctReferenceCandidate candidate in referenceFaces)
            {
                double negativeDistance = selectedPair.MinFace.SortCoord - candidate.SortCoord;
                if (negativeDistance > DuctReferenceDimensionGeometry.CoordinateMergeTolerance &&
                    negativeDistance < bestDistance)
                {
                    bestDistance = negativeDistance;
                    referenceFace = candidate;
                    referenceSide = ReferenceSide.Negative;
                }

                double positiveDistance = candidate.SortCoord - selectedPair.MaxFace.SortCoord;
                if (positiveDistance > DuctReferenceDimensionGeometry.CoordinateMergeTolerance &&
                    positiveDistance < bestDistance)
                {
                    bestDistance = positiveDistance;
                    referenceFace = candidate;
                    referenceSide = ReferenceSide.Positive;
                }
            }

            if (referenceFace == null)
            {
                reason = "No valid wall, structural column, or structural beam face was found outside the selected duct.";
                return false;
            }

            return true;
        }

        private static List<DuctFacePair> BuildOrderedDuctsBetweenReferenceAndSelected(
            IEnumerable<DuctFacePair> ductPairs,
            DuctFacePair selectedPair,
            DuctReferenceCandidate referenceFace,
            ReferenceSide referenceSide)
        {
            List<DuctFacePair> results = new List<DuctFacePair>();
            if (ductPairs == null || selectedPair == null || referenceFace == null)
                return results;

            double referenceCoord = referenceFace.SortCoord;
            double selectedNearCoord = referenceSide == ReferenceSide.Negative
                ? selectedPair.MinFace.SortCoord
                : selectedPair.MaxFace.SortCoord;
            double tolerance = DuctReferenceDimensionGeometry.CoordinateMergeTolerance;

            foreach (DuctFacePair pair in ductPairs)
            {
                pair.ReferenceSide = referenceSide;
                if (referenceSide == ReferenceSide.Negative)
                {
                    pair.NearFace = pair.MinFace;
                    pair.OppositeFace = pair.MaxFace;
                    if (pair.NearFace.SortCoord <= referenceCoord + tolerance ||
                        pair.NearFace.SortCoord > selectedNearCoord + tolerance)
                    {
                        continue;
                    }
                }
                else
                {
                    pair.NearFace = pair.MaxFace;
                    pair.OppositeFace = pair.MinFace;
                    if (pair.NearFace.SortCoord >= referenceCoord - tolerance ||
                        pair.NearFace.SortCoord < selectedNearCoord - tolerance)
                    {
                        continue;
                    }
                }

                results.Add(pair);
            }

            if (results.All(p => p.DuctId.IntValue() != selectedPair.DuctId.IntValue()))
                results.Add(selectedPair);

            return referenceSide == ReferenceSide.Negative
                ? results.OrderBy(p => p.NearFace.SortCoord).ToList()
                : results.OrderByDescending(p => p.NearFace.SortCoord).ToList();
        }

        private static List<DuctReferenceCandidate> BuildUniqueReferenceList(IEnumerable<DuctReferenceCandidate> candidates)
        {
            List<DuctReferenceCandidate> ordered = new List<DuctReferenceCandidate>();
            if (candidates == null)
                return ordered;

            HashSet<string> seenStableReferences = new HashSet<string>();

            foreach (DuctReferenceCandidate candidate in candidates
                .Where(c => c?.Reference != null && !string.IsNullOrWhiteSpace(c.StableKey))
                .OrderBy(c => c.SortCoord)
                .ThenBy(c => GetCandidatePriority(c))
                .ThenBy(c => c.AxisOffset))
            {
                if (seenStableReferences.Contains(candidate.StableKey))
                    continue;

                seenStableReferences.Add(candidate.StableKey);
                ordered.Add(candidate);
            }

            return ordered.OrderBy(c => c.SortCoord).ToList();
        }

        private static bool IsValidReferenceTarget(DuctReferenceCandidate candidate)
        {
            if (candidate == null)
                return false;

            return candidate.TargetType == DuctReferenceTargetType.Wall ||
                   candidate.TargetType == DuctReferenceTargetType.StructuralColumn ||
                   candidate.TargetType == DuctReferenceTargetType.StructuralBeam;
        }

        private static int GetCandidatePriority(DuctReferenceCandidate candidate)
        {
            if (candidate == null)
                return 100;

            if (candidate.IsSelectedDuct)
                return 0;

            switch (candidate.TargetType)
            {
                case DuctReferenceTargetType.Wall:
                    return 1;
                case DuctReferenceTargetType.StructuralColumn:
                    return 2;
                case DuctReferenceTargetType.StructuralBeam:
                    return 3;
                case DuctReferenceTargetType.Duct:
                    return 10;
                default:
                    return 20;
            }
        }

        private static bool TryGetTargetType(Element element, out DuctReferenceTargetType targetType)
        {
            targetType = DuctReferenceTargetType.Duct;
            Category category = element?.Category;
            if (category == null)
                return false;

            int categoryId = category.Id.IntValue();
            if (categoryId == (int)BuiltInCategory.OST_Walls)
            {
                targetType = DuctReferenceTargetType.Wall;
                return true;
            }

            if (categoryId == (int)BuiltInCategory.OST_StructuralColumns)
            {
                targetType = DuctReferenceTargetType.StructuralColumn;
                return true;
            }

            if (categoryId == (int)BuiltInCategory.OST_StructuralFraming)
            {
                targetType = DuctReferenceTargetType.StructuralBeam;
                return true;
            }

            if (categoryId == (int)BuiltInCategory.OST_DuctCurves)
            {
                targetType = DuctReferenceTargetType.Duct;
                return true;
            }

            return false;
        }

        private static bool IsDuctParallelToAxis(
            Element duct,
            DuctDimensionAxis axis,
            out XYZ ductDirection)
        {
            ductDirection = null;
            LocationCurve locationCurve = duct?.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null || axis == null)
                return false;

            XYZ direction = null;
            try
            {
                Transform derivatives = curve.ComputeDerivatives(0.5, true);
                direction = derivatives?.BasisX;
            }
            catch
            {
                direction = null;
            }

            if ((direction == null || direction.GetLength() <= 1e-9) && curve.IsBound)
                direction = curve.GetEndPoint(1) - curve.GetEndPoint(0);

            if (direction == null || direction.GetLength() <= 1e-9)
                return false;

            XYZ projected = direction - axis.ViewNormal * direction.DotProduct(axis.ViewNormal);
            if (projected.GetLength() <= 1e-9)
                return false;

            ductDirection = projected.Normalize();
            return DuctReferenceDimensionGeometry.AreParallel(ductDirection, axis.DuctDirection, 0.02);
        }

        private static void AddElement(IDictionary<int, Element> elementsById, Element element)
        {
            if (elementsById == null || element?.Id == null)
                return;

            int id = element.Id.IntValue();
            if (!elementsById.ContainsKey(id))
                elementsById.Add(id, element);
        }

        private static DuctDimensionFailure CreateFailure(ElementId elementId, string reason)
        {
            return new DuctDimensionFailure
            {
                ElementId = elementId,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Could not build dimension references." : reason
            };
        }

        private sealed class DuctFacePair
        {
            public ElementId DuctId { get; set; }
            public DuctReferenceCandidate MinFace { get; set; }
            public DuctReferenceCandidate MaxFace { get; set; }
            public DuctReferenceCandidate NearFace { get; set; }
            public DuctReferenceCandidate OppositeFace { get; set; }
            public double CenterCoord { get; set; }
            public DuctDimensionAxis Axis { get; set; }
            public ReferenceSide ReferenceSide { get; set; }
        }

        private enum ReferenceSide
        {
            Negative,
            Positive
        }
    }
}
