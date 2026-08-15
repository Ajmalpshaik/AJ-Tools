#region Metadata
/*
 * Tool Name     : Auto MEP Dimension
 * File Name     : MepDimensionCollector.cs
 * Purpose       : Turns one picked service run into planned dimensions - gathers every configured
 *                 reference target from the open project and from loaded Revit links, works out the
 *                 nearest reference on each requested side, and builds the chain from that reference
 *                 back to the run.
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
 * Dependencies  : Autodesk Revit API, AJTools.Models.Dimensioning, AJTools.Services.Dimensioning,
 *                 AJTools.Utils
 *
 * Input         : Host document, view, a seed run and its source, and MepDimensionSettings.
 * Output        : DimensionPlanResult - plans to create, plus failures with reasons.
 *
 * Notes         :
 * - Which categories are read, and whether each comes from the open project, from links, or both, is
 *   driven entirely by the settings. Nothing here is hard-coded to ducts.
 * - Elements are keyed by "linkInstanceId:elementId", never by ElementId alone: ids repeat between
 *   documents, so an id-keyed dictionary silently drops a linked wall that collides with a host duct.
 * - Round runs (pipe, conduit, round duct) have no flat side faces, so when the face pass finds
 *   nothing the run falls back to its centreline reference.
 * - Link elements cannot be collected through a host view, so linked categories are read from the link
 *   document and culled by the axis-band test instead.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release, replacing DuctReferenceDimensionCollector.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.Dimensioning;
using AJTools.Services.Dimensioning;
using AJTools.Utils;

namespace AJTools.Services.MepReferenceDimension
{
    /// <summary>
    /// Builds dimension plans for one measured run.
    /// </summary>
    internal sealed class MepDimensionCollector
    {
        private readonly MepDimensionSettings _settings;
        private readonly double _axisBandFeet;

        // One collector lives for a whole command run, so the element lists are gathered once rather
        // than once per run being dimensioned. Without this, dimensioning every duct in a view would
        // re-enumerate every wall, column, beam and link for each duct in turn.
        private readonly Dictionary<string, IList<Element>> _elementCache =
            new Dictionary<string, IList<Element>>(StringComparer.Ordinal);

        private IList<DimensionSource> _cachedSources;
        private bool _cachedSourcesIncludeLinks;

        internal MepDimensionCollector(MepDimensionSettings settings)
        {
            _settings = settings ?? MepDimensionSettings.CreateDefault();
            _axisBandFeet = _settings.SearchBandMm * Constants.MM_TO_FEET;
        }

        /// <summary>Every source the settings ask for, gathered once per command run.</summary>
        private IList<DimensionSource> GetSources(Document hostDocument, bool includeLinks)
        {
            if (_cachedSources != null && _cachedSourcesIncludeLinks == includeLinks)
                return _cachedSources;

            _cachedSources = DimensionSource.Collect(hostDocument, true, includeLinks);
            _cachedSourcesIncludeLinks = includeLinks;
            return _cachedSources;
        }

        /// <summary>The names of the links actually read, for the report.</summary>
        internal IList<string> GetLinkNamesRead()
        {
            if (_cachedSources == null)
                return new List<string>();

            return _cachedSources
                .Where(s => s.IsLinked)
                .Select(s => s.DisplayName)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Plans every dimension for one seed run. <paramref name="alreadyCoveredKeys"/> holds runs
        /// already documented in this command run so they are not measured twice.
        /// </summary>
        internal DimensionPlanResult BuildPlans(
            Document hostDocument,
            View view,
            DimensionSource seedSource,
            Element seedRun,
            ISet<string> alreadyCoveredKeys)
        {
            DimensionPlanResult result = new DimensionPlanResult
            {
                Plans = new List<DimensionPlan>(),
                Failures = new List<DimensionPlanFailure>()
            };

            if (hostDocument == null || view == null || seedSource == null || seedRun == null)
            {
                result.Failures.Add(Failure(null, "Missing document, view or run."));
                return result;
            }

            if (!MepDimensionGeometry.TryCreateAxis(
                    view, seedRun, seedSource.ToHost, _axisBandFeet, out DimensionAxis axis, out string axisReason))
            {
                result.Failures.Add(Failure(seedRun.Id, axisReason));
                return result;
            }

            string seedKey = seedSource.BuildKey(seedRun.Id);

            List<DimensionReferenceCandidate> candidates =
                CollectAllCandidates(hostDocument, view, seedSource, seedRun, seedKey, axis);

            List<DimensionReferenceCandidate> unique = Deduplicate(candidates);

            Dictionary<string, RunFaces> runFaces = BuildRunFaces(unique);
            List<DimensionReferenceCandidate> referenceTargets = unique
                .Where(c => !c.IsMeasuredRun)
                .ToList();

            if (!runFaces.TryGetValue(seedKey, out RunFaces seedFaces))
            {
                result.Failures.Add(Failure(
                    seedRun.Id,
                    "Could not read usable side faces or a centreline for the selected run."));
                return result;
            }

            if (referenceTargets.Count == 0)
            {
                result.Failures.Add(Failure(
                    seedRun.Id,
                    "No wall, column, beam, floor, grid or level was found in line with this run."));
                return result;
            }

            List<ChainSide> sides = new List<ChainSide>();
            if (_settings.DimensionBothSides)
            {
                sides.Add(ChainSide.Negative);
                sides.Add(ChainSide.Positive);
            }
            else
            {
                sides.Add(PickNearestSide(seedFaces, referenceTargets));
            }

            bool anyPlan = false;
            foreach (ChainSide side in sides.Distinct())
            {
                if (TryBuildSide(
                        view, axis, seedRun, seedKey, seedFaces, runFaces, referenceTargets,
                        side, alreadyCoveredKeys, result))
                {
                    anyPlan = true;
                }
            }

            if (!anyPlan && result.Failures.Count == 0)
            {
                result.Failures.Add(Failure(
                    seedRun.Id,
                    "No wall, column, beam, floor, grid or level was found beyond this run to measure to."));
            }

            return result;
        }

        // -----------------------------------------------------------------------------------------
        // Candidate gathering
        // -----------------------------------------------------------------------------------------

        private List<DimensionReferenceCandidate> CollectAllCandidates(
            Document hostDocument,
            View view,
            DimensionSource seedSource,
            Element seedRun,
            string seedKey,
            DimensionAxis axis)
        {
            List<DimensionReferenceCandidate> candidates = new List<DimensionReferenceCandidate>();

            // The seed run always contributes, whatever its category is configured to do.
            candidates.AddRange(CollectRunCandidates(seedSource, view, seedRun, axis, seedKey));

            bool anyLinked = _settings.AnyLinkedTargets();
            IList<DimensionSource> allSources = GetSources(hostDocument, anyLinked);

            foreach (DimensionTargetKind kind in MepDimensionSettings.SupportedTargetKinds)
            {
                DimensionModelScope scope = _settings.GetScope(kind);
                if (scope == DimensionModelScope.None)
                    continue;

                foreach (DimensionSource source in allSources)
                {
                    bool wanted = source.IsLinked
                        ? _settings.UsesLinkedModels(kind)
                        : _settings.UsesCurrentModel(kind);

                    if (!wanted)
                        continue;

                    CollectForKind(source, view, kind, axis, seedSource, seedRun, seedKey, candidates);
                }
            }

            return candidates;
        }

        private void CollectForKind(
            DimensionSource source,
            View view,
            DimensionTargetKind kind,
            DimensionAxis axis,
            DimensionSource seedSource,
            Element seedRun,
            string seedKey,
            IList<DimensionReferenceCandidate> candidates)
        {
            if (kind == DimensionTargetKind.MepRun || kind == DimensionTargetKind.SameServiceRun)
            {
                // "Other services" means a DIFFERENT service to the one being measured - a pipe or tray
                // beside a duct. Another duct beside a duct is a separate decision, so it has its own
                // row in the settings. Without this split, ticking "other services" to pick up pipework
                // silently pulled in every neighbouring duct as well.
                int seedCategoryId = seedRun?.Category?.Id?.IntValue() ?? -1;
                bool wantSameCategory = kind == DimensionTargetKind.SameServiceRun;

                // EVERY service category is searched here, not just the ones ticked in "Services to
                // dimension". Those two lists answer different questions: section 1 is what gets
                // DIMENSIONED, section 2 is what gets MEASURED TO. Driving this from section 1 meant
                // "other services" was inert on the shipped defaults - with only Ducts ticked there was
                // no other category left to find - and the only way to pick up a pipe as a reference was
                // to tick Pipes, which then dimensioned every pipe in its own right.
                foreach (BuiltInCategory category in MepDimensionSettings.SupportedMeasuredCategories)
                {
                    bool isSeedCategory = (int)category == seedCategoryId;
                    if (isSeedCategory != wantSameCategory)
                        continue;

                    foreach (Element element in EnumerateElements(source, view, category))
                    {
                        string key = source.BuildKey(element.Id);
                        if (string.Equals(key, seedKey, StringComparison.Ordinal))
                            continue;

                        if (!MepDimensionGeometry.MayIntersectAxisBand(element, view, axis, source.ToHost))
                            continue;

                        if (!IsRunAlignedWithAxis(element, source.ToHost, axis))
                            continue;

                        foreach (DimensionReferenceCandidate candidate in
                                 CollectRunCandidates(source, view, element, axis, seedKey))
                        {
                            candidates.Add(candidate);
                        }
                    }
                }

                return;
            }

            if (kind == DimensionTargetKind.Grid || kind == DimensionTargetKind.Level)
            {
                foreach (Element datum in EnumerateDatums(source, view, kind))
                {
                    DimensionReferenceCandidate candidate =
                        MepDimensionGeometry.CollectDatumCandidate(source, view, datum, axis, kind, seedKey);

                    if (candidate != null)
                        candidates.Add(candidate);
                }

                return;
            }

            BuiltInCategory? solidCategory = GetSolidCategory(kind);
            if (solidCategory == null)
                return;

            foreach (Element element in EnumerateElements(source, view, solidCategory.Value))
            {
                if (!MepDimensionGeometry.MayIntersectAxisBand(element, view, axis, source.ToHost))
                    continue;

                foreach (DimensionReferenceCandidate candidate in
                         MepDimensionGeometry.CollectFaceCandidates(source, view, element, axis, kind, false, seedKey))
                {
                    candidates.Add(candidate);
                }
            }
        }

        /// <summary>
        /// A run's references: its side faces, or its centreline when it is round and has none.
        /// </summary>
        private IList<DimensionReferenceCandidate> CollectRunCandidates(
            DimensionSource source,
            View view,
            Element run,
            DimensionAxis axis,
            string seedKey)
        {
            IList<DimensionReferenceCandidate> faces = MepDimensionGeometry.CollectFaceCandidates(
                source, view, run, axis, DimensionTargetKind.MepRun, true, seedKey);

            if (faces.Count >= 2)
                return faces;

            DimensionReferenceCandidate centreline = MepDimensionGeometry.CollectCenterlineCandidate(
                source, run, axis, DimensionTargetKind.MepRun, true, seedKey);

            if (centreline != null)
                return new List<DimensionReferenceCandidate> { centreline };

            return faces;
        }

        private IEnumerable<Element> EnumerateElements(DimensionSource source, View view, BuiltInCategory category)
        {
            string cacheKey = source.BuildKey(null) + "|cat|" + ((int)category).ToString(CultureInfo.InvariantCulture);
            if (_elementCache.TryGetValue(cacheKey, out IList<Element> cached))
                return cached;

            IList<Element> elements;
            try
            {
                // A host view cannot scope a collector over a link document.
                FilteredElementCollector collector = source.IsLinked
                    ? new FilteredElementCollector(source.Document)
                    : new FilteredElementCollector(source.Document, view.Id);

                elements = collector
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(e => e != null)
                    .ToList();
            }
            catch
            {
                elements = new List<Element>();
            }

            _elementCache[cacheKey] = elements;
            return elements;
        }

        private IEnumerable<Element> EnumerateDatums(DimensionSource source, View view, DimensionTargetKind kind)
        {
            string cacheKey = source.BuildKey(null) + "|datum|" + kind;
            if (_elementCache.TryGetValue(cacheKey, out IList<Element> cached))
                return cached;

            Type datumType = kind == DimensionTargetKind.Grid ? typeof(Grid) : typeof(Level);
            IList<Element> elements;

            try
            {
                FilteredElementCollector collector = source.IsLinked
                    ? new FilteredElementCollector(source.Document)
                    : new FilteredElementCollector(source.Document, view.Id);

                elements = collector
                    .OfClass(datumType)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(e => e != null)
                    .ToList();
            }
            catch
            {
                elements = new List<Element>();
            }

            _elementCache[cacheKey] = elements;
            return elements;
        }

        private static BuiltInCategory? GetSolidCategory(DimensionTargetKind kind)
        {
            switch (kind)
            {
                case DimensionTargetKind.Wall:
                    return BuiltInCategory.OST_Walls;
                case DimensionTargetKind.StructuralColumn:
                    return BuiltInCategory.OST_StructuralColumns;
                case DimensionTargetKind.StructuralBeam:
                    return BuiltInCategory.OST_StructuralFraming;
                case DimensionTargetKind.ArchitecturalColumn:
                    return BuiltInCategory.OST_Columns;
                case DimensionTargetKind.Floor:
                    return BuiltInCategory.OST_Floors;
                default:
                    return null;
            }
        }

        private static bool IsRunAlignedWithAxis(Element run, Transform toHost, DimensionAxis axis)
        {
            Curve curve = (run?.Location as LocationCurve)?.Curve;
            if (curve == null || axis == null)
                return false;

            if (!MepDimensionGeometry.TryGetCurveDirection(curve, out XYZ direction))
                return false;

            XYZ hostDirection = toHost == null || toHost.IsIdentity ? direction : toHost.OfVector(direction);
            XYZ projected = MepDimensionGeometry.ProjectVectorToPlane(hostDirection, axis.ViewNormal);
            if (projected == null || projected.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                return false;

            return MepDimensionGeometry.AreParallel(
                projected, axis.RunDirection, MepDimensionGeometry.DirectionAlignmentTolerance);
        }

        // -----------------------------------------------------------------------------------------
        // Ordering and chain building
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Removes duplicate references. Ties at the same position resolve by target priority, so a
        /// wall face wins over a run face sitting at the same coordinate.
        /// </summary>
        private static List<DimensionReferenceCandidate> Deduplicate(IEnumerable<DimensionReferenceCandidate> candidates)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<DimensionReferenceCandidate> unique = new List<DimensionReferenceCandidate>();

            foreach (DimensionReferenceCandidate candidate in candidates
                .Where(c => c != null && c.HostReference != null && !string.IsNullOrWhiteSpace(c.StableKey))
                .OrderBy(c => c.SortCoord)
                .ThenBy(GetPriority)
                .ThenBy(c => c.AxisOffset))
            {
                if (seen.Contains(candidate.StableKey))
                    continue;

                seen.Add(candidate.StableKey);
                unique.Add(candidate);
            }

            return unique.OrderBy(c => c.SortCoord).ToList();
        }

        private static int GetPriority(DimensionReferenceCandidate candidate)
        {
            if (candidate == null)
                return 100;

            if (candidate.IsSeedRun)
                return 0;

            switch (candidate.Kind)
            {
                case DimensionTargetKind.Wall: return 1;
                case DimensionTargetKind.StructuralColumn: return 2;
                case DimensionTargetKind.StructuralBeam: return 3;
                case DimensionTargetKind.ArchitecturalColumn: return 4;
                case DimensionTargetKind.Floor: return 5;
                case DimensionTargetKind.Grid: return 6;
                case DimensionTargetKind.Level: return 7;
                case DimensionTargetKind.MepRun: return 10;
                default: return 20;
            }
        }

        /// <summary>
        /// Groups run candidates by element into a near/far pair. A round run contributing only a
        /// centreline collapses to the same candidate on both ends, which the chain handles.
        /// </summary>
        private static Dictionary<string, RunFaces> BuildRunFaces(IEnumerable<DimensionReferenceCandidate> candidates)
        {
            Dictionary<string, RunFaces> map = new Dictionary<string, RunFaces>(StringComparer.Ordinal);

            foreach (IGrouping<string, DimensionReferenceCandidate> group in candidates
                .Where(c => c.IsMeasuredRun && !string.IsNullOrEmpty(c.ElementKey))
                .GroupBy(c => c.ElementKey, StringComparer.Ordinal))
            {
                List<DimensionReferenceCandidate> ordered = group.OrderBy(c => c.SortCoord).ToList();
                if (ordered.Count == 0)
                    continue;

                DimensionReferenceCandidate min = ordered.First();
                DimensionReferenceCandidate max = ordered.Last();

                map[group.Key] = new RunFaces
                {
                    ElementKey = group.Key,
                    ElementId = min.ElementId,
                    MinFace = min,
                    MaxFace = max,
                    IsSingleReference = ReferenceEquals(min, max) ||
                                        Math.Abs(max.SortCoord - min.SortCoord) <= MepDimensionGeometry.CoordinateMergeTolerance
                };
            }

            return map;
        }

        /// <summary>Which side has the closer reference target - used when only one side is wanted.</summary>
        private static ChainSide PickNearestSide(RunFaces seedFaces, IEnumerable<DimensionReferenceCandidate> targets)
        {
            double bestNegative = double.MaxValue;
            double bestPositive = double.MaxValue;

            foreach (DimensionReferenceCandidate target in targets)
            {
                double negative = seedFaces.MinFace.SortCoord - target.SortCoord;
                if (negative > MepDimensionGeometry.CoordinateMergeTolerance && negative < bestNegative)
                    bestNegative = negative;

                double positive = target.SortCoord - seedFaces.MaxFace.SortCoord;
                if (positive > MepDimensionGeometry.CoordinateMergeTolerance && positive < bestPositive)
                    bestPositive = positive;
            }

            return bestPositive < bestNegative ? ChainSide.Positive : ChainSide.Negative;
        }

        private bool TryBuildSide(
            View view,
            DimensionAxis axis,
            Element seedRun,
            string seedKey,
            RunFaces seedFaces,
            IDictionary<string, RunFaces> runFaces,
            IList<DimensionReferenceCandidate> referenceTargets,
            ChainSide side,
            ISet<string> alreadyCoveredKeys,
            DimensionPlanResult result)
        {
            bool negative = side == ChainSide.Negative;

            double seedNearCoord = negative ? seedFaces.MinFace.SortCoord : seedFaces.MaxFace.SortCoord;

            DimensionReferenceCandidate referenceTarget = null;
            double best = double.MaxValue;

            foreach (DimensionReferenceCandidate target in referenceTargets)
            {
                double distance = negative
                    ? seedNearCoord - target.SortCoord
                    : target.SortCoord - seedNearCoord;

                if (distance > MepDimensionGeometry.CoordinateMergeTolerance && distance < best)
                {
                    best = distance;
                    referenceTarget = target;
                }
            }

            if (referenceTarget == null)
                return false;

            // Every run whose near face lies between the reference target and the seed run.
            List<RunFaces> runsInBetween = runFaces.Values
                .Where(run => IsBetween(run, referenceTarget.SortCoord, seedNearCoord, negative))
                .ToList();

            if (runsInBetween.All(r => !string.Equals(r.ElementKey, seedKey, StringComparison.Ordinal)))
                runsInBetween.Add(seedFaces);

            runsInBetween = negative
                ? runsInBetween.OrderBy(r => r.MinFace.SortCoord).ToList()
                : runsInBetween.OrderByDescending(r => r.MaxFace.SortCoord).ToList();

            if (runsInBetween.Count == 0)
                return false;

            List<DimensionReferenceCandidate> chain = new List<DimensionReferenceCandidate> { referenceTarget };
            List<string> covered = new List<string>();

            // With both sides on, the two chains meet at the run and continue in opposite directions, so
            // the run's width belongs to ONE of them - putting it in both draws the same width twice,
            // on top of itself.
            bool includeWidth = _settings.IncludeRunWidth &&
                                (!_settings.DimensionBothSides || negative);

            foreach (RunFaces run in runsInBetween)
            {
                DimensionReferenceCandidate near = negative ? run.MinFace : run.MaxFace;
                DimensionReferenceCandidate far = negative ? run.MaxFace : run.MinFace;

                chain.Add(near);

                if (includeWidth && !run.IsSingleReference)
                    chain.Add(far);

                covered.Add(run.ElementKey);
            }

            List<DimensionReferenceCandidate> ordered = CollapseCoincident(
                chain
                    .GroupBy(c => c.StableKey, StringComparer.Ordinal)
                    .Select(g => g.First())
                    .OrderBy(c => c.SortCoord)
                    .ToList(),
                covered);

            if (ordered.Count < 2)
                return false;

            if (_settings.ChainStyle == DimensionChainStyle.SingleString)
            {
                return AddPlan(result, axis, seedRun, seedKey, ordered, covered, 0.0);
            }

            bool any = false;
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                List<DimensionReferenceCandidate> pair = new List<DimensionReferenceCandidate>
                {
                    ordered[i],
                    ordered[i + 1]
                };

                List<string> segmentCovered = pair
                    .Where(c => c.IsMeasuredRun)
                    .Select(c => c.ElementKey)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (AddPlan(result, axis, seedRun, seedKey, pair, segmentCovered, 0.0))
                    any = true;
            }

            return any;
        }

        /// <summary>
        /// Builds one planned dimension. Every chain sits on the SAME line through the run's midpoint:
        /// the two sides of a both-sides run extend in opposite directions from the run and never
        /// overlap, and separate segments sit end to end, so they read as one continuous line of
        /// dimensions rather than a scatter of offset rows.
        /// </summary>
        private bool AddPlan(
            DimensionPlanResult result,
            DimensionAxis axis,
            Element seedRun,
            string seedKey,
            IList<DimensionReferenceCandidate> references,
            IList<string> covered,
            double rowOffsetFeet)
        {
            DimensionPlan plan = new DimensionPlan
            {
                SeedElementId = seedRun.Id,
                SeedElementKey = seedKey,
                Axis = axis,
                References = references,
                CoveredElementKeys = covered
            };

            if (!MepDimensionGeometry.TryCreateDimensionLine(plan, rowOffsetFeet, out string lineReason))
            {
                result.Failures.Add(Failure(seedRun.Id, lineReason));
                return false;
            }

            result.Plans.Add(plan);
            return true;
        }

        /// <summary>
        /// Removes references that sit on top of one another. Two runs stacked at the same position in
        /// plan (one above the other) both offer a face at the same coordinate; leaving both in produces
        /// a zero-length segment, which Revit rejects - taking the whole chain with it in single-string
        /// mode. The higher-priority reference wins (a wall face or the seed run's own face over another
        /// run's face), and any run that loses its only reference is dropped from the covered list so it
        /// can still be dimensioned in its own right.
        /// </summary>
        private static List<DimensionReferenceCandidate> CollapseCoincident(
            List<DimensionReferenceCandidate> ordered,
            IList<string> covered)
        {
            List<DimensionReferenceCandidate> kept = new List<DimensionReferenceCandidate>();

            foreach (DimensionReferenceCandidate candidate in ordered)
            {
                if (kept.Count == 0)
                {
                    kept.Add(candidate);
                    continue;
                }

                DimensionReferenceCandidate last = kept[kept.Count - 1];
                if (Math.Abs(candidate.SortCoord - last.SortCoord) > MepDimensionGeometry.CoordinateMergeTolerance)
                {
                    kept.Add(candidate);
                    continue;
                }

                // Coincident - keep whichever is the better reference for this chain.
                DimensionReferenceCandidate loser = candidate;
                if (GetPriority(candidate) < GetPriority(last))
                {
                    kept[kept.Count - 1] = candidate;
                    loser = last;
                }

                DropUncoveredRun(kept, covered, loser);
            }

            return kept;
        }

        /// <summary>
        /// Removes a run's key from the covered list when no reference of that run survived, so it is
        /// not silently marked as documented by a dimension that does not touch it.
        /// </summary>
        private static void DropUncoveredRun(
            IEnumerable<DimensionReferenceCandidate> kept,
            IList<string> covered,
            DimensionReferenceCandidate loser)
        {
            if (covered == null || loser == null || !loser.IsMeasuredRun || string.IsNullOrEmpty(loser.ElementKey))
                return;

            bool stillPresent = kept.Any(c =>
                c.IsMeasuredRun && string.Equals(c.ElementKey, loser.ElementKey, StringComparison.Ordinal));

            if (!stillPresent)
                covered.Remove(loser.ElementKey);
        }

        private static bool IsBetween(RunFaces run, double referenceCoord, double seedNearCoord, bool negative)
        {
            double tolerance = MepDimensionGeometry.CoordinateMergeTolerance;

            if (negative)
            {
                double near = run.MinFace.SortCoord;
                return near > referenceCoord + tolerance && near <= seedNearCoord + tolerance;
            }

            double nearPositive = run.MaxFace.SortCoord;
            return nearPositive < referenceCoord - tolerance && nearPositive >= seedNearCoord - tolerance;
        }

        private static DimensionPlanFailure Failure(ElementId elementId, string reason)
        {
            return new DimensionPlanFailure
            {
                ElementId = elementId,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Could not build dimension references." : reason
            };
        }

        private enum ChainSide
        {
            Negative,
            Positive
        }

        private sealed class RunFaces
        {
            public string ElementKey { get; set; }
            public ElementId ElementId { get; set; }
            public DimensionReferenceCandidate MinFace { get; set; }
            public DimensionReferenceCandidate MaxFace { get; set; }

            /// <summary>True when the run contributed only a centreline (a round pipe, conduit or duct).</summary>
            public bool IsSingleReference { get; set; }
        }
    }
}
