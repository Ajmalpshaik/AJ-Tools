#region Metadata
/*
 * Tool Name     : Create Openings
 * File Name     : MepOpeningService.cs
 * Purpose       : Finds selected current or linked MEP crossings and creates direct openings in current-model hosts.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-06
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.MepOpenings, AJTools.Utils
 *
 * Input         : Selection-scope current or linked pipes, ducts, cable trays, and conduits plus saved opening settings.
 * Output        : Direct openings in current-model walls, floors/slabs, and beams.
 *
 * Notes         :
 * - Uses solid intersection when available, with existing-opening coverage support for reruns.
 * - Merges nearby selected crossings on the same host into one rectangular opening.
 * - Replaces nearby existing openings only when required to form the merged opening.
 * - Linked source elements are read only and transformed into current-model coordinates.
 * - Linked hosts are not modified; they require the later family opening mode.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 * v1.0.1 (2026-07-05) - Family opening elevation fallback now includes object height when the
 *                         family bounding box cannot expose the opening void height.
 * v1.0.2 (2026-07-06) - Cable tray fittings/runs are accepted as cable tray sources; family placement
 *                         can infer direction from connectors when a location curve is unavailable.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using AJTools.Models.MepOpenings;
using AJTools.Utils;
using RevitRefFace = Autodesk.Revit.Creation.eRefFace;

namespace AJTools.Services.MepOpenings
{
    internal sealed class MepOpeningService
    {
        private const double GeometryTolerance = 1e-7;
        private const double MinimumOpeningInternal = 0.01;
        private const double MinimumFamilyPlanDirectionLength = 1e-4;

        public MepOpeningRunResult CreateOpenings(
            Document doc,
            IList<Element> selectedElements,
            MepOpeningSettings settings)
        {
            IList<MepOpeningSourceElement> sources = selectedElements == null
                ? new List<MepOpeningSourceElement>()
                : selectedElements
                    .Where(element => element != null)
                    .Select(element => MepOpeningSourceElement.FromCurrent(doc, element))
                    .ToList();

            return CreateOpenings(doc, sources, settings);
        }

        public MepOpeningRunResult CreateOpenings(
            Document doc,
            IList<MepOpeningSourceElement> selectedSources,
            MepOpeningSettings settings)
        {
            var result = new MepOpeningRunResult();
            if (doc == null)
            {
                result.AddFailure("No active project document.");
                return result;
            }

            if (selectedSources == null || selectedSources.Count == 0)
            {
                result.AddSkip("No MEP elements selected.");
                return result;
            }

            settings = settings ?? MepOpeningSettings.CreateDefault();
            settings.Normalize();

            if (settings.CreationMode == MepOpeningCreationMode.FamilyOpening)
            {
                result.AddSkip("Family opening mode does not create direct openings.");
                return result;
            }

            result.SelectedCount = selectedSources.Count;
            var existingOpeningsByHost = CollectExistingOpeningsByHost(doc);
            var candidates = BuildOpeningCandidates(doc, selectedSources, settings, existingOpeningsByHost, result);

            if (candidates.Count == 0)
            {
                return result;
            }

            var groups = BuildOpeningGroups(candidates, settings);
            if (groups.Count == 0)
            {
                return result;
            }

            using (Transaction transaction = new Transaction(doc, "AJ Tools - Create Openings"))
            {
                transaction.Start();

                FailureHandlingOptions failureOptions = transaction.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new MepOpeningFailurePreprocessor());
                transaction.SetFailureHandlingOptions(failureOptions);

                foreach (OpeningGroup group in groups)
                {
                    CreateOpeningForGroup(doc, group, result);
                }

                if (result.OpeningsCreated > 0 || result.ExistingOpeningsReplaced > 0)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }
            }

            return result;
        }

        public MepOpeningRunResult CreateOpeningsFromHosts(
            Document doc,
            IList<Element> selectedHosts,
            MepOpeningSettings settings)
        {
            var result = new MepOpeningRunResult();
            if (doc == null)
            {
                result.AddFailure("No active project document.");
                return result;
            }

            if (selectedHosts == null || selectedHosts.Count == 0)
            {
                result.AddSkip("No opening hosts selected.");
                return result;
            }

            settings = settings ?? MepOpeningSettings.CreateDefault();
            settings.Normalize();

            if (settings.CreationMode == MepOpeningCreationMode.FamilyOpening)
            {
                result.AddSkip("Family opening mode does not create direct openings.");
                return result;
            }

            result.SelectedCount = selectedHosts.Count;
            var existingOpeningsByHost = CollectExistingOpeningsByHost(doc);
            var candidates = BuildOpeningCandidatesForHosts(doc, selectedHosts, settings, existingOpeningsByHost, result);

            if (candidates.Count == 0)
            {
                return result;
            }

            var groups = BuildOpeningGroups(candidates, settings);
            if (groups.Count == 0)
            {
                return result;
            }

            using (Transaction transaction = new Transaction(doc, "AJ Tools - Create Openings"))
            {
                transaction.Start();

                FailureHandlingOptions failureOptions = transaction.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new MepOpeningFailurePreprocessor());
                transaction.SetFailureHandlingOptions(failureOptions);

                foreach (OpeningGroup group in groups)
                {
                    CreateOpeningForGroup(doc, group, result);
                }

                if (result.OpeningsCreated > 0 || result.ExistingOpeningsReplaced > 0)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }
            }

            return result;
        }

        public MepOpeningRunResult CreateOpeningFamilies(
            Document doc,
            IList<MepOpeningSourceElement> selectedSources,
            MepOpeningSettings settings)
        {
            var result = new MepOpeningRunResult();
            if (doc == null)
            {
                result.AddFailure("No active project document.");
                return result;
            }

            if (selectedSources == null || selectedSources.Count == 0)
            {
                result.AddSkip("No MEP elements selected.");
                return result;
            }

            settings = settings ?? MepOpeningSettings.CreateDefault();
            settings.Normalize();

            result.SelectedCount = selectedSources.Count;
            var candidates = BuildFamilyOpeningCandidates(doc, selectedSources, settings, result);
            if (candidates.Count == 0)
            {
                return result;
            }

            var groups = BuildFamilyOpeningGroups(candidates, settings);
            if (groups.Count == 0)
            {
                return result;
            }

            using (Transaction transaction = new Transaction(doc, "AJ Tools - Place Opening Families"))
            {
                transaction.Start();

                foreach (FamilyOpeningGroup group in groups)
                {
                    PlaceOpeningFamilyForGroup(doc, group, result);
                }

                if (result.OpeningsCreated > 0)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }
            }

            return result;
        }

        public MepOpeningRunResult CreateOpeningFamiliesFromHosts(
            Document doc,
            IList<Element> selectedHosts,
            MepOpeningSettings settings)
        {
            var result = new MepOpeningRunResult();
            if (doc == null)
            {
                result.AddFailure("No active project document.");
                return result;
            }

            if (selectedHosts == null || selectedHosts.Count == 0)
            {
                result.AddSkip("No opening hosts selected.");
                return result;
            }

            settings = settings ?? MepOpeningSettings.CreateDefault();
            settings.Normalize();

            result.SelectedCount = selectedHosts.Count;
            var candidates = BuildFamilyOpeningCandidatesForHosts(doc, selectedHosts, settings, result);
            if (candidates.Count == 0)
            {
                return result;
            }

            var groups = BuildFamilyOpeningGroups(candidates, settings);
            if (groups.Count == 0)
            {
                return result;
            }

            using (Transaction transaction = new Transaction(doc, "AJ Tools - Place Opening Families"))
            {
                transaction.Start();

                foreach (FamilyOpeningGroup group in groups)
                {
                    PlaceOpeningFamilyForGroup(doc, group, result);
                }

                if (result.OpeningsCreated > 0)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }
            }

            return result;
        }

        private static List<FamilyOpeningCandidate> BuildFamilyOpeningCandidates(
            Document doc,
            IList<MepOpeningSourceElement> selectedSources,
            MepOpeningSettings settings,
            MepOpeningRunResult result)
        {
            var candidates = new List<FamilyOpeningCandidate>();
            double mergeDistanceInternal = MmToInternal(settings.MergeDistanceMm);

            if (!settings.UseCurrentModelHosts && !settings.UseLinkedModelHosts)
            {
                result.AddSkip("Select a current or linked wall host option in Opening Settings.");
                return candidates;
            }

            foreach (MepOpeningSourceElement source in selectedSources)
            {
                MepOpeningElementKind elementKind;
                MepOpeningElementRule rule;
                OpeningSize objectSize;
                double cutoutBufferInternal;
                XYZ sourceDirection;
                XYZ sourcePlanDirection;
                if (!TryPrepareFamilySource(
                    doc,
                    source,
                    settings,
                    result,
                    out elementKind,
                    out rule,
                    out objectSize,
                    out cutoutBufferInternal,
                    out sourceDirection,
                    out sourcePlanDirection))
                {
                    continue;
                }

                Element mepElement = source.Element;
                Transform sourceTransform = source.TransformToHost ?? Transform.Identity;
                OpeningBox mepBox = GetElementBox(mepElement, sourceTransform);
                if (mepBox == null)
                {
                    result.AddSkip("Element geometry box could not be read: " + GetElementLabel(mepElement));
                    continue;
                }

                double searchExpansion = Math.Max(
                    mergeDistanceInternal,
                    Math.Max(objectSize.WidthInternal, objectSize.HeightInternal) + (2.0 * cutoutBufferInternal)) + MmToInternal(50);

                List<FamilyOpeningHost> hostCandidates = CollectFamilyWallHostCandidates(
                    doc,
                    mepBox.Expand(searchExpansion),
                    settings);

                bool foundCandidateForElement = false;
                foreach (FamilyOpeningHost host in hostCandidates)
                {
                    FamilyOpeningCandidate candidate;
                    if (!TryCreateFamilyOpeningCandidate(
                        doc,
                        source,
                        mepBox,
                        elementKind,
                        rule,
                        objectSize,
                        cutoutBufferInternal,
                        sourceDirection,
                        sourcePlanDirection,
                        host,
                        out candidate,
                        result))
                    {
                        continue;
                    }

                    candidates.Add(candidate);
                    result.OpeningRequests++;
                    result.HostIntersectionsChecked++;
                    foundCandidateForElement = true;
                }

                if (!foundCandidateForElement)
                {
                    result.AddSkip("No wall crossing found for family opening: " + GetElementLabel(mepElement));
                }
            }

            return candidates;
        }

        private static List<FamilyOpeningCandidate> BuildFamilyOpeningCandidatesForHosts(
            Document doc,
            IList<Element> selectedHosts,
            MepOpeningSettings settings,
            MepOpeningRunResult result)
        {
            var candidates = new List<FamilyOpeningCandidate>();
            double mergeDistanceInternal = MmToInternal(settings.MergeDistanceMm);

            if (settings.UseLinkedModelHosts && !settings.UseCurrentModelHosts)
            {
                result.AddSkip("Linked wall host selection is not available in Host Elements mode yet. Use Source Elements mode.");
                return candidates;
            }

            foreach (Element selectedHost in selectedHosts)
            {
                if (selectedHost == null || !selectedHost.IsValidObject)
                {
                    result.AddSkip("Invalid selected host.");
                    continue;
                }

                MepOpeningHostKind hostKind;
                if (!MepOpeningSelectionFilter.IsSupportedHost(selectedHost, out hostKind) ||
                    hostKind != MepOpeningHostKind.Wall)
                {
                    result.AddSkip("Family opening mode currently supports wall hosts only: " + GetElementLabel(selectedHost));
                    continue;
                }

                if (IsOwnedByOtherUser(doc, selectedHost))
                {
                    result.AddSkip("Host is owned by another user: " + GetElementLabel(selectedHost));
                    continue;
                }

                OpeningBox hostBox = GetElementBox(selectedHost);
                if (hostBox == null)
                {
                    result.AddSkip("Host geometry box could not be read: " + GetElementLabel(selectedHost));
                    continue;
                }

                var host = FamilyOpeningHost.FromCurrent(doc, selectedHost);
                List<MepOpeningSourceElement> sourceCandidates = CollectMepSourceCandidates(
                    doc,
                    hostBox.Expand(Math.Max(mergeDistanceInternal, MmToInternal(50))),
                    settings);

                bool foundCandidateForHost = false;
                foreach (MepOpeningSourceElement source in sourceCandidates)
                {
                    MepOpeningElementKind elementKind;
                    MepOpeningElementRule rule;
                    OpeningSize objectSize;
                    double cutoutBufferInternal;
                    XYZ sourceDirection;
                    XYZ sourcePlanDirection;
                    if (!TryPrepareFamilySource(
                        doc,
                        source,
                        settings,
                        result,
                        out elementKind,
                        out rule,
                        out objectSize,
                        out cutoutBufferInternal,
                        out sourceDirection,
                        out sourcePlanDirection))
                    {
                        continue;
                    }

                    Element mepElement = source.Element;
                    OpeningBox mepBox = GetElementBox(mepElement, source.TransformToHost ?? Transform.Identity);
                    if (mepBox == null || !mepBox.IntersectsOrWithin(hostBox, GeometryTolerance))
                    {
                        continue;
                    }

                    FamilyOpeningCandidate candidate;
                    if (!TryCreateFamilyOpeningCandidate(
                        doc,
                        source,
                        mepBox,
                        elementKind,
                        rule,
                        objectSize,
                        cutoutBufferInternal,
                        sourceDirection,
                        sourcePlanDirection,
                        host,
                        out candidate,
                        result))
                    {
                        continue;
                    }

                    candidates.Add(candidate);
                    result.OpeningRequests++;
                    result.HostIntersectionsChecked++;
                    foundCandidateForHost = true;
                }

                if (!foundCandidateForHost)
                {
                    result.AddSkip("No duct or cable tray crossing found: " + GetElementLabel(selectedHost));
                }
            }

            return candidates;
        }

        private static bool TryPrepareFamilySource(
            Document hostDoc,
            MepOpeningSourceElement source,
            MepOpeningSettings settings,
            MepOpeningRunResult result,
            out MepOpeningElementKind elementKind,
            out MepOpeningElementRule rule,
            out OpeningSize objectSize,
            out double cutoutBufferInternal,
            out XYZ sourceDirection,
            out XYZ sourcePlanDirection)
        {
            elementKind = MepOpeningElementKind.Duct;
            rule = null;
            objectSize = null;
            cutoutBufferInternal = 0;
            sourceDirection = null;
            sourcePlanDirection = null;

            Element mepElement = source?.Element;
            if (mepElement == null || !mepElement.IsValidObject)
            {
                result.AddSkip("Invalid selected element.");
                return false;
            }

            if (source.IsLinked && !settings.UseLinkedModelSources)
            {
                result.AddSkip("Linked source element skipped by settings.");
                return false;
            }

            if (!source.IsLinked && !settings.UseCurrentModelSources)
            {
                result.AddSkip("Current-model source element skipped by settings.");
                return false;
            }

            if (!MepOpeningSelectionFilter.TryGetElementKind(mepElement, out elementKind))
            {
                result.AddSkip("Wrong element selected.");
                return false;
            }

            if (elementKind != MepOpeningElementKind.Duct &&
                elementKind != MepOpeningElementKind.CableTray)
            {
                result.AddSkip("Family opening mode currently supports ducts and cable trays only: " + GetElementLabel(mepElement));
                return false;
            }

            rule = settings.GetRule(elementKind);
            if (rule != null && !rule.IsIncluded)
            {
                result.AddSkip("Element type is unchecked in Opening Settings: " + GetElementLabel(mepElement));
                return false;
            }

            if (rule == null || string.IsNullOrWhiteSpace(rule.VerticalOpeningFamilyName))
            {
                result.AddSkip("Select a vertical Generic Model opening family for " + GetElementKindLabel(elementKind) + " in Opening Settings.");
                return false;
            }

            Document sourceDoc = source.SourceDocument ?? hostDoc;
            if (!TryGetFamilyObjectSize(sourceDoc, mepElement, elementKind, settings.IncludeInsulation, out objectSize))
            {
                result.AddSkip("Element size could not be read: " + GetElementLabel(mepElement));
                return false;
            }

            if (!TryGetSourceDirection(source, out sourceDirection))
            {
                result.AddSkip("Element direction could not be read for family rotation: " + GetElementLabel(mepElement));
                return false;
            }

            if (!TryGetPlanDirection(sourceDirection, out sourcePlanDirection))
            {
                result.AddSkip("Vertical duct/cable tray family openings are not included in this update: " + GetElementLabel(mepElement));
                return false;
            }

            cutoutBufferInternal = MmToInternal(rule?.CutoutBufferMm ?? 0);
            result.SupportedMepCount++;
            return true;
        }

        private static bool TryCreateFamilyOpeningCandidate(
            Document doc,
            MepOpeningSourceElement source,
            OpeningBox mepBox,
            MepOpeningElementKind elementKind,
            MepOpeningElementRule rule,
            OpeningSize objectSize,
            double cutoutBufferInternal,
            XYZ sourceDirection,
            XYZ sourcePlanDirection,
            FamilyOpeningHost host,
            out FamilyOpeningCandidate candidate,
            MepOpeningRunResult result)
        {
            candidate = null;
            Element mepElement = source?.Element;
            if (doc == null || mepElement == null || host == null || host.Element == null)
            {
                return false;
            }

            if (!host.IsLinked && IsOwnedByOtherUser(doc, host.Element))
            {
                result.AddSkip("Host is owned by another user: " + GetElementLabel(host.Element));
                return false;
            }

            OpeningBox hostBox = GetElementBox(host.Element, host.TransformToHost);
            if (hostBox == null || mepBox == null || !mepBox.IntersectsOrWithin(hostBox, GeometryTolerance))
            {
                return false;
            }

            OpeningBox intersectionBox;
            bool solidIntersection = TryGetSolidIntersectionBox(
                mepElement,
                source.TransformToHost ?? Transform.Identity,
                host.Element,
                host.TransformToHost,
                out intersectionBox);
            if (intersectionBox == null)
            {
                intersectionBox = mepBox.GetOverlap(hostBox);
            }

            if (intersectionBox == null || !solidIntersection)
            {
                return false;
            }

            var cutoutSize = new OpeningSize
            {
                WidthInternal = objectSize.WidthInternal + (2.0 * cutoutBufferInternal),
                HeightInternal = objectSize.HeightInternal + (2.0 * cutoutBufferInternal),
                RequestedShape = MepOpeningShape.Rectangle
            };

            OpeningBox candidateBox = BuildCandidateBox(intersectionBox, cutoutSize);
            XYZ placementPoint;
            if (!TryGetSourceCenterlinePoint(source, intersectionBox.Center, out placementPoint))
            {
                placementPoint = candidateBox.Center;
            }

            XYZ wallDirection;
            if (!TryGetHostWallDirection(host, out wallDirection))
            {
                result.AddSkip("Wall direction could not be read: " + host.Label);
                return false;
            }

            double wallWidthInternal;
            if (!TryGetWallWidthAlongDirection(host, placementPoint, sourceDirection, out wallWidthInternal))
            {
                result.AddSkip("Wall width could not be read from two wall-face hits: " + host.Label);
                return false;
            }

            candidate = new FamilyOpeningCandidate
            {
                MepElementId = mepElement.Id,
                ElementKind = elementKind,
                Host = host,
                HostKey = host.Key,
                FamilyDisplayName = rule.VerticalOpeningFamilyName,
                Box = candidateBox,
                PlacementPoint = placementPoint,
                ObjectWidthInternal = objectSize.WidthInternal,
                ObjectHeightInternal = objectSize.HeightInternal,
                CutoutBufferInternal = cutoutBufferInternal,
                WallWidthInternal = wallWidthInternal,
                SourcePlanDirection = sourcePlanDirection,
                WallDirection = wallDirection,
                PreferredLevelId = ResolvePreferredPlacementLevelId(doc, source, placementPoint)
            };

            return true;
        }

        private static List<FamilyOpeningHost> CollectFamilyWallHostCandidates(
            Document hostDoc,
            OpeningBox hostSearchBox,
            MepOpeningSettings settings)
        {
            var hosts = new List<FamilyOpeningHost>();
            if (hostDoc == null || hostSearchBox == null || settings == null)
            {
                return hosts;
            }

            if (settings.UseCurrentModelHosts)
            {
                AddFamilyWallHostCandidates(hostDoc, Transform.Identity, null, hostSearchBox, hosts);
            }

            if (settings.UseLinkedModelHosts)
            {
                RevitLinkInstance linkInstance = FindLinkInstance(hostDoc, settings.HostLinkInstanceUniqueId);
                Document linkDoc = linkInstance == null ? null : linkInstance.GetLinkDocument();
                if (linkDoc != null)
                {
                    Transform linkToHost = linkInstance.GetTotalTransform() ?? Transform.Identity;
                    OpeningBox linkSearchBox = hostSearchBox.TransformBy(linkToHost.Inverse);
                    AddFamilyWallHostCandidates(linkDoc, linkToHost, linkInstance, linkSearchBox, hosts);
                }
            }

            return hosts
                .Where(host => host != null && host.Element != null)
                .GroupBy(host => host.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static void AddFamilyWallHostCandidates(
            Document sourceDoc,
            Transform transformToHost,
            RevitLinkInstance linkInstance,
            OpeningBox searchBox,
            ICollection<FamilyOpeningHost> hosts)
        {
            if (sourceDoc == null || searchBox == null || hosts == null)
            {
                return;
            }

            var filter = new BoundingBoxIntersectsFilter(searchBox.ToOutline());
            foreach (Element element in new FilteredElementCollector(sourceDoc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .WherePasses(filter))
            {
                if (linkInstance == null)
                {
                    hosts.Add(FamilyOpeningHost.FromCurrent(sourceDoc, element));
                }
                else
                {
                    hosts.Add(FamilyOpeningHost.FromLinked(
                        sourceDoc,
                        element,
                        linkInstance,
                        transformToHost,
                        GetCleanLinkName(linkInstance, sourceDoc)));
                }
            }
        }

        private static List<FamilyOpeningGroup> BuildFamilyOpeningGroups(
            IList<FamilyOpeningCandidate> candidates,
            MepOpeningSettings settings)
        {
            var groups = candidates
                .Select(candidate => new FamilyOpeningGroup(candidate))
                .ToList();

            double mergeDistanceInternal = MmToInternal(settings.MergeDistanceMm);
            bool merged;

            do
            {
                merged = false;
                for (int i = 0; i < groups.Count; i++)
                {
                    for (int j = i + 1; j < groups.Count; j++)
                    {
                        if (!groups[i].CanMergeWith(groups[j], mergeDistanceInternal))
                        {
                            continue;
                        }

                        groups[i].Merge(groups[j]);
                        groups.RemoveAt(j);
                        merged = true;
                        break;
                    }

                    if (merged)
                    {
                        break;
                    }
                }
            } while (merged);

            return groups;
        }

        private static void PlaceOpeningFamilyForGroup(
            Document doc,
            FamilyOpeningGroup group,
            MepOpeningRunResult result)
        {
            if (doc == null || group == null)
            {
                result.AddFailure("Opening family placement data is missing.");
                return;
            }

            FamilySymbol symbol = ResolveGenericModelOpeningSymbol(doc, group.FamilyDisplayName, result);
            if (symbol == null)
            {
                return;
            }

            try
            {
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                XYZ point = group.PlacementPoint;
                Level placementLevel = ResolvePlacementLevel(doc, point, group.PreferredLevelId);
                FamilyInstance instance = CreateOpeningFamilyInstance(doc, symbol, point, placementLevel);
                if (instance == null)
                {
                    result.AddFailure("Opening family could not be placed: " + group.FamilyDisplayName);
                    return;
                }

                string parameterError;
                if (!TrySetOpeningFamilyParameters(instance, symbol, group, out parameterError) ||
                    !TryApplyOpeningFamilyLevelAndElevation(
                        doc,
                        instance,
                        placementLevel,
                        point,
                        group.ObjectHeightInternal,
                        group.CutoutBufferInternal,
                        out parameterError) ||
                    !TryRotateOpeningFamily(doc, instance, point, group.SourcePlanDirection, out parameterError))
                {
                    try
                    {
                        doc.Delete(instance.Id);
                    }
                    catch
                    {
                        // Keep the original placement error; delete failure is secondary.
                    }

                    result.AddFailure(parameterError);
                    return;
                }

                result.OpeningsCreated++;
            }
            catch (Exception ex)
            {
                result.AddFailure("Opening family placement failed: " + ex.Message);
            }
        }

        private static FamilySymbol ResolveGenericModelOpeningSymbol(
            Document doc,
            string displayName,
            MepOpeningRunResult result)
        {
            if (doc == null || string.IsNullOrWhiteSpace(displayName))
            {
                result.AddFailure("Opening family is not selected in Opening Settings.");
                return null;
            }

            List<FamilySymbol> matches = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(IsGenericModelOpeningSymbol)
                .Where(symbol => string.Equals(GetFamilySymbolDisplayName(symbol), displayName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count == 0)
            {
                result.AddFailure("Selected Generic Model opening family is missing: " + displayName);
                return null;
            }

            result.AddFailure("Selected Generic Model opening family is ambiguous: " + displayName);
            return null;
        }

        private static FamilyInstance CreateOpeningFamilyInstance(Document doc, FamilySymbol symbol, XYZ point, Level level)
        {
            if (doc == null || symbol == null || point == null || level == null)
            {
                return null;
            }

            try
            {
                return doc.Create.NewFamilyInstance(point, symbol, level, StructuralType.NonStructural);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryApplyOpeningFamilyLevelAndElevation(
            Document doc,
            FamilyInstance instance,
            Level level,
            XYZ targetPoint,
            double objectHeightInternal,
            double cutoutBufferInternal,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (doc == null || instance == null || level == null || targetPoint == null)
            {
                errorMessage = "Opening family level/elevation data is missing.";
                return false;
            }

            TrySetFamilyLevelParameter(instance, level.Id);
            doc.Regenerate();

            double familyCenterOffsetFromLocation = 0.0;
            double fallbackCenterOffset = GetOpeningFamilyFallbackCenterOffset(
                objectHeightInternal,
                cutoutBufferInternal);
            XYZ currentLocationPoint;
            double currentFamilyCenterZ;
            if (TryGetFamilyInstancePoint(instance, out currentLocationPoint) &&
                TryGetFamilyVerticalCenterZ(instance, out currentFamilyCenterZ))
            {
                familyCenterOffsetFromLocation = currentFamilyCenterZ - currentLocationPoint.Z;
            }
            else if (fallbackCenterOffset > GeometryTolerance)
            {
                familyCenterOffsetFromLocation = fallbackCenterOffset;
            }

            double targetLocationZ = targetPoint.Z - familyCenterOffsetFromLocation;
            double offset = targetLocationZ - level.Elevation;
            Parameter offsetParameter = GetWritableOpeningElevationParameter(instance);
            if (offsetParameter == null)
            {
                errorMessage = "Opening family elevation offset parameter is missing or read-only.";
                return false;
            }

            if (!TrySetDoubleParameter(offsetParameter, offset, out errorMessage))
            {
                return false;
            }

            doc.Regenerate();

            double actualAlignmentZ;
            if (!TryGetOpeningFamilyAlignmentZ(instance, fallbackCenterOffset, out actualAlignmentZ))
            {
                errorMessage = "Opening family vertical center could not be read after level placement.";
                return false;
            }

            double zDelta = targetPoint.Z - actualAlignmentZ;
            if (Math.Abs(zDelta) <= MmToInternal(1))
            {
                return true;
            }

            if (!TrySetDoubleParameter(offsetParameter, offset + zDelta, out errorMessage))
            {
                return false;
            }

            doc.Regenerate();

            if (!TryGetOpeningFamilyAlignmentZ(instance, fallbackCenterOffset, out actualAlignmentZ) ||
                Math.Abs(targetPoint.Z - actualAlignmentZ) > MmToInternal(1))
            {
                errorMessage = "Opening family vertical center did not match the wall intersection point after offset update.";
                return false;
            }

            return true;
        }

        private static bool TryRotateOpeningFamily(
            Document doc,
            FamilyInstance instance,
            XYZ point,
            XYZ sourcePlanDirection,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (doc == null || instance == null || sourcePlanDirection == null)
            {
                errorMessage = "Opening family could not be rotated because placement direction was missing.";
                return false;
            }

            double angle = Math.Atan2(sourcePlanDirection.Y, sourcePlanDirection.X) + (Math.PI / 2.0);
            try
            {
                Line axis = Line.CreateUnbound(point, XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, instance.Id, axis, angle);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Opening family rotation failed: " + ex.Message;
                return false;
            }
        }

        private static bool TrySetOpeningFamilyParameters(
            FamilyInstance instance,
            FamilySymbol symbol,
            FamilyOpeningGroup group,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (instance == null || symbol == null || group == null)
            {
                errorMessage = "Opening family parameter data is missing.";
                return false;
            }

            if (!TrySetFamilyDoubleParameter(instance, symbol, "Object Width", group.ObjectWidthInternal, out errorMessage) ||
                !TrySetFamilyDoubleParameter(instance, symbol, "Object Height", group.ObjectHeightInternal, out errorMessage) ||
                !TrySetFamilyDoubleParameter(instance, symbol, "Cutout Buffer", group.CutoutBufferInternal, out errorMessage) ||
                !TrySetFamilyDoubleParameter(instance, symbol, "Wall Width", group.WallWidthInternal, out errorMessage))
            {
                return false;
            }

            return true;
        }

        private static bool TrySetFamilyDoubleParameter(
            FamilyInstance instance,
            FamilySymbol symbol,
            string parameterName,
            double value,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            Parameter parameter = FindWritableParameter(instance, parameterName) ??
                                  FindWritableParameter(symbol, parameterName);
            if (parameter == null)
            {
                errorMessage = "Opening family parameter is missing or read-only: " + parameterName;
                return false;
            }

            if (parameter.StorageType != StorageType.Double)
            {
                errorMessage = "Opening family parameter must be a length/number parameter: " + parameterName;
                return false;
            }

            try
            {
                parameter.Set(value);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Opening family parameter could not be updated: " + parameterName + " - " + ex.Message;
                return false;
            }
        }

        private static Parameter FindWritableParameter(Element element, string parameterName)
        {
            if (element == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return null;
            }

            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter == null ||
                    parameter.IsReadOnly ||
                    parameter.Definition == null ||
                    !string.Equals(parameter.Definition.Name, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return parameter;
            }

            return null;
        }

        private static void TrySetFamilyLevelParameter(FamilyInstance instance, ElementId levelId)
        {
            if (instance == null || levelId == null || levelId == ElementId.InvalidElementId)
            {
                return;
            }

            BuiltInParameter[] levelParameters =
            {
                BuiltInParameter.FAMILY_LEVEL_PARAM,
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_LEVEL_PARAM
            };

            foreach (BuiltInParameter parameterId in levelParameters)
            {
                Parameter parameter = instance.get_Parameter(parameterId);
                if (parameter == null ||
                    parameter.IsReadOnly ||
                    parameter.StorageType != StorageType.ElementId)
                {
                    continue;
                }

                try
                {
                    parameter.Set(levelId);
                    return;
                }
                catch
                {
                    // Try the next level parameter.
                }
            }
        }

        private static Parameter GetWritableOpeningElevationParameter(FamilyInstance instance)
        {
            if (instance == null)
            {
                return null;
            }

            Parameter builtInOffset = instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
            if (builtInOffset != null &&
                !builtInOffset.IsReadOnly &&
                builtInOffset.StorageType == StorageType.Double)
            {
                return builtInOffset;
            }

            string[] names =
            {
                "Elevation from Level",
                "Offset",
                "Default Elevation"
            };

            foreach (string name in names)
            {
                Parameter parameter = FindWritableParameter(instance, name);
                if (parameter != null && parameter.StorageType == StorageType.Double)
                {
                    return parameter;
                }
            }

            return null;
        }

        private static bool TrySetDoubleParameter(Parameter parameter, double value, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
            {
                errorMessage = "Opening family elevation offset parameter is missing or read-only.";
                return false;
            }

            try
            {
                parameter.Set(value);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Opening family elevation offset could not be updated: " + ex.Message;
                return false;
            }
        }

        private static bool TryGetFamilyInstancePoint(FamilyInstance instance, out XYZ point)
        {
            point = null;
            var locationPoint = instance?.Location as LocationPoint;
            if (locationPoint == null)
            {
                return false;
            }

            point = locationPoint.Point;
            return point != null;
        }

        private static bool TryGetOpeningFamilyAlignmentZ(
            FamilyInstance instance,
            double fallbackCenterOffsetFromLocation,
            out double z)
        {
            if (TryGetFamilyVerticalCenterZ(instance, out z))
            {
                return true;
            }

            XYZ point;
            if (TryGetFamilyInstancePoint(instance, out point))
            {
                z = point.Z + Math.Max(0, fallbackCenterOffsetFromLocation);
                return true;
            }

            z = 0;
            return false;
        }

        private static double GetOpeningFamilyFallbackCenterOffset(
            double objectHeightInternal,
            double cutoutBufferInternal)
        {
            double safeObjectHeight = Math.Max(0, objectHeightInternal);
            double safeBuffer = Math.Max(0, cutoutBufferInternal);
            return (safeObjectHeight / 2.0) + safeBuffer;
        }

        private static bool TryGetFamilyVerticalCenterZ(FamilyInstance instance, out double z)
        {
            z = 0;
            if (instance == null)
            {
                return false;
            }

            BoundingBoxXYZ box = instance.get_BoundingBox(null);
            if (box == null || box.Min == null || box.Max == null)
            {
                return false;
            }

            if (Math.Abs(box.Max.Z - box.Min.Z) <= MmToInternal(1))
            {
                return false;
            }

            z = (box.Min.Z + box.Max.Z) / 2.0;
            return true;
        }

        private static ElementId ResolvePreferredPlacementLevelId(
            Document hostDoc,
            MepOpeningSourceElement source,
            XYZ placementPoint)
        {
            if (hostDoc == null)
            {
                return ElementId.InvalidElementId;
            }

            Level sourceLevel;
            if (source != null &&
                !source.IsLinked &&
                TryResolveSourceElementLevel(hostDoc, source.Element, out sourceLevel))
            {
                return sourceLevel.Id;
            }

            Level fallback = ResolvePlacementLevel(hostDoc, placementPoint, ElementId.InvalidElementId);
            return fallback == null ? ElementId.InvalidElementId : fallback.Id;
        }

        private static bool TryResolveSourceElementLevel(Document doc, Element element, out Level level)
        {
            level = null;
            if (doc == null || element == null)
            {
                return false;
            }

            var mepCurve = element as MEPCurve;
            if (mepCurve != null && mepCurve.ReferenceLevel != null)
            {
                level = mepCurve.ReferenceLevel;
                return true;
            }

            ElementId levelId;
            BuiltInParameter[] levelParameters =
            {
                BuiltInParameter.RBS_START_LEVEL_PARAM,
                BuiltInParameter.LEVEL_PARAM,
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.FAMILY_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_LEVEL_PARAM
            };

            foreach (BuiltInParameter parameterId in levelParameters)
            {
                if (TryGetLevelIdFromParameter(element, parameterId, out levelId))
                {
                    level = doc.GetElement(levelId) as Level;
                    if (level != null)
                    {
                        return true;
                    }
                }
            }

            try
            {
                if (element.LevelId != null && element.LevelId != ElementId.InvalidElementId)
                {
                    level = doc.GetElement(element.LevelId) as Level;
                    return level != null;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryGetLevelIdFromParameter(Element element, BuiltInParameter parameterId, out ElementId levelId)
        {
            levelId = ElementId.InvalidElementId;
            Parameter parameter = element?.get_Parameter(parameterId);
            if (parameter == null || parameter.StorageType != StorageType.ElementId)
            {
                return false;
            }

            ElementId value = parameter.AsElementId();
            if (value == null || value == ElementId.InvalidElementId)
            {
                return false;
            }

            levelId = value;
            return true;
        }

        private static Level ResolvePlacementLevel(Document doc, XYZ point, ElementId preferredLevelId)
        {
            if (doc == null)
            {
                return null;
            }

            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ToList();

            if (levels.Count == 0)
            {
                return null;
            }

            if (preferredLevelId != null && preferredLevelId != ElementId.InvalidElementId)
            {
                Level preferredLevel = levels.FirstOrDefault(level => level.Id == preferredLevelId);
                if (preferredLevel != null)
                {
                    return preferredLevel;
                }
            }

            if (point == null)
            {
                return levels[0];
            }

            Level below = levels
                .Where(level => level.Elevation <= point.Z + GeometryTolerance)
                .OrderByDescending(level => level.Elevation)
                .FirstOrDefault();

            return below ?? levels
                .OrderBy(level => Math.Abs(level.Elevation - point.Z))
                .FirstOrDefault();
        }

        private static bool TryGetFamilyObjectSize(
            Document doc,
            Element element,
            MepOpeningElementKind kind,
            bool includeInsulation,
            out OpeningSize objectSize)
        {
            objectSize = null;
            double width;
            double height;
            double diameter;

            switch (kind)
            {
                case MepOpeningElementKind.Duct:
                    if (TryGetDoubleParameter(element, BuiltInParameter.RBS_CURVE_WIDTH_PARAM, out width) &&
                        TryGetDoubleParameter(element, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM, out height))
                    {
                        break;
                    }

                    if (!TryGetDoubleParameter(element, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM, out diameter))
                    {
                        return false;
                    }

                    width = diameter;
                    height = diameter;
                    break;

                case MepOpeningElementKind.CableTray:
                    if (!TryGetCableTraySize(element, out width, out height))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }

            double insulationThickness = includeInsulation && kind == MepOpeningElementKind.Duct
                ? GetInsulationThickness(doc, element, kind)
                : 0;

            objectSize = new OpeningSize
            {
                WidthInternal = Math.Max(width + (2.0 * insulationThickness), MinimumOpeningInternal),
                HeightInternal = Math.Max(height + (2.0 * insulationThickness), MinimumOpeningInternal),
                RequestedShape = MepOpeningShape.Rectangle
            };

            return true;
        }

        private static bool TryGetSourceDirection(MepOpeningSourceElement source, out XYZ direction)
        {
            direction = null;
            Element element = source?.Element;
            Transform transform = source?.TransformToHost ?? Transform.Identity;

            var locationCurve = element?.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve != null)
            {
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                XYZ rawDirection = end - start;
                if (rawDirection.GetLength() > GeometryTolerance)
                {
                    XYZ transformed = transform.OfVector(rawDirection);
                    if (transformed.GetLength() > GeometryTolerance)
                    {
                        direction = transformed.Normalize();
                        return true;
                    }
                }
            }

            return TryGetConnectorDirection(element, transform, out direction) ||
                   TryGetBoundingBoxPlanDirection(element, transform, out direction);
        }

        private static bool TryGetConnectorDirection(Element element, Transform transform, out XYZ direction)
        {
            direction = null;
            ConnectorManager connectorManager = GetConnectorManager(element);
            if (connectorManager == null)
            {
                return false;
            }

            var points = new List<XYZ>();
            try
            {
                foreach (Connector connector in connectorManager.Connectors)
                {
                    if (connector == null ||
                        !connector.IsValidObject ||
                        connector.ConnectorType != ConnectorType.End)
                    {
                        continue;
                    }

                    XYZ origin = connector.Origin;
                    if (origin != null)
                    {
                        points.Add(origin);
                    }
                }
            }
            catch
            {
                return false;
            }

            if (points.Count < 2)
            {
                return false;
            }

            XYZ first = null;
            XYZ second = null;
            double maxDistance = 0;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double distance = points[i].DistanceTo(points[j]);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        first = points[i];
                        second = points[j];
                    }
                }
            }

            if (first == null || second == null || maxDistance <= GeometryTolerance)
            {
                return false;
            }

            XYZ transformed = (transform ?? Transform.Identity).OfVector(second - first);
            if (transformed.GetLength() <= GeometryTolerance)
            {
                return false;
            }

            direction = transformed.Normalize();
            return true;
        }

        private static bool TryGetBoundingBoxPlanDirection(Element element, Transform transform, out XYZ direction)
        {
            direction = null;
            OpeningBox box = GetElementBox(element, transform ?? Transform.Identity);
            if (box == null)
            {
                return false;
            }

            double xRange = box.Max.X - box.Min.X;
            double yRange = box.Max.Y - box.Min.Y;
            if (Math.Max(xRange, yRange) <= GeometryTolerance)
            {
                return false;
            }

            direction = xRange >= yRange ? XYZ.BasisX : XYZ.BasisY;
            return true;
        }

        private static ConnectorManager GetConnectorManager(Element element)
        {
            try
            {
                var mepCurve = element as MEPCurve;
                if (mepCurve != null)
                {
                    return mepCurve.ConnectorManager;
                }

                var familyInstance = element as FamilyInstance;
                return familyInstance?.MEPModel?.ConnectorManager;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetSourceCenterlinePoint(
            MepOpeningSourceElement source,
            XYZ hostPoint,
            out XYZ centerlinePoint)
        {
            centerlinePoint = null;
            Element element = source?.Element;
            var locationCurve = element?.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null || hostPoint == null)
            {
                return false;
            }

            try
            {
                Transform sourceToHost = source.TransformToHost ?? Transform.Identity;
                XYZ sourcePoint = sourceToHost.Inverse.OfPoint(hostPoint);
                IntersectionResult projected = curve.Project(sourcePoint);
                if (projected == null || projected.XYZPoint == null)
                {
                    return false;
                }

                centerlinePoint = sourceToHost.OfPoint(projected.XYZPoint);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetPlanDirection(XYZ direction, out XYZ planDirection)
        {
            planDirection = null;
            if (direction == null)
            {
                return false;
            }

            XYZ projected = new XYZ(direction.X, direction.Y, 0);
            if (projected.GetLength() <= MinimumFamilyPlanDirectionLength)
            {
                return false;
            }

            planDirection = projected.Normalize();
            return true;
        }

        private static bool TryGetHostWallDirection(FamilyOpeningHost host, out XYZ wallDirection)
        {
            wallDirection = null;
            if (host == null || host.Element == null)
            {
                return false;
            }

            var locationCurve = host.Element.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null)
            {
                return false;
            }

            XYZ direction = curve.GetEndPoint(1) - curve.GetEndPoint(0);
            Transform transform = host.TransformToHost ?? Transform.Identity;
            XYZ transformed = transform.OfVector(direction);
            XYZ projected = new XYZ(transformed.X, transformed.Y, 0);
            if (projected.GetLength() <= GeometryTolerance)
            {
                return false;
            }

            wallDirection = projected.Normalize();
            return true;
        }

        private static bool TryGetWallWidthAlongDirection(
            FamilyOpeningHost host,
            XYZ center,
            XYZ direction,
            out double wallWidthInternal)
        {
            wallWidthInternal = 0;
            if (host == null || host.Element == null || center == null || direction == null)
            {
                return false;
            }

            XYZ rayDirection = direction.Normalize();
            OpeningBox hostBox = GetElementBox(host.Element, host.TransformToHost);
            if (hostBox == null)
            {
                return false;
            }

            double halfLength = Math.Max(hostBox.GetRangeAlong(rayDirection) + MmToInternal(1000), MmToInternal(1000));
            Line ray = Line.CreateBound(
                center - rayDirection.Multiply(halfLength),
                center + rayDirection.Multiply(halfLength));

            IList<Solid> solids = GetSolids(host.Element, host.TransformToHost);
            foreach (Solid solid in solids)
            {
                try
                {
                    var options = new SolidCurveIntersectionOptions();
                    SolidCurveIntersection intersection = solid.IntersectWithCurve(ray, options);
                    if (intersection == null || intersection.SegmentCount == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < intersection.SegmentCount; i++)
                    {
                        Curve segment = intersection.GetCurveSegment(i);
                        if (segment == null)
                        {
                            continue;
                        }

                        wallWidthInternal = Math.Max(wallWidthInternal, segment.Length);
                    }
                }
                catch
                {
                    // Try the next solid; imported or complex wall layers may reject curve intersection.
                }
            }

            return wallWidthInternal > GeometryTolerance;
        }

        private static bool IsGenericModelOpeningSymbol(FamilySymbol symbol)
        {
            if (symbol == null || symbol.Category == null)
            {
                return false;
            }

            return MepOpeningSelectionFilter.IsCategory(symbol, BuiltInCategory.OST_GenericModel);
        }

        private static string GetFamilySymbolDisplayName(FamilySymbol symbol)
        {
            if (symbol == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(symbol.FamilyName)
                ? symbol.Name
                : symbol.FamilyName + " : " + symbol.Name;
        }

        private static string GetElementKindLabel(MepOpeningElementKind kind)
        {
            switch (kind)
            {
                case MepOpeningElementKind.Duct:
                    return "Duct";
                case MepOpeningElementKind.CableTray:
                    return "Cable Tray";
                case MepOpeningElementKind.Pipe:
                    return "Pipe";
                case MepOpeningElementKind.Conduit:
                    return "Conduit";
                default:
                    return kind.ToString();
            }
        }

        private static List<OpeningCandidate> BuildOpeningCandidates(
            Document doc,
            IList<MepOpeningSourceElement> selectedSources,
            MepOpeningSettings settings,
            Dictionary<ElementId, List<ExistingOpeningInfo>> existingOpeningsByHost,
            MepOpeningRunResult result)
        {
            var candidates = new List<OpeningCandidate>();
            double mergeDistanceInternal = MmToInternal(settings.MergeDistanceMm);

            if (!settings.UseCurrentModelHosts)
            {
                result.AddSkip("Linked host openings need the family opening mode.");
                return candidates;
            }

            foreach (MepOpeningSourceElement source in selectedSources)
            {
                Element mepElement = source?.Element;
                if (mepElement == null || !mepElement.IsValidObject)
                {
                    result.AddSkip("Invalid selected element.");
                    continue;
                }

                if (source.IsLinked && !settings.UseLinkedModelSources)
                {
                    result.AddSkip("Linked source element skipped by settings.");
                    continue;
                }

                if (!source.IsLinked && !settings.UseCurrentModelSources)
                {
                    result.AddSkip("Current-model source element skipped by settings.");
                    continue;
                }

                MepOpeningElementKind elementKind;
                if (!MepOpeningSelectionFilter.TryGetElementKind(mepElement, out elementKind))
                {
                    result.AddSkip("Wrong element selected.");
                    continue;
                }

                MepOpeningElementRule rule = settings.GetRule(elementKind);
                if (rule != null && !rule.IsIncluded)
                {
                    result.AddSkip("Element type is unchecked in Opening Settings: " + GetElementLabel(mepElement));
                    continue;
                }

                result.SupportedMepCount++;

                OpeningSize openingSize;
                Document sourceDoc = source.SourceDocument ?? doc;
                Transform sourceTransform = source.TransformToHost ?? Transform.Identity;
                if (!TryGetOpeningSize(sourceDoc, mepElement, elementKind, rule, settings.IncludeInsulation, out openingSize))
                {
                    result.AddSkip("Element size could not be read: " + GetElementLabel(mepElement));
                    continue;
                }

                OpeningBox mepBox = GetElementBox(mepElement, sourceTransform);
                if (mepBox == null)
                {
                    result.AddSkip("Element geometry box could not be read: " + GetElementLabel(mepElement));
                    continue;
                }

                double searchExpansion = Math.Max(
                    mergeDistanceInternal,
                    Math.Max(openingSize.WidthInternal, openingSize.HeightInternal)) + MmToInternal(50);

                List<Element> hostCandidates = CollectHostCandidates(doc, mepBox.Expand(searchExpansion));
                bool foundCandidateForElement = false;
                bool foundCoveredOpeningForElement = false;

                foreach (Element host in hostCandidates)
                {
                    if (host == null || !host.IsValidObject)
                    {
                        continue;
                    }

                    MepOpeningHostKind hostKind;
                    if (!MepOpeningSelectionFilter.IsSupportedHost(host, out hostKind))
                    {
                        continue;
                    }

                    result.HostIntersectionsChecked++;

                    if (IsOwnedByOtherUser(doc, host))
                    {
                        result.AddSkip("Host is owned by another user: " + GetElementLabel(host));
                        continue;
                    }

                    OpeningBox hostBox = GetElementBox(host);
                    if (hostBox == null || !mepBox.IntersectsOrWithin(hostBox, GeometryTolerance))
                    {
                        continue;
                    }

                    OpeningBox intersectionBox;
                    bool solidIntersection = TryGetSolidIntersectionBox(
                        mepElement,
                        sourceTransform,
                        host,
                        Transform.Identity,
                        out intersectionBox);
                    if (intersectionBox == null)
                    {
                        intersectionBox = mepBox.GetOverlap(hostBox);
                    }

                    if (intersectionBox == null)
                    {
                        continue;
                    }

                    OpeningBox candidateBox = BuildCandidateBox(intersectionBox, openingSize);
                    List<ExistingOpeningInfo> existingForHost = GetExistingOpeningsForHost(existingOpeningsByHost, host.Id);
                    bool coveredByExisting = existingForHost.Any(existing => existing.Box.Contains(candidateBox, GeometryTolerance));

                    if (coveredByExisting)
                    {
                        result.AlreadyCovered++;
                        foundCoveredOpeningForElement = true;
                        continue;
                    }

                    List<ExistingOpeningInfo> existingToMerge = existingForHost
                        .Where(existing => existing.Box.IntersectsOrWithin(candidateBox, mergeDistanceInternal))
                        .ToList();

                    if (!solidIntersection && existingToMerge.Count == 0)
                    {
                        continue;
                    }

                    candidates.Add(new OpeningCandidate
                    {
                        MepElementId = mepElement.Id,
                        HostId = host.Id,
                        HostKind = hostKind,
                        RequestedShape = rule.Shape,
                        Box = candidateBox,
                        MinimumProfileWidth = openingSize.WidthInternal,
                        MinimumProfileHeight = openingSize.HeightInternal,
                        ExistingOpeningsToReplace = existingToMerge
                    });

                    result.OpeningRequests++;
                    foundCandidateForElement = true;
                }

                if (!foundCandidateForElement && !foundCoveredOpeningForElement)
                {
                    result.AddSkip("No wall, floor, slab, or beam crossing found: " + GetElementLabel(mepElement));
                }
            }

            return candidates;
        }

        private static List<OpeningCandidate> BuildOpeningCandidatesForHosts(
            Document doc,
            IList<Element> selectedHosts,
            MepOpeningSettings settings,
            Dictionary<ElementId, List<ExistingOpeningInfo>> existingOpeningsByHost,
            MepOpeningRunResult result)
        {
            var candidates = new List<OpeningCandidate>();
            double mergeDistanceInternal = MmToInternal(settings.MergeDistanceMm);

            if (!settings.UseCurrentModelHosts)
            {
                result.AddSkip("Linked host openings need the family opening mode.");
                return candidates;
            }

            foreach (Element host in selectedHosts)
            {
                if (host == null || !host.IsValidObject)
                {
                    result.AddSkip("Invalid selected host.");
                    continue;
                }

                MepOpeningHostKind hostKind;
                if (!MepOpeningSelectionFilter.IsSupportedHost(host, out hostKind))
                {
                    result.AddSkip("Wrong host selected.");
                    continue;
                }

                if (IsOwnedByOtherUser(doc, host))
                {
                    result.AddSkip("Host is owned by another user: " + GetElementLabel(host));
                    continue;
                }

                OpeningBox hostBox = GetElementBox(host);
                if (hostBox == null)
                {
                    result.AddSkip("Host geometry box could not be read: " + GetElementLabel(host));
                    continue;
                }

                double searchExpansion = Math.Max(mergeDistanceInternal, MmToInternal(50));
                List<MepOpeningSourceElement> sourceCandidates = CollectMepSourceCandidates(
                    doc,
                    hostBox.Expand(searchExpansion),
                    settings);

                bool foundCandidateForHost = false;
                bool foundCoveredOpeningForHost = false;

                foreach (MepOpeningSourceElement source in sourceCandidates)
                {
                    Element mepElement = source?.Element;
                    if (mepElement == null || !mepElement.IsValidObject)
                    {
                        continue;
                    }

                    if (source.IsLinked && !settings.UseLinkedModelSources)
                    {
                        continue;
                    }

                    if (!source.IsLinked && !settings.UseCurrentModelSources)
                    {
                        continue;
                    }

                    MepOpeningElementKind elementKind;
                    if (!MepOpeningSelectionFilter.TryGetElementKind(mepElement, out elementKind))
                    {
                        continue;
                    }

                    MepOpeningElementRule rule = settings.GetRule(elementKind);
                    if (rule != null && !rule.IsIncluded)
                    {
                        continue;
                    }

                    result.SupportedMepCount++;

                    OpeningSize openingSize;
                    Document sourceDoc = source.SourceDocument ?? doc;
                    Transform sourceTransform = source.TransformToHost ?? Transform.Identity;
                    if (!TryGetOpeningSize(sourceDoc, mepElement, elementKind, rule, settings.IncludeInsulation, out openingSize))
                    {
                        result.AddSkip("Element size could not be read: " + GetElementLabel(mepElement));
                        continue;
                    }

                    OpeningBox mepBox = GetElementBox(mepElement, sourceTransform);
                    if (mepBox == null || !mepBox.IntersectsOrWithin(hostBox, GeometryTolerance))
                    {
                        continue;
                    }

                    result.HostIntersectionsChecked++;

                    OpeningBox intersectionBox;
                    bool solidIntersection = TryGetSolidIntersectionBox(
                        mepElement,
                        sourceTransform,
                        host,
                        Transform.Identity,
                        out intersectionBox);
                    if (intersectionBox == null)
                    {
                        intersectionBox = mepBox.GetOverlap(hostBox);
                    }

                    if (intersectionBox == null)
                    {
                        continue;
                    }

                    OpeningBox candidateBox = BuildCandidateBox(intersectionBox, openingSize);
                    List<ExistingOpeningInfo> existingForHost = GetExistingOpeningsForHost(existingOpeningsByHost, host.Id);
                    bool coveredByExisting = existingForHost.Any(existing => existing.Box.Contains(candidateBox, GeometryTolerance));

                    if (coveredByExisting)
                    {
                        result.AlreadyCovered++;
                        foundCoveredOpeningForHost = true;
                        continue;
                    }

                    List<ExistingOpeningInfo> existingToMerge = existingForHost
                        .Where(existing => existing.Box.IntersectsOrWithin(candidateBox, mergeDistanceInternal))
                        .ToList();

                    if (!solidIntersection && existingToMerge.Count == 0)
                    {
                        continue;
                    }

                    candidates.Add(new OpeningCandidate
                    {
                        MepElementId = mepElement.Id,
                        HostId = host.Id,
                        HostKind = hostKind,
                        RequestedShape = rule.Shape,
                        Box = candidateBox,
                        MinimumProfileWidth = openingSize.WidthInternal,
                        MinimumProfileHeight = openingSize.HeightInternal,
                        ExistingOpeningsToReplace = existingToMerge
                    });

                    result.OpeningRequests++;
                    foundCandidateForHost = true;
                }

                if (!foundCandidateForHost && !foundCoveredOpeningForHost)
                {
                    result.AddSkip("No pipe, duct, cable tray, or conduit crossing found: " + GetElementLabel(host));
                }
            }

            return candidates;
        }

        private static List<OpeningGroup> BuildOpeningGroups(IList<OpeningCandidate> candidates, MepOpeningSettings settings)
        {
            var groups = candidates
                .Select(candidate => new OpeningGroup(candidate))
                .ToList();

            double mergeDistanceInternal = MmToInternal(settings.MergeDistanceMm);
            bool merged;

            do
            {
                merged = false;
                for (int i = 0; i < groups.Count; i++)
                {
                    for (int j = i + 1; j < groups.Count; j++)
                    {
                        if (!groups[i].CanMergeWith(groups[j], mergeDistanceInternal))
                        {
                            continue;
                        }

                        groups[i].Merge(groups[j]);
                        groups.RemoveAt(j);
                        merged = true;
                        break;
                    }

                    if (merged)
                    {
                        break;
                    }
                }
            } while (merged);

            return groups;
        }

        private static void CreateOpeningForGroup(Document doc, OpeningGroup group, MepOpeningRunResult result)
        {
            Element host = doc.GetElement(group.HostId);
            if (host == null || !host.IsValidObject)
            {
                result.AddFailure("Host no longer exists.");
                return;
            }

            List<ElementId> existingOpeningIds = group.ExistingOpeningIds
                .Distinct(new ElementIdEqualityComparer())
                .Where(id =>
                {
                    Element existingOpening = doc.GetElement(id);
                    return existingOpening != null && existingOpening.IsValidObject;
                })
                .ToList();

            // Delete-then-recreate happens inside one SubTransaction so a failed recreation rolls back
            // the delete too - the host keeps its existing opening instead of losing it with nothing
            // put back in its place.
            using (SubTransaction subTransaction = new SubTransaction(doc))
            {
                subTransaction.Start();

                try
                {
                    foreach (ElementId existingOpeningId in existingOpeningIds)
                    {
                        doc.Delete(existingOpeningId);
                    }

                    Opening opening = null;
                    switch (group.HostKind)
                    {
                        case MepOpeningHostKind.Wall:
                            opening = CreateWallOpening(doc, host as Wall, group);
                            break;
                        case MepOpeningHostKind.FloorSlab:
                            opening = CreateFloorOpening(doc, host, group);
                            break;
                        case MepOpeningHostKind.Beam:
                            opening = CreateBeamOpening(doc, host, group);
                            break;
                    }

                    if (opening == null)
                    {
                        subTransaction.RollBack();
                        result.AddFailure("Opening was not created for host: " + GetElementLabel(host) +
                            (existingOpeningIds.Count > 0 ? " (existing opening was kept)." : "."));
                        return;
                    }

                    subTransaction.Commit();
                    result.ExistingOpeningsReplaced += existingOpeningIds.Count;
                    result.OpeningsCreated++;
                }
                catch (Exception ex)
                {
                    if (subTransaction.GetStatus() == TransactionStatus.Started)
                        subTransaction.RollBack();

                    result.AddFailure("Opening failed on host " + GetElementLabel(host) + ": " + ex.Message +
                        (existingOpeningIds.Count > 0 ? " (existing opening was kept)." : ""));
                }
            }
        }

        private static Opening CreateWallOpening(Document doc, Wall wall, OpeningGroup group)
        {
            if (wall == null)
            {
                return null;
            }

            XYZ wallDirection = GetWallDirection(wall);
            XYZ center = GetWallOpeningCenter(wall, group.Box.Center);
            double width = Math.Max(group.GetRangeAlong(wallDirection), group.MinimumProfileWidth);
            double height = Math.Max(group.Box.Max.Z - group.Box.Min.Z, group.MinimumProfileHeight);

            if (width < MinimumOpeningInternal || height < MinimumOpeningInternal)
            {
                return null;
            }

            XYZ p1 = center - wallDirection.Multiply(width / 2.0) - XYZ.BasisZ.Multiply(height / 2.0);
            XYZ p2 = center + wallDirection.Multiply(width / 2.0) + XYZ.BasisZ.Multiply(height / 2.0);
            return doc.Create.NewOpening(wall, p1, p2);
        }

        private static Opening CreateFloorOpening(Document doc, Element host, OpeningGroup group)
        {
            CurveArray profile = group.ShouldCreateCircle
                ? CreateCircleProfile(group.Box.Center, group.CircleRadius, XYZ.BasisX, XYZ.BasisY)
                : CreateRectangleProfile(
                    group.Box.Center,
                    XYZ.BasisX,
                    XYZ.BasisY,
                    Math.Max(group.Box.Max.X - group.Box.Min.X, group.MinimumProfileWidth),
                    Math.Max(group.Box.Max.Y - group.Box.Min.Y, group.MinimumProfileHeight));

            return doc.Create.NewOpening(host, profile, true);
        }

        private static Opening CreateBeamOpening(Document doc, Element host, OpeningGroup group)
        {
            XYZ beamDirection = GetBeamDirection(host);
            XYZ up = Math.Abs(beamDirection.DotProduct(XYZ.BasisZ)) > 0.95 ? XYZ.BasisY : XYZ.BasisZ;
            XYZ side = up.CrossProduct(beamDirection).Normalize();

            CurveArray profileCenterY = group.ShouldCreateCircle
                ? CreateCircleProfile(group.Box.Center, group.CircleRadius, beamDirection, up)
                : CreateRectangleProfile(
                    group.Box.Center,
                    beamDirection,
                    up,
                    Math.Max(group.GetRangeAlong(beamDirection), group.MinimumProfileWidth),
                    Math.Max(group.GetRangeAlong(up), group.MinimumProfileHeight));

            Opening opening = TryCreateBeamOpening(doc, host, profileCenterY, RevitRefFace.CenterY);
            if (opening != null)
            {
                return opening;
            }

            CurveArray profileCenterZ = CreateRectangleProfile(
                group.Box.Center,
                beamDirection,
                side,
                Math.Max(group.GetRangeAlong(beamDirection), group.MinimumProfileWidth),
                Math.Max(group.GetRangeAlong(side), group.MinimumProfileHeight));

            opening = TryCreateBeamOpening(doc, host, profileCenterZ, RevitRefFace.CenterZ);
            if (opening != null)
            {
                return opening;
            }

            CurveArray profileCenterX = CreateRectangleProfile(
                group.Box.Center,
                side,
                up,
                Math.Max(group.GetRangeAlong(side), group.MinimumProfileWidth),
                Math.Max(group.GetRangeAlong(up), group.MinimumProfileHeight));

            return TryCreateBeamOpening(doc, host, profileCenterX, RevitRefFace.CenterX);
        }

        private static Opening TryCreateBeamOpening(Document doc, Element host, CurveArray profile, RevitRefFace referenceFace)
        {
            try
            {
                return doc.Create.NewOpening(host, profile, referenceFace);
            }
            catch
            {
                return null;
            }
        }

        private static CurveArray CreateRectangleProfile(XYZ center, XYZ axisX, XYZ axisY, double width, double height)
        {
            XYZ x = axisX.Normalize();
            XYZ y = axisY.Normalize();
            double halfWidth = Math.Max(width, MinimumOpeningInternal) / 2.0;
            double halfHeight = Math.Max(height, MinimumOpeningInternal) / 2.0;

            XYZ p1 = center - x.Multiply(halfWidth) - y.Multiply(halfHeight);
            XYZ p2 = center + x.Multiply(halfWidth) - y.Multiply(halfHeight);
            XYZ p3 = center + x.Multiply(halfWidth) + y.Multiply(halfHeight);
            XYZ p4 = center - x.Multiply(halfWidth) + y.Multiply(halfHeight);

            var profile = new CurveArray();
            profile.Append(Line.CreateBound(p1, p2));
            profile.Append(Line.CreateBound(p2, p3));
            profile.Append(Line.CreateBound(p3, p4));
            profile.Append(Line.CreateBound(p4, p1));
            return profile;
        }

        private static CurveArray CreateCircleProfile(XYZ center, double radius, XYZ axisX, XYZ axisY)
        {
            double safeRadius = Math.Max(radius, MinimumOpeningInternal / 2.0);
            XYZ x = axisX.Normalize();
            XYZ y = axisY.Normalize();

            var profile = new CurveArray();
            profile.Append(Arc.Create(center, safeRadius, 0, Math.PI, x, y));
            profile.Append(Arc.Create(center, safeRadius, Math.PI, Math.PI * 2.0, x, y));
            return profile;
        }

        private static List<Element> CollectHostCandidates(Document doc, OpeningBox searchBox)
        {
            var hosts = new List<Element>();
            if (doc == null || searchBox == null)
            {
                return hosts;
            }

            var filter = new BoundingBoxIntersectsFilter(searchBox.ToOutline());
            AddHostCandidates(doc, filter, BuiltInCategory.OST_Walls, hosts);
            AddHostCandidates(doc, filter, BuiltInCategory.OST_Floors, hosts);
            AddHostCandidates(doc, filter, BuiltInCategory.OST_StructuralFraming, hosts);

            return hosts
                .Where(element => element != null)
                .GroupBy(element => element.Id, new ElementIdEqualityComparer())
                .Select(group => group.First())
                .ToList();
        }

        private static void AddHostCandidates(
            Document doc,
            ElementFilter boundingFilter,
            BuiltInCategory category,
            ICollection<Element> hosts)
        {
            foreach (Element host in new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .WherePasses(boundingFilter))
            {
                hosts.Add(host);
            }
        }

        private static List<MepOpeningSourceElement> CollectMepSourceCandidates(
            Document hostDoc,
            OpeningBox hostSearchBox,
            MepOpeningSettings settings)
        {
            var sources = new List<MepOpeningSourceElement>();
            if (hostDoc == null || hostSearchBox == null || settings == null)
            {
                return sources;
            }

            if (settings.UseCurrentModelSources)
            {
                AddMepSourceCandidates(hostDoc, Transform.Identity, null, hostSearchBox, settings, sources);
            }

            if (settings.UseLinkedModelSources)
            {
                RevitLinkInstance linkInstance = FindLinkInstance(hostDoc, settings.SourceLinkInstanceUniqueId);
                Document linkDoc = linkInstance == null ? null : linkInstance.GetLinkDocument();
                if (linkDoc != null)
                {
                    Transform linkToHost = linkInstance.GetTotalTransform() ?? Transform.Identity;
                    OpeningBox linkSearchBox = hostSearchBox.TransformBy(linkToHost.Inverse);
                    AddMepSourceCandidates(linkDoc, linkToHost, linkInstance, linkSearchBox, settings, sources);
                }
            }

            return sources
                .Where(source => source != null && source.Element != null)
                .GroupBy(source => GetSourceKey(source), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static void AddMepSourceCandidates(
            Document sourceDoc,
            Transform transformToHost,
            RevitLinkInstance linkInstance,
            OpeningBox searchBox,
            MepOpeningSettings settings,
            ICollection<MepOpeningSourceElement> sources)
        {
            if (sourceDoc == null || searchBox == null || sources == null)
            {
                return;
            }

            var filter = new BoundingBoxIntersectsFilter(searchBox.ToOutline());
            if (IsElementKindIncluded(settings, MepOpeningElementKind.Pipe))
            {
                AddMepSourceCandidatesOfCategory(sourceDoc, transformToHost, linkInstance, filter, BuiltInCategory.OST_PipeCurves, sources);
            }

            if (IsElementKindIncluded(settings, MepOpeningElementKind.Duct))
            {
                AddMepSourceCandidatesOfCategory(sourceDoc, transformToHost, linkInstance, filter, BuiltInCategory.OST_DuctCurves, sources);
            }

            if (IsElementKindIncluded(settings, MepOpeningElementKind.CableTray))
            {
                AddMepSourceCandidatesOfCategory(sourceDoc, transformToHost, linkInstance, filter, BuiltInCategory.OST_CableTray, sources);
                AddMepSourceCandidatesOfCategory(sourceDoc, transformToHost, linkInstance, filter, BuiltInCategory.OST_CableTrayRun, sources);
                AddMepSourceCandidatesOfCategory(sourceDoc, transformToHost, linkInstance, filter, BuiltInCategory.OST_CableTrayFitting, sources);
            }

            if (IsElementKindIncluded(settings, MepOpeningElementKind.Conduit))
            {
                AddMepSourceCandidatesOfCategory(sourceDoc, transformToHost, linkInstance, filter, BuiltInCategory.OST_Conduit, sources);
            }
        }

        private static void AddMepSourceCandidatesOfCategory(
            Document sourceDoc,
            Transform transformToHost,
            RevitLinkInstance linkInstance,
            ElementFilter boundingFilter,
            BuiltInCategory category,
            ICollection<MepOpeningSourceElement> sources)
        {
            foreach (Element element in new FilteredElementCollector(sourceDoc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .WherePasses(boundingFilter))
            {
                if (linkInstance == null)
                {
                    sources.Add(MepOpeningSourceElement.FromCurrent(sourceDoc, element));
                }
                else
                {
                    sources.Add(MepOpeningSourceElement.FromLinked(
                        sourceDoc,
                        element,
                        linkInstance,
                        GetCleanLinkName(linkInstance, sourceDoc)));
                }
            }
        }

        private static RevitLinkInstance FindLinkInstance(Document hostDoc, string uniqueId)
        {
            if (hostDoc == null || string.IsNullOrWhiteSpace(uniqueId))
            {
                return null;
            }

            return new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault(link => link != null &&
                                        link.GetLinkDocument() != null &&
                                        string.Equals(link.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetSourceKey(MepOpeningSourceElement source)
        {
            if (source == null || source.Element == null)
            {
                return string.Empty;
            }

            return source.IsLinked
                ? "L:" + ElementIdHelper.ToReportString(source.LinkInstanceId) + ":" + source.Element.UniqueId
                : "C:" + source.Element.UniqueId;
        }

        private static bool IsElementKindIncluded(MepOpeningSettings settings, MepOpeningElementKind kind)
        {
            MepOpeningElementRule rule = settings?.GetRule(kind);
            return rule == null || rule.IsIncluded;
        }

        private static string GetCleanLinkName(RevitLinkInstance linkInstance, Document linkDoc)
        {
            string name = linkDoc != null && !string.IsNullOrWhiteSpace(linkDoc.Title)
                ? linkDoc.Title
                : linkInstance?.Name ?? "Linked Model";

            int colonIndex = name.IndexOf(':');
            return colonIndex > -1 ? name.Substring(0, colonIndex).Trim() : name.Trim();
        }

        private static Dictionary<ElementId, List<ExistingOpeningInfo>> CollectExistingOpeningsByHost(Document doc)
        {
            var map = new Dictionary<ElementId, List<ExistingOpeningInfo>>(new ElementIdEqualityComparer());
            if (doc == null)
            {
                return map;
            }

            foreach (Opening opening in new FilteredElementCollector(doc)
                .OfClass(typeof(Opening))
                .Cast<Opening>())
            {
                Element host = null;
                try
                {
                    host = opening.Host;
                }
                catch
                {
                    host = null;
                }

                if (host == null)
                {
                    continue;
                }

                OpeningBox box = GetElementBox(opening);
                if (box == null)
                {
                    continue;
                }

                List<ExistingOpeningInfo> list;
                if (!map.TryGetValue(host.Id, out list))
                {
                    list = new List<ExistingOpeningInfo>();
                    map[host.Id] = list;
                }

                list.Add(new ExistingOpeningInfo
                {
                    OpeningId = opening.Id,
                    HostId = host.Id,
                    Box = box
                });
            }

            return map;
        }

        private static List<ExistingOpeningInfo> GetExistingOpeningsForHost(
            Dictionary<ElementId, List<ExistingOpeningInfo>> existingOpeningsByHost,
            ElementId hostId)
        {
            if (existingOpeningsByHost == null || hostId == null)
            {
                return new List<ExistingOpeningInfo>();
            }

            List<ExistingOpeningInfo> openings;
            return existingOpeningsByHost.TryGetValue(hostId, out openings)
                ? openings
                : new List<ExistingOpeningInfo>();
        }

        private static bool TryGetOpeningSize(
            Document doc,
            Element element,
            MepOpeningElementKind kind,
            MepOpeningElementRule rule,
            bool includeInsulation,
            out OpeningSize openingSize)
        {
            openingSize = null;
            double width;
            double height;
            double diameter;

            switch (kind)
            {
                case MepOpeningElementKind.Pipe:
                    if (!TryGetDoubleParameter(element, BuiltInParameter.RBS_PIPE_OUTER_DIAMETER, out diameter) &&
                        !TryGetDoubleParameter(element, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM, out diameter) &&
                        !TryGetDoubleParameter(element, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM, out diameter))
                    {
                        return false;
                    }

                    width = diameter;
                    height = diameter;
                    break;

                case MepOpeningElementKind.Duct:
                    if (TryGetDoubleParameter(element, BuiltInParameter.RBS_CURVE_WIDTH_PARAM, out width) &&
                        TryGetDoubleParameter(element, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM, out height))
                    {
                        break;
                    }

                    if (!TryGetDoubleParameter(element, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM, out diameter))
                    {
                        return false;
                    }

                    width = diameter;
                    height = diameter;
                    break;

                case MepOpeningElementKind.CableTray:
                    if (!TryGetCableTraySize(element, out width, out height))
                    {
                        return false;
                    }

                    break;

                case MepOpeningElementKind.Conduit:
                    if (!TryGetDoubleParameter(element, BuiltInParameter.RBS_CONDUIT_OUTER_DIAM_PARAM, out diameter) &&
                        !TryGetDoubleParameter(element, BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM, out diameter))
                    {
                        return false;
                    }

                    width = diameter;
                    height = diameter;
                    break;

                default:
                    return false;
            }

            double insulationThickness = includeInsulation
                ? GetInsulationThickness(doc, element, kind)
                : 0;
            double buffer = MmToInternal(rule?.CutoutBufferMm ?? 0);
            double totalHorizontalExtra = 2.0 * (insulationThickness + buffer);
            double totalVerticalExtra = totalHorizontalExtra;

            double finalWidth = Math.Max(width + totalHorizontalExtra, MinimumOpeningInternal);
            double finalHeight = Math.Max(height + totalVerticalExtra, MinimumOpeningInternal);
            bool circle = rule != null && rule.Shape == MepOpeningShape.Circle &&
                          (kind == MepOpeningElementKind.Pipe || kind == MepOpeningElementKind.Conduit);

            openingSize = new OpeningSize
            {
                WidthInternal = finalWidth,
                HeightInternal = finalHeight,
                RequestedShape = circle ? MepOpeningShape.Circle : MepOpeningShape.Rectangle
            };

            return true;
        }

        private static bool TryGetDoubleParameter(Element element, BuiltInParameter parameterId, out double value)
        {
            value = 0;
            Parameter parameter = element?.get_Parameter(parameterId);
            if (parameter == null || parameter.StorageType != StorageType.Double)
            {
                return false;
            }

            value = parameter.AsDouble();
            return value > GeometryTolerance;
        }

        private static bool TryGetCableTraySize(Element element, out double width, out double height)
        {
            if (TryGetDoubleParameter(element, BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM, out width) &&
                TryGetDoubleParameter(element, BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM, out height))
            {
                return true;
            }

            width = 0;
            height = 0;
            BoundingBoxXYZ box = element?.get_BoundingBox(null);
            if (box == null || box.Min == null || box.Max == null)
            {
                return false;
            }

            double xRange = Math.Max(0, box.Max.X - box.Min.X);
            double yRange = Math.Max(0, box.Max.Y - box.Min.Y);
            double zRange = Math.Max(0, box.Max.Z - box.Min.Z);
            double horizontalRange = Math.Min(
                xRange > GeometryTolerance ? xRange : double.MaxValue,
                yRange > GeometryTolerance ? yRange : double.MaxValue);

            if (horizontalRange == double.MaxValue || zRange <= GeometryTolerance)
            {
                return false;
            }

            width = horizontalRange;
            height = zRange;
            return width > GeometryTolerance && height > GeometryTolerance;
        }

        private static double GetInsulationThickness(Document doc, Element element, MepOpeningElementKind kind)
        {
            if (doc == null || element == null)
            {
                return 0;
            }

            if (kind != MepOpeningElementKind.Pipe && kind != MepOpeningElementKind.Duct)
            {
                return 0;
            }

            double thickness = 0;
            try
            {
                ICollection<ElementId> insulationIds = InsulationLiningBase.GetInsulationIds(doc, element.Id);
                foreach (ElementId id in insulationIds)
                {
                    var insulation = doc.GetElement(id) as InsulationLiningBase;
                    if (insulation != null)
                    {
                        thickness = Math.Max(thickness, insulation.Thickness);
                    }
                }
            }
            catch
            {
                thickness = 0;
            }

            if (thickness > GeometryTolerance)
            {
                return thickness;
            }

            BuiltInParameter[] fallbackParameters = kind == MepOpeningElementKind.Pipe
                ? new[]
                {
                    BuiltInParameter.RBS_PIPE_INSULATION_THICKNESS,
                    BuiltInParameter.RBS_INSULATION_THICKNESS_FOR_PIPE,
                    BuiltInParameter.RBS_REFERENCE_INSULATION_THICKNESS
                }
                : new[]
                {
                    BuiltInParameter.RBS_INSULATION_THICKNESS_FOR_DUCT,
                    BuiltInParameter.RBS_REFERENCE_INSULATION_THICKNESS
                };

            foreach (BuiltInParameter parameterId in fallbackParameters)
            {
                double parameterValue;
                if (TryGetDoubleParameter(element, parameterId, out parameterValue))
                {
                    thickness = Math.Max(thickness, parameterValue);
                }
            }

            return thickness;
        }

        private static OpeningBox BuildCandidateBox(OpeningBox intersectionBox, OpeningSize openingSize)
        {
            OpeningBox box = intersectionBox.Clone();
            XYZ center = box.Center;
            box.EnsureMinimumSize(center, openingSize.WidthInternal, openingSize.HeightInternal, openingSize.HeightInternal);
            return box;
        }

        private static OpeningBox GetElementBox(Element element)
        {
            return GetElementBox(element, Transform.Identity);
        }

        private static OpeningBox GetElementBox(Element element, Transform transformToHost)
        {
            if (element == null)
            {
                return null;
            }

            BoundingBoxXYZ box = element.get_BoundingBox(null);
            OpeningBox openingBox = OpeningBox.FromBoundingBox(box);
            return openingBox == null ? null : openingBox.TransformBy(transformToHost ?? Transform.Identity);
        }

        private static bool TryGetSolidIntersectionBox(
            Element first,
            Transform firstTransform,
            Element second,
            Transform secondTransform,
            out OpeningBox intersectionBox)
        {
            intersectionBox = null;

            IList<Solid> firstSolids = GetSolids(first, firstTransform);
            IList<Solid> secondSolids = GetSolids(second, secondTransform);
            foreach (Solid firstSolid in firstSolids)
            {
                foreach (Solid secondSolid in secondSolids)
                {
                    try
                    {
                        Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                            firstSolid,
                            secondSolid,
                            BooleanOperationsType.Intersect);

                        if (intersection == null || intersection.Volume <= GeometryTolerance)
                        {
                            continue;
                        }

                        OpeningBox box = OpeningBox.FromBoundingBox(intersection.GetBoundingBox());
                        if (box == null)
                        {
                            continue;
                        }

                        intersectionBox = intersectionBox == null ? box : intersectionBox.Union(box);
                    }
                    catch
                    {
                        // Some Revit solids are not valid for boolean operations; other solids may still work.
                    }
                }
            }

            return intersectionBox != null;
        }

        private static IList<Solid> GetSolids(Element element, Transform transformToHost)
        {
            var solids = new List<Solid>();
            if (element == null)
            {
                return solids;
            }

            try
            {
                var options = new Options
                {
                    DetailLevel = ViewDetailLevel.Fine,
                    IncludeNonVisibleObjects = false,
                    ComputeReferences = false
                };

                GeometryElement geometry = element.get_Geometry(options);
                CollectSolids(geometry, solids);
            }
            catch
            {
                return solids;
            }

            Transform transform = transformToHost ?? Transform.Identity;
            var transformedSolids = new List<Solid>();
            foreach (Solid solid in solids)
            {
                try
                {
                    transformedSolids.Add(SolidUtils.CreateTransformed(solid, transform));
                }
                catch
                {
                    transformedSolids.Add(solid);
                }
            }

            return transformedSolids;
        }

        private static void CollectSolids(GeometryElement geometry, ICollection<Solid> solids)
        {
            if (geometry == null)
            {
                return;
            }

            foreach (GeometryObject geometryObject in geometry)
            {
                var solid = geometryObject as Solid;
                if (solid != null && solid.Volume > GeometryTolerance)
                {
                    solids.Add(solid);
                    continue;
                }

                var instance = geometryObject as GeometryInstance;
                if (instance != null)
                {
                    CollectSolids(instance.GetInstanceGeometry(), solids);
                }
            }
        }

        private static XYZ GetWallDirection(Wall wall)
        {
            var locationCurve = wall?.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve != null)
            {
                XYZ direction = (curve.GetEndPoint(1) - curve.GetEndPoint(0));
                direction = new XYZ(direction.X, direction.Y, 0);
                if (direction.GetLength() > GeometryTolerance)
                {
                    return direction.Normalize();
                }
            }

            return XYZ.BasisX;
        }

        private static XYZ GetWallOpeningCenter(Wall wall, XYZ requestedCenter)
        {
            var locationCurve = wall?.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve == null)
            {
                return requestedCenter;
            }

            IntersectionResult projected = curve.Project(requestedCenter);
            if (projected == null)
            {
                return requestedCenter;
            }

            XYZ projectedPoint = projected.XYZPoint;
            return new XYZ(projectedPoint.X, projectedPoint.Y, requestedCenter.Z);
        }

        private static XYZ GetBeamDirection(Element host)
        {
            var locationCurve = host?.Location as LocationCurve;
            Curve curve = locationCurve?.Curve;
            if (curve != null)
            {
                XYZ direction = curve.GetEndPoint(1) - curve.GetEndPoint(0);
                if (direction.GetLength() > GeometryTolerance)
                {
                    return direction.Normalize();
                }
            }

            return XYZ.BasisX;
        }

        private static bool IsOwnedByOtherUser(Document doc, Element element)
        {
            if (doc == null || element == null || !doc.IsWorkshared)
            {
                return false;
            }

            try
            {
                CheckoutStatus status = WorksharingUtils.GetCheckoutStatus(doc, element.Id);
                return status == CheckoutStatus.OwnedByOtherUser;
            }
            catch
            {
                return false;
            }
        }

        private static string GetElementLabel(Element element)
        {
            if (element == null)
            {
                return "(none)";
            }

            string name = string.IsNullOrWhiteSpace(element.Name) ? element.GetType().Name : element.Name;
            return name + " [" + ElementIdHelper.ToReportString(element.Id) + "]";
        }

        private static double MmToInternal(double value)
        {
#if REVIT2021 || REVIT2022 || REVIT2023 || REVIT2024 || REVIT2025 || REVIT2026 || REVIT2027 || REVIT2024_OR_GREATER
            return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
#else
            return UnitUtils.ConvertToInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS);
#endif
        }

        private sealed class OpeningSize
        {
            public double WidthInternal { get; set; }
            public double HeightInternal { get; set; }
            public MepOpeningShape RequestedShape { get; set; }
        }

        private sealed class OpeningCandidate
        {
            public ElementId MepElementId { get; set; }
            public ElementId HostId { get; set; }
            public MepOpeningHostKind HostKind { get; set; }
            public MepOpeningShape RequestedShape { get; set; }
            public OpeningBox Box { get; set; }
            public double MinimumProfileWidth { get; set; }
            public double MinimumProfileHeight { get; set; }
            public List<ExistingOpeningInfo> ExistingOpeningsToReplace { get; set; }
        }

        private sealed class FamilyOpeningHost
        {
            private FamilyOpeningHost()
            {
            }

            public Document Document { get; private set; }
            public Element Element { get; private set; }
            public Transform TransformToHost { get; private set; }
            public bool IsLinked { get; private set; }
            public ElementId LinkInstanceId { get; private set; }
            public string Key { get; private set; }
            public string Label { get; private set; }

            public static FamilyOpeningHost FromCurrent(Document doc, Element element)
            {
                string elementId = ElementIdHelper.ToReportString(element?.Id);
                return new FamilyOpeningHost
                {
                    Document = doc,
                    Element = element,
                    TransformToHost = Transform.Identity,
                    IsLinked = false,
                    LinkInstanceId = ElementId.InvalidElementId,
                    Key = "C:" + elementId,
                    Label = "Current Model Wall [" + elementId + "]"
                };
            }

            public static FamilyOpeningHost FromLinked(
                Document linkDoc,
                Element element,
                RevitLinkInstance linkInstance,
                Transform linkToHost,
                string linkName)
            {
                string linkId = ElementIdHelper.ToReportString(linkInstance?.Id);
                string elementId = ElementIdHelper.ToReportString(element?.Id);
                string sourceLabel = string.IsNullOrWhiteSpace(linkName) ? "Linked Model" : linkName;
                return new FamilyOpeningHost
                {
                    Document = linkDoc,
                    Element = element,
                    TransformToHost = linkToHost ?? Transform.Identity,
                    IsLinked = true,
                    LinkInstanceId = linkInstance == null ? ElementId.InvalidElementId : linkInstance.Id,
                    Key = "L:" + linkId + ":" + elementId,
                    Label = sourceLabel + " Wall [" + elementId + "]"
                };
            }
        }

        private sealed class FamilyOpeningCandidate
        {
            public ElementId MepElementId { get; set; }
            public MepOpeningElementKind ElementKind { get; set; }
            public FamilyOpeningHost Host { get; set; }
            public string HostKey { get; set; }
            public string FamilyDisplayName { get; set; }
            public OpeningBox Box { get; set; }
            public XYZ PlacementPoint { get; set; }
            public double ObjectWidthInternal { get; set; }
            public double ObjectHeightInternal { get; set; }
            public double CutoutBufferInternal { get; set; }
            public double WallWidthInternal { get; set; }
            public XYZ SourcePlanDirection { get; set; }
            public XYZ WallDirection { get; set; }
            public ElementId PreferredLevelId { get; set; }
        }

        private sealed class FamilyOpeningGroup
        {
            private readonly List<FamilyOpeningCandidate> _candidates = new List<FamilyOpeningCandidate>();

            public FamilyOpeningGroup(FamilyOpeningCandidate candidate)
            {
                Add(candidate);
            }

            public string HostKey { get; private set; }
            public string FamilyDisplayName { get; private set; }
            public OpeningBox Box { get; private set; }
            public XYZ SinglePlacementPoint { get; private set; }
            public double MaxObjectWidthInternal { get; private set; }
            public double MaxObjectHeightInternal { get; private set; }
            public double CutoutBufferInternal { get; private set; }
            public double WallWidthInternal { get; private set; }
            public XYZ SourcePlanDirection { get; private set; }
            public XYZ WallDirection { get; private set; }
            public ElementId PreferredLevelId { get; private set; }

            public XYZ PlacementPoint
            {
                get { return _candidates.Count == 1 && SinglePlacementPoint != null ? SinglePlacementPoint : Box.Center; }
            }

            public double ObjectWidthInternal
            {
                get
                {
                    XYZ widthAxis = WallDirection ?? XYZ.BasisX;
                    double widthFromMergedCutout = Box.GetRangeAlong(widthAxis) - (2.0 * CutoutBufferInternal);
                    return Math.Max(Math.Max(widthFromMergedCutout, MaxObjectWidthInternal), MinimumOpeningInternal);
                }
            }

            public double ObjectHeightInternal
            {
                get
                {
                    double heightFromMergedCutout = (Box.Max.Z - Box.Min.Z) - (2.0 * CutoutBufferInternal);
                    return Math.Max(Math.Max(heightFromMergedCutout, MaxObjectHeightInternal), MinimumOpeningInternal);
                }
            }

            public bool CanMergeWith(FamilyOpeningGroup other, double mergeDistance)
            {
                if (other == null ||
                    !string.Equals(HostKey, other.HostKey, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(FamilyDisplayName, other.FamilyDisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (SourcePlanDirection != null && other.SourcePlanDirection != null)
                {
                    double alignment = Math.Abs(SourcePlanDirection.DotProduct(other.SourcePlanDirection));
                    if (alignment < 0.95)
                    {
                        return false;
                    }
                }

                return Box.IntersectsOrWithin(other.Box, mergeDistance);
            }

            public void Merge(FamilyOpeningGroup other)
            {
                foreach (FamilyOpeningCandidate candidate in other._candidates)
                {
                    Add(candidate);
                }
            }

            private void Add(FamilyOpeningCandidate candidate)
            {
                if (candidate == null)
                {
                    return;
                }

                if (_candidates.Count == 0)
                {
                    HostKey = candidate.HostKey;
                    FamilyDisplayName = candidate.FamilyDisplayName;
                    Box = candidate.Box.Clone();
                    SinglePlacementPoint = candidate.PlacementPoint;
                    MaxObjectWidthInternal = candidate.ObjectWidthInternal;
                    MaxObjectHeightInternal = candidate.ObjectHeightInternal;
                    CutoutBufferInternal = candidate.CutoutBufferInternal;
                    WallWidthInternal = candidate.WallWidthInternal;
                    SourcePlanDirection = candidate.SourcePlanDirection;
                    WallDirection = candidate.WallDirection;
                    PreferredLevelId = candidate.PreferredLevelId;
                }
                else
                {
                    Box = Box.Union(candidate.Box);
                    MaxObjectWidthInternal = Math.Max(MaxObjectWidthInternal, candidate.ObjectWidthInternal);
                    MaxObjectHeightInternal = Math.Max(MaxObjectHeightInternal, candidate.ObjectHeightInternal);
                    CutoutBufferInternal = Math.Max(CutoutBufferInternal, candidate.CutoutBufferInternal);
                    WallWidthInternal = Math.Max(WallWidthInternal, candidate.WallWidthInternal);
                }

                _candidates.Add(candidate);
            }
        }

        private sealed class ExistingOpeningInfo
        {
            public ElementId OpeningId { get; set; }
            public ElementId HostId { get; set; }
            public OpeningBox Box { get; set; }
        }

        private sealed class OpeningGroup
        {
            private readonly List<OpeningCandidate> _candidates = new List<OpeningCandidate>();
            private readonly List<ExistingOpeningInfo> _existingOpenings = new List<ExistingOpeningInfo>();

            public OpeningGroup(OpeningCandidate candidate)
            {
                Add(candidate);
            }

            public ElementId HostId { get; private set; }
            public MepOpeningHostKind HostKind { get; private set; }
            public OpeningBox Box { get; private set; }
            public double MinimumProfileWidth { get; private set; }
            public double MinimumProfileHeight { get; private set; }
            public MepOpeningShape RequestedShape { get; private set; }

            public IEnumerable<ElementId> ExistingOpeningIds
            {
                get { return _existingOpenings.Select(opening => opening.OpeningId); }
            }

            public double CircleRadius
            {
                get { return Math.Max(MinimumProfileWidth, MinimumProfileHeight) / 2.0; }
            }

            public bool ShouldCreateCircle
            {
                get
                {
                    return RequestedShape == MepOpeningShape.Circle &&
                           _candidates.Count == 1 &&
                           _existingOpenings.Count == 0;
                }
            }

            public bool CanMergeWith(OpeningGroup other, double mergeDistance)
            {
                if (other == null || !HostId.Equals(other.HostId))
                {
                    return false;
                }

                if (Box.IntersectsOrWithin(other.Box, mergeDistance))
                {
                    return true;
                }

                return ExistingOpeningIds.Any(id => other.ExistingOpeningIds.Contains(id, new ElementIdEqualityComparer()));
            }

            public void Merge(OpeningGroup other)
            {
                foreach (OpeningCandidate candidate in other._candidates)
                {
                    Add(candidate);
                }
            }

            public double GetRangeAlong(XYZ axis)
            {
                return Box.GetRangeAlong(axis);
            }

            private void Add(OpeningCandidate candidate)
            {
                if (candidate == null)
                {
                    return;
                }

                if (_candidates.Count == 0)
                {
                    HostId = candidate.HostId;
                    HostKind = candidate.HostKind;
                    RequestedShape = candidate.RequestedShape;
                    Box = candidate.Box.Clone();
                    MinimumProfileWidth = candidate.MinimumProfileWidth;
                    MinimumProfileHeight = candidate.MinimumProfileHeight;
                }
                else
                {
                    Box = Box.Union(candidate.Box);
                    MinimumProfileWidth = Math.Max(MinimumProfileWidth, candidate.MinimumProfileWidth);
                    MinimumProfileHeight = Math.Max(MinimumProfileHeight, candidate.MinimumProfileHeight);
                    if (candidate.RequestedShape != RequestedShape)
                    {
                        RequestedShape = MepOpeningShape.Rectangle;
                    }
                }

                _candidates.Add(candidate);

                foreach (ExistingOpeningInfo existingOpening in candidate.ExistingOpeningsToReplace ?? new List<ExistingOpeningInfo>())
                {
                    if (_existingOpenings.Any(existing => existing.OpeningId.Equals(existingOpening.OpeningId)))
                    {
                        continue;
                    }

                    _existingOpenings.Add(existingOpening);
                    Box = Box.Union(existingOpening.Box);
                }

                if (_candidates.Count > 1 || _existingOpenings.Count > 0)
                {
                    RequestedShape = MepOpeningShape.Rectangle;
                }
            }
        }

        private sealed class OpeningBox
        {
            public XYZ Min { get; private set; }
            public XYZ Max { get; private set; }

            public XYZ Center
            {
                get
                {
                    return new XYZ(
                        (Min.X + Max.X) / 2.0,
                        (Min.Y + Max.Y) / 2.0,
                        (Min.Z + Max.Z) / 2.0);
                }
            }

            public static OpeningBox FromBoundingBox(BoundingBoxXYZ box)
            {
                if (box == null)
                {
                    return null;
                }

                Transform transform = box.Transform ?? Transform.Identity;
                var points = new List<XYZ>
                {
                    transform.OfPoint(new XYZ(box.Min.X, box.Min.Y, box.Min.Z)),
                    transform.OfPoint(new XYZ(box.Min.X, box.Min.Y, box.Max.Z)),
                    transform.OfPoint(new XYZ(box.Min.X, box.Max.Y, box.Min.Z)),
                    transform.OfPoint(new XYZ(box.Min.X, box.Max.Y, box.Max.Z)),
                    transform.OfPoint(new XYZ(box.Max.X, box.Min.Y, box.Min.Z)),
                    transform.OfPoint(new XYZ(box.Max.X, box.Min.Y, box.Max.Z)),
                    transform.OfPoint(new XYZ(box.Max.X, box.Max.Y, box.Min.Z)),
                    transform.OfPoint(new XYZ(box.Max.X, box.Max.Y, box.Max.Z))
                };

                return FromPoints(points);
            }

            public static OpeningBox FromPoints(IList<XYZ> points)
            {
                if (points == null || points.Count == 0)
                {
                    return null;
                }

                return new OpeningBox
                {
                    Min = new XYZ(points.Min(point => point.X), points.Min(point => point.Y), points.Min(point => point.Z)),
                    Max = new XYZ(points.Max(point => point.X), points.Max(point => point.Y), points.Max(point => point.Z))
                };
            }

            public OpeningBox Clone()
            {
                return new OpeningBox
                {
                    Min = Min,
                    Max = Max
                };
            }

            public OpeningBox Expand(double value)
            {
                return new OpeningBox
                {
                    Min = new XYZ(Min.X - value, Min.Y - value, Min.Z - value),
                    Max = new XYZ(Max.X + value, Max.Y + value, Max.Z + value)
                };
            }

            public OpeningBox TransformBy(Transform transform)
            {
                if (transform == null)
                {
                    return Clone();
                }

                return FromPoints(new List<XYZ>
                {
                    transform.OfPoint(new XYZ(Min.X, Min.Y, Min.Z)),
                    transform.OfPoint(new XYZ(Min.X, Min.Y, Max.Z)),
                    transform.OfPoint(new XYZ(Min.X, Max.Y, Min.Z)),
                    transform.OfPoint(new XYZ(Min.X, Max.Y, Max.Z)),
                    transform.OfPoint(new XYZ(Max.X, Min.Y, Min.Z)),
                    transform.OfPoint(new XYZ(Max.X, Min.Y, Max.Z)),
                    transform.OfPoint(new XYZ(Max.X, Max.Y, Min.Z)),
                    transform.OfPoint(new XYZ(Max.X, Max.Y, Max.Z))
                });
            }

            public OpeningBox Union(OpeningBox other)
            {
                if (other == null)
                {
                    return Clone();
                }

                return new OpeningBox
                {
                    Min = new XYZ(
                        Math.Min(Min.X, other.Min.X),
                        Math.Min(Min.Y, other.Min.Y),
                        Math.Min(Min.Z, other.Min.Z)),
                    Max = new XYZ(
                        Math.Max(Max.X, other.Max.X),
                        Math.Max(Max.Y, other.Max.Y),
                        Math.Max(Max.Z, other.Max.Z))
                };
            }

            public OpeningBox GetOverlap(OpeningBox other)
            {
                if (other == null || !IntersectsOrWithin(other, GeometryTolerance))
                {
                    return null;
                }

                return new OpeningBox
                {
                    Min = new XYZ(
                        Math.Max(Min.X, other.Min.X),
                        Math.Max(Min.Y, other.Min.Y),
                        Math.Max(Min.Z, other.Min.Z)),
                    Max = new XYZ(
                        Math.Min(Max.X, other.Max.X),
                        Math.Min(Max.Y, other.Max.Y),
                        Math.Min(Max.Z, other.Max.Z))
                };
            }

            public bool Contains(OpeningBox other, double tolerance)
            {
                if (other == null)
                {
                    return false;
                }

                return Min.X - tolerance <= other.Min.X &&
                       Min.Y - tolerance <= other.Min.Y &&
                       Min.Z - tolerance <= other.Min.Z &&
                       Max.X + tolerance >= other.Max.X &&
                       Max.Y + tolerance >= other.Max.Y &&
                       Max.Z + tolerance >= other.Max.Z;
            }

            public bool IntersectsOrWithin(OpeningBox other, double maxDistance)
            {
                return DistanceTo(other) <= maxDistance;
            }

            public double DistanceTo(OpeningBox other)
            {
                if (other == null)
                {
                    return double.MaxValue;
                }

                double dx = Math.Max(0, Math.Max(other.Min.X - Max.X, Min.X - other.Max.X));
                double dy = Math.Max(0, Math.Max(other.Min.Y - Max.Y, Min.Y - other.Max.Y));
                double dz = Math.Max(0, Math.Max(other.Min.Z - Max.Z, Min.Z - other.Max.Z));
                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            public void EnsureMinimumSize(XYZ center, double widthX, double widthY, double heightZ)
            {
                double halfX = Math.Max(widthX, MinimumOpeningInternal) / 2.0;
                double halfY = Math.Max(widthY, MinimumOpeningInternal) / 2.0;
                double halfZ = Math.Max(heightZ, MinimumOpeningInternal) / 2.0;

                Min = new XYZ(
                    Math.Min(Min.X, center.X - halfX),
                    Math.Min(Min.Y, center.Y - halfY),
                    Math.Min(Min.Z, center.Z - halfZ));
                Max = new XYZ(
                    Math.Max(Max.X, center.X + halfX),
                    Math.Max(Max.Y, center.Y + halfY),
                    Math.Max(Max.Z, center.Z + halfZ));
            }

            public double GetRangeAlong(XYZ axis)
            {
                XYZ normalized = axis.Normalize();
                double min = double.MaxValue;
                double max = double.MinValue;

                foreach (XYZ point in GetCorners())
                {
                    double projection = point.DotProduct(normalized);
                    min = Math.Min(min, projection);
                    max = Math.Max(max, projection);
                }

                return Math.Max(max - min, MinimumOpeningInternal);
            }

            public Outline ToOutline()
            {
                return new Outline(Min, Max);
            }

            private IEnumerable<XYZ> GetCorners()
            {
                yield return new XYZ(Min.X, Min.Y, Min.Z);
                yield return new XYZ(Min.X, Min.Y, Max.Z);
                yield return new XYZ(Min.X, Max.Y, Min.Z);
                yield return new XYZ(Min.X, Max.Y, Max.Z);
                yield return new XYZ(Max.X, Min.Y, Min.Z);
                yield return new XYZ(Max.X, Min.Y, Max.Z);
                yield return new XYZ(Max.X, Max.Y, Min.Z);
                yield return new XYZ(Max.X, Max.Y, Max.Z);
            }
        }

        private sealed class ElementIdEqualityComparer : IEqualityComparer<ElementId>
        {
            public bool Equals(ElementId x, ElementId y)
            {
                return ElementIdHelper.GetIntegerValue(x) == ElementIdHelper.GetIntegerValue(y);
            }

            public int GetHashCode(ElementId obj)
            {
                return ElementIdHelper.GetIntegerValue(obj).GetHashCode();
            }
        }

        private sealed class MepOpeningFailurePreprocessor : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                return FailureProcessingResult.Continue;
            }
        }
    }
}
