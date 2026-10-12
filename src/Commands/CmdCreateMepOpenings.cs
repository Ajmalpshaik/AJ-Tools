#region Metadata
/*
 * Tool Name     : Create Openings
 * File Name     : CmdCreateMepOpenings.cs
 * Purpose       : Revit external command entry point for automatic MEP opening creation.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.1
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.MepOpenings
 *
 * Input         : Selection-scope MEP sources or opening hosts, based on Opening Settings.
 * Output        : Direct openings in current-model walls, floors/slabs, and beams, or opening families
 *                 (Family Opening mode).
 *
 * Notes         :
 * - One transaction named "AJ-Tools: Create Openings" is used for all model changes.
 * - No confirmation and no success popup: a clean run is silent. A run that created nothing, or that
 *   failed or skipped anything, shows one report with the reasons grouped, counted and capped.
 * - Linked source elements are read only and transformed into current-model coordinates.
 * - Linked host elements are never modified; linked walls are handled by Family Opening mode, which
 *   places an opening family in the current model (Source Elements selection).
 * - Direct wall openings are rectangular in the Revit API; circle settings use a bounding rectangle on walls.
 *
 * Changelog     :
 * v1.2.1 (2026-10-12) - Run report: the failure reasons the service collects were never shown - a run
 *                       where every opening failed returned Failed with an empty message and showed
 *                       nothing, and a partly successful run never said what it left out. Now one
 *                       plain report lists failures and skips (grouped, counted, capped) whenever
 *                       anything failed or was skipped.
 *                       Corrected the stale note that linked hosts need a "later" family mode.
 *                       Also: an unexpected error now shows one message, not the same text twice
 *                       (Revit's own error box is no longer given it).
 * v1.2.0 (2026-07-21) - Reverted v1.1.0 below - the Opening split button's default face is now permanently
 *                       fixed on "Create Openings" (RibbonManager.cs v1.12.0, IsSynchronizedWithCurrentItem
 *                       = false), so Execute() no longer needs to set CurrentButton itself.
 * v1.1.0 (2026-07-21) - Now sets the Opening ribbon split button's CurrentButton to itself as the very
 *                       first thing Execute does (App.MepOpeningSplitButton / CreateOpeningsButton), so
 *                       the split button's default face reflects that Create Openings was the one just
 *                       run - see RibbonManager.cs v1.10.0.
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Models.MepOpenings;
using AJTools.Services.MepOpenings;
using AJTools.Utils;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCreateMepOpenings : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uiDoc, out message))
            {
                TaskDialog.Show("Create Openings", "Open a Revit project and try again.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                TaskDialog.Show("Create Openings", message);
                return Result.Cancelled;
            }

            try
            {
                var settings = MepOpeningSettingsService.Load();
                settings.Normalize();

                if (settings.UseLinkedModelSources && string.IsNullOrWhiteSpace(settings.SourceLinkInstanceUniqueId))
                {
                    TaskDialog.Show("Create Openings", "Select the opening source linked model in Opening Settings and try again.");
                    return Result.Cancelled;
                }

                if (settings.UseLinkedModelHosts && string.IsNullOrWhiteSpace(settings.HostLinkInstanceUniqueId))
                {
                    TaskDialog.Show("Create Openings", "Select the opening host linked model in Opening Settings and try again.");
                    return Result.Cancelled;
                }

                if (settings.UseLinkedModelHosts && settings.CreationMode == MepOpeningCreationMode.DirectOpening)
                {
                    TaskDialog.Show(
                        "Create Openings",
                        "Linked host model needs Family Opening mode.");
                    return Result.Cancelled;
                }

                var service = new MepOpeningService();
                MepOpeningRunResult result;

                if (settings.CreationMode == MepOpeningCreationMode.FamilyOpening)
                {
                    if (settings.SelectionMethod == MepOpeningSelectionMethod.HostElements &&
                        settings.UseLinkedModelHosts &&
                        !settings.UseCurrentModelHosts)
                    {
                        TaskDialog.Show(
                            "Create Openings",
                            "Linked wall host family openings currently run from Source Elements mode.\n\nSet Selection Method to Source Elements and try again.");
                        return Result.Cancelled;
                    }

                    if (settings.SelectionMethod == MepOpeningSelectionMethod.HostElements)
                    {
                        IList<Element> selectedHosts = GetSelectedOrPickedHosts(uiDoc);
                        if (selectedHosts == null || selectedHosts.Count == 0)
                        {
                            return Result.Cancelled;
                        }

                        result = service.CreateOpeningFamiliesFromHosts(doc, selectedHosts, settings);
                    }
                    else
                    {
                        IList<MepOpeningSourceElement> selectedSources = GetSelectedOrPickedSources(uiDoc, settings);
                        if (selectedSources == null || selectedSources.Count == 0)
                        {
                            return Result.Cancelled;
                        }

                        result = service.CreateOpeningFamilies(doc, selectedSources, settings);
                    }
                }
                else if (settings.SelectionMethod == MepOpeningSelectionMethod.HostElements)
                {
                    IList<Element> selectedHosts = GetSelectedOrPickedHosts(uiDoc);
                    if (selectedHosts == null || selectedHosts.Count == 0)
                    {
                        return Result.Cancelled;
                    }

                    result = service.CreateOpeningsFromHosts(doc, selectedHosts, settings);
                }
                else
                {
                    IList<MepOpeningSourceElement> selectedSources = GetSelectedOrPickedSources(uiDoc, settings);
                    if (selectedSources == null || selectedSources.Count == 0)
                    {
                        return Result.Cancelled;
                    }

                    result = service.CreateOpenings(doc, selectedSources, settings);
                }

                // The service collects a reason for every failure and skip. The failures used to be
                // shown nowhere: a run where every opening failed returned Failed with an empty message
                // and showed nothing, and a run that created some openings never said what it left out.
                if (result.Failed > 0 && result.OpeningsCreated == 0)
                {
                    TaskDialog.Show("Create Openings", BuildRunReport(result));
                    return Result.Failed;
                }

                // A run that created nothing (e.g. everything was unchecked in Opening Settings, or
                // every crossing was already covered) must still tell the modeller why, and a run that
                // failed or skipped anything says what it left out. A clean run stays silent.
                if (result.OpeningsCreated == 0 || result.Failed > 0 || result.Skipped > 0)
                {
                    TaskDialog.Show("Create Openings", BuildRunReport(result));
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Create Openings", "Openings could not be created:\n\n" + ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// One plain report: what was created, then the failures and the skips, each grouped by
        /// reason with a count and capped, so a big selection cannot turn it into a wall of text.
        /// </summary>
        private static string BuildRunReport(MepOpeningRunResult result)
        {
            var text = new StringBuilder();
            text.Append(result.OpeningsCreated == 0
                ? "No openings were created."
                : result.OpeningsCreated + " opening(s) created. Some of the selection was left out:");

            AppendReasons(text, "Failed", result.Failed, result.FailureReasons, result.GetTopFailureReasons());
            AppendReasons(text, "Skipped", result.Skipped, result.SkipReasons, result.GetTopSkipReasons());

            // Crossings that already had an opening are not counted as skips, but when nothing was
            // created they are often the whole answer.
            if (result.OpeningsCreated == 0 && result.AlreadyCovered > 0)
            {
                text.Append("\n\nAlready covered by an existing opening: ")
                    .Append(result.AlreadyCovered)
                    .Append(" crossing(s).");
            }

            return text.ToString();
        }

        private static void AppendReasons(
            StringBuilder text,
            string heading,
            int count,
            IList<string> allReasons,
            IEnumerable<string> topReasons)
        {
            if (count <= 0)
            {
                return;
            }

            text.Append("\n\n").Append(heading).Append(" (").Append(count).Append("):");
            foreach (string line in topReasons)
            {
                text.Append("\n - ").Append(line);
            }

            int notListed = allReasons.Distinct().Count() - MepOpeningRunResult.MaxReportedReasons;
            if (notListed > 0)
            {
                text.Append("\n - ...and ").Append(notListed).Append(" more");
            }
        }

        private static IList<MepOpeningSourceElement> GetSelectedOrPickedSources(
            UIDocument uiDoc,
            MepOpeningSettings settings)
        {
            List<MepOpeningSourceElement> preselectedSources = GetPreselectedCurrentSources(uiDoc, settings);
            if (preselectedSources.Count > 0)
            {
                return preselectedSources;
            }

            IList<Reference> pickedReferences = PickSourceReferences(uiDoc, settings);
            return ResolveSourceReferences(uiDoc.Document, pickedReferences, settings);
        }

        private static List<MepOpeningSourceElement> GetPreselectedCurrentSources(
            UIDocument uiDoc,
            MepOpeningSettings settings)
        {
            var sources = new List<MepOpeningSourceElement>();
            if (uiDoc == null || settings == null || !settings.UseCurrentModelSources)
            {
                return sources;
            }

            ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return sources;
            }

            foreach (ElementId id in selectedIds)
            {
                Element element = uiDoc.Document.GetElement(id);
                MepOpeningElementKind ignored;
                if (!MepOpeningSelectionFilter.TryGetElementKind(element, out ignored) ||
                    !IsElementKindIncluded(settings, ignored))
                {
                    continue;
                }

                sources.Add(MepOpeningSourceElement.FromCurrent(uiDoc.Document, element));
            }

            return sources;
        }

        private static IList<Element> GetSelectedOrPickedHosts(UIDocument uiDoc)
        {
            List<Element> preselectedHosts = GetPreselectedCurrentHosts(uiDoc);
            if (preselectedHosts.Count > 0)
            {
                return preselectedHosts;
            }

            IList<Reference> pickedReferences = uiDoc.Selection.PickObjects(
                ObjectType.Element,
                new MepOpeningHostSelectionFilter(),
                "Select walls, floors, slabs, or beams for openings");

            return pickedReferences
                .Where(reference => reference != null)
                .Select(reference => uiDoc.Document.GetElement(reference.ElementId))
                .Where(element =>
                {
                    MepOpeningHostKind ignored;
                    return MepOpeningSelectionFilter.IsSupportedHost(element, out ignored);
                })
                .GroupBy(element => ElementIdHelper.ToReportString(element.Id), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<Element> GetPreselectedCurrentHosts(UIDocument uiDoc)
        {
            var hosts = new List<Element>();
            if (uiDoc == null)
            {
                return hosts;
            }

            ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return hosts;
            }

            foreach (ElementId id in selectedIds)
            {
                Element element = uiDoc.Document.GetElement(id);
                MepOpeningHostKind ignored;
                if (MepOpeningSelectionFilter.IsSupportedHost(element, out ignored))
                {
                    hosts.Add(element);
                }
            }

            return hosts;
        }

        private static IList<Reference> PickSourceReferences(UIDocument uiDoc, MepOpeningSettings settings)
        {
            var filter = new MepOpeningSourceSelectionFilter(uiDoc.Document, settings);
            string prompt = BuildPickPrompt(settings);

            if (settings.UseLinkedModelSources && !settings.UseCurrentModelSources)
            {
                return uiDoc.Selection.PickObjects(ObjectType.LinkedElement, filter, prompt);
            }

            if (settings.UseLinkedModelSources && settings.UseCurrentModelSources)
            {
                return uiDoc.Selection.PickObjects(ObjectType.PointOnElement, filter, prompt);
            }

            return uiDoc.Selection.PickObjects(ObjectType.Element, filter, prompt);
        }

        private static string BuildPickPrompt(MepOpeningSettings settings)
        {
            if (settings.UseLinkedModelSources && !settings.UseCurrentModelSources)
            {
                return "Select pipes, ducts, cable trays, or conduits inside the selected linked model";
            }

            if (settings.UseLinkedModelSources && settings.UseCurrentModelSources)
            {
                return "Select pipes, ducts, cable trays, or conduits from the current model or selected linked model";
            }

            return "Select pipes, ducts, cable trays, or conduits from the current model";
        }

        private static List<MepOpeningSourceElement> ResolveSourceReferences(
            Document hostDoc,
            IList<Reference> references,
            MepOpeningSettings settings)
        {
            var sources = new List<MepOpeningSourceElement>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (hostDoc == null || references == null)
            {
                return sources;
            }

            foreach (Reference reference in references.Where(item => item != null))
            {
                MepOpeningSourceElement source;
                if (!TryResolveSourceReference(hostDoc, reference, settings, out source) || source == null)
                {
                    continue;
                }

                string key = source.IsLinked
                    ? "L:" + ElementIdHelper.ToReportString(source.LinkInstanceId) + ":" + ElementIdHelper.ToReportString(source.Element.Id)
                    : "C:" + ElementIdHelper.ToReportString(source.Element.Id);

                if (keys.Add(key))
                {
                    sources.Add(source);
                }
            }

            return sources;
        }

        private static bool TryResolveSourceReference(
            Document hostDoc,
            Reference reference,
            MepOpeningSettings settings,
            out MepOpeningSourceElement source)
        {
            source = null;
            if (reference == null || hostDoc == null || settings == null)
            {
                return false;
            }

            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                if (!settings.UseLinkedModelSources)
                {
                    return false;
                }

                RevitLinkInstance linkInstance = hostDoc.GetElement(reference.ElementId) as RevitLinkInstance;
                if (!IsAllowedSourceLink(linkInstance, settings))
                {
                    return false;
                }

                Document linkDoc = linkInstance.GetLinkDocument();
                Element linkedElement = linkDoc == null ? null : linkDoc.GetElement(reference.LinkedElementId);
                MepOpeningElementKind linkedKind;
                if (!MepOpeningSelectionFilter.TryGetElementKind(linkedElement, out linkedKind) ||
                    !IsElementKindIncluded(settings, linkedKind))
                {
                    return false;
                }

                source = MepOpeningSourceElement.FromLinked(
                    linkDoc,
                    linkedElement,
                    linkInstance,
                    GetCleanLinkName(linkInstance, linkDoc));
                return true;
            }

            if (!settings.UseCurrentModelSources)
            {
                return false;
            }

            Element currentElement = hostDoc.GetElement(reference.ElementId);
            MepOpeningElementKind currentKind;
            if (!MepOpeningSelectionFilter.TryGetElementKind(currentElement, out currentKind) ||
                !IsElementKindIncluded(settings, currentKind))
            {
                return false;
            }

            source = MepOpeningSourceElement.FromCurrent(hostDoc, currentElement);
            return true;
        }

        private static bool IsAllowedSourceLink(RevitLinkInstance linkInstance, MepOpeningSettings settings)
        {
            if (linkInstance == null || settings == null || linkInstance.GetLinkDocument() == null)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(settings.SourceLinkInstanceUniqueId) ||
                   string.Equals(linkInstance.UniqueId, settings.SourceLinkInstanceUniqueId, StringComparison.OrdinalIgnoreCase);
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
    }
}
