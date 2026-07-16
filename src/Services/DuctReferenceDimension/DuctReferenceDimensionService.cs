// Tool Name: Duct Reference Dimension Service
// Description: Runs the AJ Annotation duct reference dimension pick loop and dimension creation.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-05-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Services.DuctReferenceDimension
{
    internal static class DuctReferenceDimensionService
    {
        private const string Title = "Duct Reference Dimension";
        private const string ActiveViewTitle = "Duct Reference Dimension - Active View";
        private const string TransactionName = "AJ Annotation - Duct Reference Dimension";
        private const double ExistingLineToleranceFactor = 1.25;
        private const double MinimumBatchDuctLength = 1000.0 * Constants.MM_TO_FEET;
        private const double VerticalDuctDotTolerance = 0.90;
        private const double DirectionTolerance = 1e-9;

        private static readonly HashSet<ViewType> SupportedPlanViews = new HashSet<ViewType>
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.EngineeringPlan
        };

        internal static Result Execute(ExternalCommandData commandData)
        {
            DuctReferenceDimensionReport report = new DuctReferenceDimensionReport();

            try
            {
                UIDocument uidoc = commandData.Application?.ActiveUIDocument;
                if (uidoc == null)
                    return Fail("Open a project plan view before running this command.");

                Document doc = uidoc.Document;
                View view = doc.ActiveView;
                if (!IsSupportedPlanView(view, out string viewMessage))
                    return Fail(viewMessage);

                DuctReferenceDimensionCollector collector = new DuctReferenceDimensionCollector();
                HashSet<ElementId> processedDuctIds = new HashSet<ElementId>(new ElementIdIntegerComparer());
                List<DuctDimensionLineRecord> createdDimensionRecords = new List<DuctDimensionLineRecord>();
                DuctSelectionFilter selectionFilter = new DuctSelectionFilter();

                while (true)
                {
                    Reference pickedReference;
                    try
                    {
                        pickedReference = uidoc.Selection.PickObject(
                            ObjectType.Element,
                            selectionFilter,
                            "Pick one duct for reference dimension (ESC to finish)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    Element pickedElement = pickedReference == null ? null : doc.GetElement(pickedReference);
                    if (!DuctSelectionFilter.IsDuct(pickedElement))
                    {
                        report.RecordWrongSelection();
                        DialogHelper.ShowInfo(Title, "Please select a duct from the active model.");
                        continue;
                    }

                    report.RecordDuctPicked();

                    if (processedDuctIds.Contains(pickedElement.Id))
                    {
                        report.RecordSkipped("Duct already covered during this command run");
                        continue;
                    }

                    if (ExistingDimensionCoversElement(doc, view, pickedElement.Id))
                    {
                        processedDuctIds.Add(pickedElement.Id);
                        report.RecordSkipped("Existing dimension already covers selected duct");
                        continue;
                    }

                    DuctDimensionBatchBuildResult buildResult = collector.BuildSegmentPlans(
                        doc,
                        view,
                        pickedElement,
                        processedDuctIds);

                    ProcessSegmentPlans(
                        doc,
                        view,
                        buildResult,
                        pickedElement.Id,
                        processedDuctIds,
                        createdDimensionRecords,
                        report);
                }

                if (report.HasActivity)
                {
                    return report.TotalDimensionsCreated > 0 ? Result.Succeeded : Result.Cancelled;
                }

                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(Title, "An error occurred:\n" + ex.Message);
                return Result.Failed;
            }
        }

        internal static Result ExecuteActiveView(ExternalCommandData commandData)
        {
            DuctReferenceDimensionReport report = new DuctReferenceDimensionReport();

            try
            {
                UIDocument uidoc = commandData.Application?.ActiveUIDocument;
                if (uidoc == null)
                    return Fail("Open a project plan view before running this command.");

                Document doc = uidoc.Document;
                View view = doc.ActiveView;
                if (!IsSupportedPlanView(view, out string viewMessage))
                    return Fail(viewMessage);

                DuctReferenceDimensionCollector collector = new DuctReferenceDimensionCollector();
                HashSet<ElementId> processedDuctIds = new HashSet<ElementId>(new ElementIdIntegerComparer());
                HashSet<ElementId> ignoredDuctIds = new HashSet<ElementId>(new ElementIdIntegerComparer());
                List<DuctDimensionLineRecord> createdDimensionRecords = new List<DuctDimensionLineRecord>();

                IList<Element> ducts = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_DuctCurves)
                    .WhereElementIsNotElementType()
                    .Where(DuctSelectionFilter.IsDuct)
                    .OrderBy(e => e.Id.IntValue())
                    .ToList();

                foreach (Element duct in ducts)
                {
                    if (!IsBatchDuctEligible(duct, out string skipReason))
                    {
                        ignoredDuctIds.Add(duct.Id);
                        report.RecordSkipped(skipReason);
                    }
                }

                foreach (Element duct in ducts)
                {
                    if (duct == null || ignoredDuctIds.Contains(duct.Id) || processedDuctIds.Contains(duct.Id))
                        continue;

                    if (ExistingDimensionCoversElement(doc, view, duct.Id))
                    {
                        processedDuctIds.Add(duct.Id);
                        report.RecordSkipped("Existing dimension already covers duct");
                        continue;
                    }

                    DuctDimensionBatchBuildResult buildResult = collector.BuildSegmentPlans(
                        doc,
                        view,
                        duct,
                        processedDuctIds,
                        ignoredDuctIds);

                    ProcessSegmentPlans(
                        doc,
                        view,
                        buildResult,
                        duct.Id,
                        processedDuctIds,
                        createdDimensionRecords,
                        report);
                }

                return report.TotalDimensionsCreated > 0 ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(ActiveViewTitle, "An error occurred:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static void ProcessSegmentPlans(
            Document doc,
            View view,
            DuctDimensionBatchBuildResult buildResult,
            ElementId seedDuctId,
            ISet<ElementId> processedDuctIds,
            IList<DuctDimensionLineRecord> createdDimensionRecords,
            DuctReferenceDimensionReport report)
        {
            if (buildResult == null)
                return;

            if (buildResult.Failures != null)
            {
                foreach (DuctDimensionFailure failure in buildResult.Failures)
                    report.RecordFailed(failure.ElementId, failure.Reason);
            }

            if (!buildResult.HasPlans)
                return;

            foreach (DuctDimensionPlan plan in buildResult.Plans)
            {
                if (plan?.SelectedDuctId == null)
                    continue;

                if (processedDuctIds.Contains(plan.SelectedDuctId))
                {
                    report.RecordSkipped("Duct already covered during this command run");
                    continue;
                }

                if (ExistingDimensionCoversElement(doc, view, plan.SelectedDuctId))
                {
                    processedDuctIds.Add(plan.SelectedDuctId);
                    report.RecordSkipped("Existing dimension already covers duct");
                    continue;
                }

                if (HasSimilarExistingDimension(doc, view, plan))
                {
                    report.RecordSkipped("Similar dimension already exists in active view");
                    MarkCoveredDucts(processedDuctIds, plan.CoveredDuctIds);
                    continue;
                }

                if (OverlapsCreatedDimension(createdDimensionRecords, plan))
                {
                    report.RecordSkipped("Duplicate dimension already created during this command run");
                    MarkCoveredDucts(processedDuctIds, plan.CoveredDuctIds);
                    continue;
                }

                if (!TryCreateDimension(doc, view, plan, out string createReason))
                {
                    report.RecordFailed(plan.SelectedDuctId, createReason);
                    continue;
                }

                MarkCoveredDucts(processedDuctIds, plan.CoveredDuctIds);
                createdDimensionRecords.Add(CreateLineRecord(plan));
                report.RecordCreated(plan.CoveredDuctIds, seedDuctId);
            }
        }

        private static bool IsBatchDuctEligible(Element duct, out string skipReason)
        {
            skipReason = string.Empty;

            if (!DuctSelectionFilter.IsDuct(duct))
            {
                skipReason = "Skipped non-duct category";
                return false;
            }

            LocationCurve locationCurve = duct.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null)
            {
                skipReason = "Skipped duct without a valid location curve";
                return false;
            }

            double length;
            try
            {
                length = curve.Length;
            }
            catch
            {
                skipReason = "Skipped duct with unreadable curve length";
                return false;
            }

            if (length < MinimumBatchDuctLength)
            {
                skipReason = "Skipped duct shorter than 1000 mm";
                return false;
            }

            if (!TryGetDuctDirection(curve, out XYZ direction))
            {
                skipReason = "Skipped duct with unresolved direction";
                return false;
            }

            XYZ normalized = direction.Normalize();
            double verticalDot = Math.Abs(normalized.DotProduct(XYZ.BasisZ));
            XYZ horizontal = normalized - XYZ.BasisZ * normalized.DotProduct(XYZ.BasisZ);
            if (verticalDot >= VerticalDuctDotTolerance || horizontal.GetLength() <= DirectionTolerance)
            {
                skipReason = "Skipped vertical duct";
                return false;
            }

            return true;
        }

        private static bool TryGetDuctDirection(Curve curve, out XYZ direction)
        {
            direction = null;
            if (curve == null)
                return false;

            try
            {
                Transform derivatives = curve.ComputeDerivatives(0.5, true);
                XYZ tangent = derivatives?.BasisX;
                if (tangent != null && tangent.GetLength() > DirectionTolerance)
                {
                    direction = tangent;
                    return true;
                }
            }
            catch
            {
                // Fall back to bound endpoints.
            }

            if (!curve.IsBound)
                return false;

            XYZ vector = curve.GetEndPoint(1) - curve.GetEndPoint(0);
            if (vector.GetLength() <= DirectionTolerance)
                return false;

            direction = vector;
            return true;
        }

        private static bool TryCreateDimension(
            Document doc,
            View view,
            DuctDimensionPlan plan,
            out string reason)
        {
            reason = string.Empty;
            if (doc == null || view == null || plan?.DimensionLine == null)
            {
                reason = "Missing dimension data.";
                return false;
            }

            using (Transaction transaction = new Transaction(doc, TransactionName))
            {
                try
                {
                    transaction.Start();
                    Dimension dimension = doc.Create.NewDimension(view, plan.DimensionLine, plan.ToReferenceArray());
                    if (dimension == null)
                    {
                        if (transaction.HasStarted() && !transaction.HasEnded())
                            transaction.RollBack();

                        reason = "Revit did not create a dimension from the collected references.";
                        return false;
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    if (transaction.HasStarted() && !transaction.HasEnded())
                        transaction.RollBack();

                    reason = "Failed to create dimension: " + ex.Message;
                    return false;
                }
            }
        }

        private static bool ExistingDimensionCoversElement(Document doc, View view, ElementId elementId)
        {
            if (doc == null || view == null || elementId == null)
                return false;

            foreach (Dimension dimension in GetDimensionsInView(doc, view))
            {
                foreach (Reference reference in EnumerateDimensionReferences(dimension))
                {
                    ElementId referenceElementId = reference?.ElementId;
                    if (referenceElementId != null && referenceElementId.IntValue() == elementId.IntValue())
                        return true;
                }
            }

            return false;
        }

        private static bool HasSimilarExistingDimension(Document doc, View view, DuctDimensionPlan plan)
        {
            if (doc == null || view == null || plan?.Axis == null)
                return false;

            DuctDimensionLineRecord proposedRecord = CreateLineRecord(plan);
            foreach (Dimension dimension in GetDimensionsInView(doc, view))
            {
                if (!TryCreateLineRecordFromExistingDimension(doc, dimension, plan.Axis, out DuctDimensionLineRecord existingRecord))
                    continue;

                if (!LineRecordsOverlap(existingRecord, proposedRecord))
                    continue;

                int sharedReferenceCount = existingRecord.StableReferenceKeys
                    .Intersect(proposedRecord.StableReferenceKeys)
                    .Count();

                if (sharedReferenceCount >= 2)
                    return true;
            }

            return false;
        }

        private static bool OverlapsCreatedDimension(
            IEnumerable<DuctDimensionLineRecord> createdRecords,
            DuctDimensionPlan plan)
        {
            if (createdRecords == null || plan == null)
                return false;

            DuctDimensionLineRecord proposedRecord = CreateLineRecord(plan);
            return createdRecords.Any(record =>
                LineRecordsOverlap(record, proposedRecord) &&
                record.StableReferenceKeys.Intersect(proposedRecord.StableReferenceKeys).Count() >= 2);
        }

        private static bool TryCreateLineRecordFromExistingDimension(
            Document doc,
            Dimension dimension,
            DuctDimensionAxis axis,
            out DuctDimensionLineRecord record)
        {
            record = null;
            if (dimension == null || axis == null)
                return false;

            Curve curve = null;
            try
            {
                curve = dimension.Curve;
            }
            catch
            {
                curve = null;
            }

            if (!DuctReferenceDimensionGeometry.TryGetLineIntervalAlongAxis(
                curve,
                axis,
                out double minCoord,
                out double maxCoord,
                out double ductCoord,
                out XYZ direction))
            {
                return false;
            }

            if (!DuctReferenceDimensionGeometry.AreParallel(
                direction,
                axis.DimensionDirection,
                DuctReferenceDimensionGeometry.CoordinateMergeTolerance))
            {
                return false;
            }

            record = new DuctDimensionLineRecord
            {
                DuctId = null,
                DimensionDirection = axis.DimensionDirection,
                DuctDirection = axis.DuctDirection,
                DuctCoord = ductCoord,
                MinDimensionCoord = minCoord,
                MaxDimensionCoord = maxCoord,
                StableReferenceKeys = BuildStableReferenceKeySet(doc, EnumerateDimensionReferences(dimension))
            };

            return true;
        }

        private static DuctDimensionLineRecord CreateLineRecord(DuctDimensionPlan plan)
        {
            HashSet<string> stableKeys = new HashSet<string>();
            if (plan?.References != null)
            {
                foreach (DuctReferenceCandidate candidate in plan.References)
                {
                    if (!string.IsNullOrWhiteSpace(candidate.StableKey))
                        stableKeys.Add(candidate.StableKey);
                }
            }

            return new DuctDimensionLineRecord
            {
                DuctId = plan.SelectedDuctId,
                DimensionDirection = plan.Axis.DimensionDirection,
                DuctDirection = plan.Axis.DuctDirection,
                DuctCoord = plan.Axis.OriginDuctCoord,
                MinDimensionCoord = plan.References.Min(r => r.SortCoord),
                MaxDimensionCoord = plan.References.Max(r => r.SortCoord),
                StableReferenceKeys = stableKeys
            };
        }

        private static bool LineRecordsOverlap(
            DuctDimensionLineRecord existing,
            DuctDimensionLineRecord proposed)
        {
            if (existing == null || proposed == null)
                return false;

            if (!DuctReferenceDimensionGeometry.AreParallel(existing.DimensionDirection, proposed.DimensionDirection, 0.02))
                return false;

            if (!DuctReferenceDimensionGeometry.AreParallel(existing.DuctDirection, proposed.DuctDirection, 0.02))
                return false;

            double lineTolerance = DuctReferenceDimensionGeometry.AxisBandTolerance * ExistingLineToleranceFactor;
            if (Math.Abs(existing.DuctCoord - proposed.DuctCoord) > lineTolerance)
                return false;

            return DuctReferenceDimensionGeometry.IntervalsOverlap(
                existing.MinDimensionCoord,
                existing.MaxDimensionCoord,
                proposed.MinDimensionCoord,
                proposed.MaxDimensionCoord,
                DuctReferenceDimensionGeometry.AxisBandTolerance);
        }

        private static HashSet<string> BuildStableReferenceKeySet(Document doc, IEnumerable<Reference> references)
        {
            HashSet<string> keys = new HashSet<string>();
            if (references == null)
                return keys;

            foreach (Reference reference in references)
            {
                string key = DuctReferenceDimensionGeometry.GetReferenceStableKey(doc, reference);
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }

            return keys;
        }

        private static IEnumerable<Reference> EnumerateDimensionReferences(Dimension dimension)
        {
            if (dimension == null)
                yield break;

            ReferenceArray references = null;
            try
            {
                references = dimension.References;
            }
            catch
            {
                references = null;
            }

            if (references == null)
                yield break;

            ReferenceArrayIterator iterator = references.ForwardIterator();
            while (iterator.MoveNext())
            {
                Reference reference = iterator.Current as Reference;
                if (reference != null)
                    yield return reference;
            }
        }

        private static IEnumerable<Dimension> GetDimensionsInView(Document doc, View view)
        {
            // Category-based filter (not OfClass(typeof(Dimension))): Revit 2025+ returns linear
            // dimensions as the LinearDimension subclass, which an exact-type OfClass filter misses.
            return new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Dimensions)
                .WhereElementIsNotElementType()
                .Cast<Dimension>();
        }

        private static void MarkCoveredDucts(ISet<ElementId> processedDuctIds, IEnumerable<ElementId> coveredDuctIds)
        {
            if (processedDuctIds == null || coveredDuctIds == null)
                return;

            foreach (ElementId elementId in coveredDuctIds)
            {
                if (elementId != null)
                    processedDuctIds.Add(elementId);
            }
        }

        private static bool IsSupportedPlanView(View view, out string message)
        {
            if (view == null)
            {
                message = "No active view.";
                return false;
            }

            if (view.IsTemplate)
            {
                message = "Please run this tool in a non-template plan view.";
                return false;
            }

            if (!SupportedPlanViews.Contains(view.ViewType))
            {
                message = "This tool works only in floor, ceiling, or engineering plan views.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static Result Fail(string message)
        {
            DialogHelper.ShowError(Title, message);
            return Result.Failed;
        }
    }
}
