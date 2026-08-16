#region Metadata
/*
 * Tool Name     : Auto MEP Dimension
 * File Name     : MepReferenceDimensionService.cs
 * Purpose       : Runs the Auto MEP Dimension workflow in its three modes - pick runs one at a time,
 *                 dimension the current selection, or dimension every eligible run in the active view -
 *                 creates the dimensions in a single undoable step, and returns the run report.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Dimensioning, AJTools.Services.Dimensioning,
 *                 AJTools.Utils
 *
 * Input         : Active plan/section view, settings, and either picked runs, the current selection,
 *                 or every eligible run in the view.
 * Output        : Dimensions in the model, and a DimensionRunReport returned to the command.
 *
 * Notes         :
 * - The whole run sits inside ONE TransactionGroup which is assimilated at the end, so a single Ctrl+Z
 *   reverses everything. The previous tool opened and committed a transaction per dimension, leaving
 *   one undo step per dimension - a batch over a busy view could not be taken back in one go.
 *   An empty session rolls the group back so no undo entry is left at all.
 * - This service does NOT show dialogs. It returns the report and the command decides - shared code
 *   that raises its own TaskDialog blocks Revit when called from anywhere but the ribbon.
 * - Existing dimensions are matched on (link instance id, element id) pairs, because for a linked
 *   reference Reference.ElementId is the RevitLinkInstance, not the element. Comparing the wrong one
 *   makes one dimension to any linked element look like a dimension to every linked element.
 * - Whether one dimension already documents another is decided by its REFERENCES, never by where its
 *   string was drawn. Each chain is placed through its own seed run's midpoint, so two parallel runs of
 *   different lengths sit metres apart along the run even when one chain completely contains the other.
 *
 * Changelog     :
 * v1.1.0 (2026-08-16) - Two dimensions measuring the same wall-to-run relationship are no longer left
 *                       stacked. RecordsOverlap dropped the "same station along the run" gate that
 *                       rejected exactly the pairs it was meant to catch, and AddsNothingNew gained an
 *                       element-identity fallback so a chain from an EARLIER command run is recognised
 *                       too, not just one created in the current run.
 * v1.0.0 (2026-08-15) - Initial release, replacing DuctReferenceDimensionService.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Models.Dimensioning;
using AJTools.Services.Dimensioning;
using AJTools.Utils;

namespace AJTools.Services.MepReferenceDimension
{
    /// <summary>How the runs to dimension are chosen.</summary>
    internal enum MepDimensionMode
    {
        /// <summary>Pick runs one at a time until ESC.</summary>
        PickRuns,

        /// <summary>Dimension whatever is selected when the command starts.</summary>
        CurrentSelection,

        /// <summary>Dimension every eligible run in the active view.</summary>
        ActiveView
    }

    /// <summary>What a run produced.</summary>
    internal sealed class MepDimensionRunResult
    {
        internal DimensionRunReport Report { get; set; }
        internal Result Status { get; set; }
    }

    /// <summary>
    /// Orchestrates the Auto MEP Dimension workflow.
    /// </summary>
    internal static class MepReferenceDimensionService
    {
        private const string TransactionGroupName = "AJ Annotation - Auto MEP Dimension";
        private const string TransactionName = "Auto MEP Dimension";
        private const double VerticalRunDotTolerance = 0.90;

        private static readonly HashSet<ViewType> SupportedPlanViews = new HashSet<ViewType>
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.EngineeringPlan
        };

        private static readonly HashSet<ViewType> SupportedSectionViews = new HashSet<ViewType>
        {
            ViewType.Section,
            ViewType.Elevation
        };

        /// <summary>
        /// Runs the workflow. Never shows a dialog; the caller decides what to show.
        /// </summary>
        internal static MepDimensionRunResult Execute(
            ExternalCommandData commandData,
            MepDimensionMode mode,
            MepDimensionSettings settings,
            string title,
            out string blockingMessage)
        {
            blockingMessage = string.Empty;

            MepDimensionRunResult runResult = new MepDimensionRunResult
            {
                Report = new DimensionRunReport(title),
                Status = Result.Cancelled
            };

            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out blockingMessage))
            {
                runResult.Status = Result.Cancelled;
                return runResult;
            }

            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (!IsSupportedView(view, out blockingMessage))
            {
                runResult.Status = Result.Cancelled;
                return runResult;
            }

            MepDimensionSettings effective = settings ?? MepDimensionSettings.CreateDefault();
            effective.Normalize();

            DimensionRunReport report = runResult.Report;
            report.RecordViewProcessed();

            MepDimensionCollector collector = new MepDimensionCollector(effective);
            HashSet<string> coveredKeys = new HashSet<string>(StringComparer.Ordinal);
            List<DimensionLineRecord> createdRecords = new List<DimensionLineRecord>();

            DimensionType dimensionType = DimensionFactory.ResolveDimensionType(doc, effective.DimensionTypeName);
            if (dimensionType == null && !string.IsNullOrWhiteSpace(effective.DimensionTypeName))
            {
                report.AddNote("Dimension type \"" + effective.DimensionTypeName +
                               "\" is not in this project - the default type was used instead.");
            }

            using (TransactionGroup group = new TransactionGroup(doc, TransactionGroupName))
            {
                group.Start();

                try
                {
                    switch (mode)
                    {
                        case MepDimensionMode.PickRuns:
                            RunPickLoop(uidoc, doc, view, effective, collector, coveredKeys,
                                        createdRecords, dimensionType, report, title);
                            break;

                        case MepDimensionMode.CurrentSelection:
                            RunOverElements(doc, view, GetSelectedRuns(uidoc, doc, effective, report),
                                            effective, collector, coveredKeys, createdRecords,
                                            dimensionType, report);
                            break;

                        default:
                            RunOverElements(doc, view, GetEligibleRunsInView(doc, view, effective, report),
                                            effective, collector, coveredKeys, createdRecords,
                                            dimensionType, report);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    group.RollBack();
                    blockingMessage = "An error occurred:\n" + ex.Message;
                    runResult.Status = Result.Failed;
                    return runResult;
                }

                if (report.DimensionsCreated > 0)
                    group.Assimilate();
                else
                    group.RollBack();
            }

            // Which links were read is only worth saying when nothing was created - then it explains
            // where the tool looked. On a successful run it is six lines of noise.
            if (report.DimensionsCreated == 0)
            {
                IList<string> linkNames = collector.GetLinkNamesRead();
                if (linkNames.Count > 0)
                    report.AddNote("Linked models read: " + string.Join(", ", linkNames.ToArray()));
            }

            runResult.Status = report.DimensionsCreated > 0 ? Result.Succeeded : Result.Cancelled;
            return runResult;
        }

        // -----------------------------------------------------------------------------------------
        // Modes
        // -----------------------------------------------------------------------------------------

        private static void RunPickLoop(
            UIDocument uidoc,
            Document doc,
            View view,
            MepDimensionSettings settings,
            MepDimensionCollector collector,
            ISet<string> coveredKeys,
            IList<DimensionLineRecord> createdRecords,
            DimensionType dimensionType,
            DimensionRunReport report,
            string title)
        {
            MepRunSelectionFilter filter = new MepRunSelectionFilter(settings.GetMeasuredCategories());

            while (true)
            {
                Reference picked;
                try
                {
                    picked = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        filter,
                        "Pick a run to dimension (ESC to finish)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                if (!DimensionSource.TryResolvePickedReference(doc, picked, out DimensionSource source, out Element element))
                {
                    report.RecordSkipped("Could not read the picked element");
                    continue;
                }

                if (!filter.IsAllowedRun(element))
                {
                    DialogHelper.ShowInfo(title, "That is not one of the service categories this tool is set to measure.");
                    report.RecordSkipped("Picked element was not a measured service category");
                    continue;
                }

                ProcessRun(doc, view, source, element, settings, collector, coveredKeys,
                           createdRecords, dimensionType, report);
            }
        }

        private static void RunOverElements(
            Document doc,
            View view,
            IEnumerable<SourcedElement> runs,
            MepDimensionSettings settings,
            MepDimensionCollector collector,
            ISet<string> coveredKeys,
            IList<DimensionLineRecord> createdRecords,
            DimensionType dimensionType,
            DimensionRunReport report)
        {
            foreach (SourcedElement run in runs)
            {
                ProcessRun(doc, view, run.Source, run.Element, settings, collector, coveredKeys,
                           createdRecords, dimensionType, report);
            }
        }

        private static void ProcessRun(
            Document doc,
            View view,
            DimensionSource source,
            Element run,
            MepDimensionSettings settings,
            MepDimensionCollector collector,
            ISet<string> coveredKeys,
            IList<DimensionLineRecord> createdRecords,
            DimensionType dimensionType,
            DimensionRunReport report)
        {
            if (source == null || run == null)
                return;

            string key = source.BuildKey(run.Id);

            if (coveredKeys.Contains(key))
            {
                report.RecordSkipped("Already dimensioned earlier in this run");
                return;
            }

            if (settings.SkipAlreadyDimensioned && ExistingDimensionCoversElement(doc, view, key))
            {
                coveredKeys.Add(key);
                report.RecordSkipped("An existing dimension already covers this run");
                return;
            }

            DimensionPlanResult planResult = collector.BuildPlans(doc, view, source, run, coveredKeys);

            if (planResult.Failures != null)
            {
                foreach (DimensionPlanFailure failure in planResult.Failures)
                    report.RecordFailed(failure.ElementId, failure.Reason);
            }

            if (!planResult.HasPlans)
                return;

            foreach (DimensionPlan plan in planResult.Plans)
            {
                DimensionLineRecord proposed = BuildLineRecord(plan);

                // On a skip, only the run this plan was built for is marked covered - never the whole
                // chain. Marking every run in a rejected chain used to retire runs that no dimension
                // actually documents, so they were never attempted again.
                if (settings.SkipAlreadyDimensioned && HasSimilarExistingDimension(doc, view, plan, proposed))
                {
                    report.RecordSkipped("A matching dimension already exists in this view");
                    MarkCovered(coveredKeys, new[] { plan.SeedElementKey });
                    continue;
                }

                // Anything this tool already drew that the new dimension completely supersedes is
                // REPLACED, not stacked on top of. Dimensioning the second duct therefore turns the
                // first duct's dimension into one longer string, instead of leaving two overlapping.
                List<DimensionLineRecord> supersededHere = createdRecords
                    .Where(r => RecordsOverlap(r, proposed, MepDimensionGeometry.CoordinateMergeTolerance * 150.0) &&
                                AddsNothingNew(proposed, r, MepDimensionGeometry.CoordinateMergeTolerance * 150.0))
                    .ToList();

                List<ElementId> toDelete = supersededHere
                    .Where(r => r.CreatedId != null)
                    .Select(r => r.CreatedId)
                    .ToList();

                toDelete.AddRange(FindSupersededOwnedDimensions(doc, view, plan, proposed, toDelete));

                using (Transaction transaction = new Transaction(doc, TransactionName))
                {
                    try
                    {
                        transaction.Start();

                        bool created = DimensionFactory.TryCreate(
                            doc, view, plan.DimensionLine, plan.ToReferenceArray(), dimensionType,
                            out Dimension dimension, out string reason);

                        if (!created)
                        {
                            if (transaction.HasStarted() && !transaction.HasEnded())
                                transaction.RollBack();

                            report.RecordFailed(plan.SeedElementId, reason);
                            continue;
                        }

                        // Stamp it so a later run can recognise it as ours and tidy it up.
                        DimensionOwnership.Mark(dimension);
                        proposed.CreatedId = dimension.Id;

                        // Only now that the replacement exists is the old one removed.
                        foreach (ElementId oldId in toDelete.Distinct(new ElementIdIntegerComparer()))
                        {
                            try { doc.Delete(oldId); }
                            catch { /* leaving a superseded dimension behind is not worth failing over */ }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        if (transaction.HasStarted() && !transaction.HasEnded())
                            transaction.RollBack();

                        report.RecordFailed(plan.SeedElementId, ex.Message);
                        continue;
                    }
                }

                foreach (DimensionLineRecord gone in supersededHere)
                    createdRecords.Remove(gone);

                if (toDelete.Count > 0)
                    report.RecordReplaced(toDelete.Count);

                createdRecords.Add(proposed);
                MarkCovered(coveredKeys, plan.CoveredElementKeys);
                report.RecordCreated(plan.CoveredElementKeys?.Count ?? 1);
            }
        }

        // -----------------------------------------------------------------------------------------
        // Run selection
        // -----------------------------------------------------------------------------------------

        private static IList<SourcedElement> GetSelectedRuns(
            UIDocument uidoc,
            Document doc,
            MepDimensionSettings settings,
            DimensionRunReport report)
        {
            List<SourcedElement> runs = new List<SourcedElement>();
            ICollection<ElementId> selected;

            try
            {
                selected = uidoc.Selection.GetElementIds();
            }
            catch
            {
                return runs;
            }

            if (selected == null || selected.Count == 0)
                return runs;

            DimensionSource host = DimensionSource.ForHost(doc);
            MepRunSelectionFilter filter = new MepRunSelectionFilter(settings.GetMeasuredCategories());

            foreach (ElementId id in selected)
            {
                Element element = doc.GetElement(id);
                if (element == null)
                    continue;

                if (!filter.IsAllowedRun(element))
                {
                    report.RecordSkipped("Selected element was not a measured service category");
                    continue;
                }

                if (!IsRunEligible(element, settings, out string reason))
                {
                    report.RecordSkipped(reason);
                    continue;
                }

                runs.Add(new SourcedElement { Source = host, Element = element });
            }

            return runs;
        }

        private static IList<SourcedElement> GetEligibleRunsInView(
            Document doc,
            View view,
            MepDimensionSettings settings,
            DimensionRunReport report)
        {
            List<SourcedElement> runs = new List<SourcedElement>();
            DimensionSource host = DimensionSource.ForHost(doc);

            foreach (BuiltInCategory category in settings.GetMeasuredCategories())
            {
                IList<Element> elements;
                try
                {
                    elements = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .ToElements();
                }
                catch
                {
                    continue;
                }

                foreach (Element element in elements.Where(e => e != null).OrderBy(e => e.Id.LongValue()))
                {
                    if (!IsRunEligible(element, settings, out string reason))
                    {
                        report.RecordSkipped(reason);
                        continue;
                    }

                    runs.Add(new SourcedElement { Source = host, Element = element });
                }
            }

            return runs;
        }

        private static bool IsRunEligible(Element run, MepDimensionSettings settings, out string reason)
        {
            reason = string.Empty;

            Curve curve = (run?.Location as LocationCurve)?.Curve;
            if (curve == null)
            {
                reason = "Skipped a fitting or accessory with no straight run";
                return false;
            }

            double length;
            try
            {
                length = curve.Length;
            }
            catch
            {
                reason = "Skipped a run whose length could not be read";
                return false;
            }

            if (settings.MinimumRunLengthMm > 0 &&
                length < settings.MinimumRunLengthMm * Constants.MM_TO_FEET)
            {
                reason = "Skipped runs shorter than " +
                         settings.MinimumRunLengthMm.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture) +
                         " mm";
                return false;
            }

            if (!settings.SkipVerticalRuns)
                return true;

            if (!MepDimensionGeometry.TryGetCurveDirection(curve, out XYZ direction))
            {
                reason = "Skipped a run whose direction could not be read";
                return false;
            }

            if (Math.Abs(direction.DotProduct(XYZ.BasisZ)) >= VerticalRunDotTolerance)
            {
                reason = "Skipped vertical runs";
                return false;
            }

            return true;
        }

        // -----------------------------------------------------------------------------------------
        // Existing dimension matching
        // -----------------------------------------------------------------------------------------

        private static bool ExistingDimensionCoversElement(Document doc, View view, string elementKey)
        {
            if (string.IsNullOrEmpty(elementKey))
                return false;

            foreach (Dimension dimension in GetDimensionsInView(doc, view))
            {
                foreach (Reference reference in EnumerateReferences(dimension))
                {
                    if (string.Equals(DimensionSource.GetReferenceTargetKey(reference), elementKey, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static bool HasSimilarExistingDimension(
            Document doc,
            View view,
            DimensionPlan plan,
            DimensionLineRecord proposed)
        {
            if (plan?.Axis == null || proposed == null)
                return false;

            foreach (Dimension dimension in GetDimensionsInView(doc, view))
            {
                Curve curve;
                try
                {
                    curve = dimension.Curve;
                }
                catch
                {
                    continue;
                }

                if (!MepDimensionGeometry.TryGetLineIntervalAlongAxis(
                        curve, plan.Axis, out double minCoord, out double maxCoord,
                        out double runCoord, out XYZ direction))
                {
                    continue;
                }

                if (!MepDimensionGeometry.AreParallel(
                        direction, plan.Axis.DimensionDirection, MepDimensionGeometry.DirectionAlignmentTolerance))
                {
                    continue;
                }

                DimensionLineRecord existing = new DimensionLineRecord
                {
                    DimensionDirection = plan.Axis.DimensionDirection,
                    MinDimensionCoord = minCoord,
                    MaxDimensionCoord = maxCoord,
                    StableReferenceKeys = BuildStableKeySet(doc, dimension),
                    ElementKeys = BuildElementKeySet(dimension)
                };

                if (!RecordsOverlap(existing, proposed, plan.Axis.AxisBandTolerance))
                    continue;

                if (AddsNothingNew(existing, proposed, plan.Axis.AxisBandTolerance))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Dimensions ALREADY IN THE VIEW that this tool created and that the new one completely
        /// supersedes. Ownership is checked on the element itself - a dimension drawn by hand is never
        /// returned here, however well it matches (Ajmal's rule, 2026-08-15).
        /// </summary>
        private static IList<ElementId> FindSupersededOwnedDimensions(
            Document doc,
            View view,
            DimensionPlan plan,
            DimensionLineRecord proposed,
            ICollection<ElementId> alreadyQueued)
        {
            List<ElementId> found = new List<ElementId>();
            if (plan?.Axis == null || proposed == null)
                return found;

            foreach (Dimension dimension in GetDimensionsInView(doc, view))
            {
                if (alreadyQueued != null &&
                    alreadyQueued.Any(id => id != null && id.IntValue() == dimension.Id.IntValue()))
                {
                    continue;
                }

                if (!DimensionOwnership.IsOwned(dimension))
                    continue;

                Curve curve;
                try { curve = dimension.Curve; }
                catch { continue; }

                if (!MepDimensionGeometry.TryGetLineIntervalAlongAxis(
                        curve, plan.Axis, out double minCoord, out double maxCoord,
                        out double runCoord, out XYZ direction))
                {
                    continue;
                }

                if (!MepDimensionGeometry.AreParallel(
                        direction, plan.Axis.DimensionDirection, MepDimensionGeometry.DirectionAlignmentTolerance))
                {
                    continue;
                }

                DimensionLineRecord existing = new DimensionLineRecord
                {
                    DimensionDirection = plan.Axis.DimensionDirection,
                    MinDimensionCoord = minCoord,
                    MaxDimensionCoord = maxCoord,
                    StableReferenceKeys = BuildStableKeySet(doc, dimension),
                    ElementKeys = BuildElementKeySet(dimension)
                };

                if (!RecordsOverlap(existing, proposed, plan.Axis.AxisBandTolerance))
                    continue;

                // Only when the new dimension carries everything the old one did.
                if (AddsNothingNew(proposed, existing, plan.Axis.AxisBandTolerance))
                    found.Add(dimension.Id);
            }

            return found;
        }

        /// <summary>
        /// True only when the proposed dimension would document nothing the existing one does not
        /// already carry.
        /// </summary>
        /// <remarks>
        /// The test used to be "they share two or more references", which is wrong in the common case:
        /// every chain in a row of parallel runs shares its wall face and its first run face with the
        /// shortest chain, so wall-A-B-C was discarded as a duplicate of wall-A and runs B and C were
        /// then marked as covered and never dimensioned at all. Whether that happened depended purely on
        /// which run had the lower element id. A chain that reaches further must survive.
        /// </remarks>
        private static bool AddsNothingNew(
            DimensionLineRecord existing,
            DimensionLineRecord proposed,
            double tolerance)
        {
            if (existing == null || proposed == null)
                return false;

            // Preferred test - face for face. Two records built inside the SAME command run come from
            // one collector pass, so their stable representations are directly comparable.
            if (existing.StableReferenceKeys != null &&
                proposed.StableReferenceKeys != null &&
                proposed.StableReferenceKeys.Count > 0 &&
                proposed.StableReferenceKeys.IsSubsetOf(existing.StableReferenceKeys))
            {
                return true;
            }

            // Fallback, for a dimension read back out of the model. Revit does not promise that the
            // stable representation Dimension.References hands back is the same string that was passed
            // to NewDimension, so a chain left by an EARLIER command run can fail the face test while
            // documenting exactly the same thing. Element identity survives that round trip.
            if (existing.ElementKeys == null || proposed.ElementKeys == null || proposed.ElementKeys.Count == 0)
                return false;

            if (!proposed.ElementKeys.IsSubsetOf(existing.ElementKeys))
                return false;

            // Touching the same elements is not enough on its own: with "dimension both sides" on, the
            // two chains from one run touch the same run and reach in OPPOSITE directions, and neither
            // documents the other. Requiring the proposed span to sit inside the existing one separates
            // a genuinely shorter chain from a chain that merely starts at the same place.
            return proposed.MinDimensionCoord >= existing.MinDimensionCoord - tolerance &&
                   proposed.MaxDimensionCoord <= existing.MaxDimensionCoord + tolerance;
        }

        /// <summary>
        /// Cheap geometric pre-filter: do these two dimensions measure the same way, over overlapping
        /// ground? What each one actually documents is decided by <see cref="AddsNothingNew"/>.
        /// </summary>
        /// <remarks>
        /// This deliberately does NOT compare where each dimension sits ALONG the run, which is what it
        /// used to do. Every chain is drawn through its own seed run's midpoint, so two parallel ducts of
        /// different lengths put their strings metres apart down the run - and the old test demanded they
        /// agree to within 187.5 mm before it would even look at the references. The result was the bug
        /// Ajmal reported: the long wall-to-outer-duct chain never recognised the short wall-to-inner-duct
        /// dimension it supersedes, so both were left stacked on the drawing. Whether one dimension
        /// carries everything another does is a question about references, not about where the string
        /// was placed.
        /// </remarks>
        private static bool RecordsOverlap(DimensionLineRecord existing, DimensionLineRecord proposed, double bandTolerance)
        {
            if (existing == null || proposed == null)
                return false;

            if (!MepDimensionGeometry.AreParallel(
                    existing.DimensionDirection, proposed.DimensionDirection, MepDimensionGeometry.DirectionAlignmentTolerance))
            {
                return false;
            }

            return MepDimensionGeometry.IntervalsOverlap(
                existing.MinDimensionCoord,
                existing.MaxDimensionCoord,
                proposed.MinDimensionCoord,
                proposed.MaxDimensionCoord,
                bandTolerance);
        }

        private static DimensionLineRecord BuildLineRecord(DimensionPlan plan)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> elementKeys = new HashSet<string>(StringComparer.Ordinal);

            if (plan.References != null)
            {
                foreach (DimensionReferenceCandidate candidate in plan.References)
                {
                    if (!string.IsNullOrWhiteSpace(candidate.StableKey))
                        keys.Add(candidate.StableKey);

                    if (!string.IsNullOrWhiteSpace(candidate.ElementKey))
                        elementKeys.Add(candidate.ElementKey);
                }
            }

            return new DimensionLineRecord
            {
                DimensionDirection = plan.Axis.DimensionDirection,
                MinDimensionCoord = plan.References.Min(r => r.SortCoord),
                MaxDimensionCoord = plan.References.Max(r => r.SortCoord),
                StableReferenceKeys = keys,
                ElementKeys = elementKeys
            };
        }

        /// <summary>
        /// The elements an existing dimension points at, in the same "linkInstanceId:elementId" form the
        /// candidates use. For a linked reference the real element is Reference.LinkedElementId, not
        /// Reference.ElementId - GetReferenceTargetKey owns that distinction.
        /// </summary>
        private static HashSet<string> BuildElementKeySet(Dimension dimension)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (Reference reference in EnumerateReferences(dimension))
            {
                string key = DimensionSource.GetReferenceTargetKey(reference);
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }

            return keys;
        }

        private static HashSet<string> BuildStableKeySet(Document doc, Dimension dimension)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (Reference reference in EnumerateReferences(dimension))
            {
                string key = DimensionSource.GetStableKey(doc, reference);
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }

            return keys;
        }

        private static IEnumerable<Reference> EnumerateReferences(Dimension dimension)
        {
            if (dimension == null)
                yield break;

            ReferenceArray references;
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
                if (iterator.Current is Reference reference)
                    yield return reference;
            }
        }

        private static IEnumerable<Dimension> GetDimensionsInView(Document doc, View view)
        {
            // Category-based filter, not OfClass(typeof(Dimension)): Revit 2025+ returns linear
            // dimensions as the LinearDimension subclass, which an exact-type filter silently misses.
            IList<Element> elements;
            try
            {
                elements = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Dimensions)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }
            catch
            {
                return Enumerable.Empty<Dimension>();
            }

            return elements.OfType<Dimension>();
        }

        private static void MarkCovered(ISet<string> coveredKeys, IEnumerable<string> keys)
        {
            if (coveredKeys == null || keys == null)
                return;

            foreach (string key in keys)
            {
                if (!string.IsNullOrEmpty(key))
                    coveredKeys.Add(key);
            }
        }

        private static bool IsSupportedView(View view, out string message)
        {
            if (view == null)
            {
                message = "No active view.";
                return false;
            }

            if (view.IsTemplate)
            {
                message = "Please run this tool in a normal project view, not a view template.";
                return false;
            }

            if (SupportedPlanViews.Contains(view.ViewType) || SupportedSectionViews.Contains(view.ViewType))
            {
                message = string.Empty;
                return true;
            }

            message = "This tool works in floor, ceiling and structural plans, and in sections and elevations.";
            return false;
        }

        private sealed class SourcedElement
        {
            internal DimensionSource Source { get; set; }
            internal Element Element { get; set; }
        }
    }
}
