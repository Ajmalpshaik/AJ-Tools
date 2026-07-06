#region Metadata
/*
 * Tool Name     : Create Openings
 * File Name     : CmdCreateMepOpenings.cs
 * Purpose       : Revit external command entry point for automatic MEP opening creation.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-03
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.MepOpenings
 *
 * Input         : Selection-scope MEP sources or opening hosts, based on Opening Settings.
 * Output        : Direct openings in current-model walls, floors/slabs, and beams.
 *
 * Notes         :
 * - One transaction named "AJ-Tools: Create Openings" is used for all model changes.
 * - Normal create runs do not show confirmation or success/report popups.
 * - Linked source elements are read only and transformed into current-model coordinates.
 * - Linked host elements are not modified; they require the later family opening mode.
 * - Direct wall openings are rectangular in the Revit API; circle settings use a bounding rectangle on walls.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
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

                if (result.Failed > 0 && result.OpeningsCreated == 0)
                {
                    return Result.Failed;
                }

                // A run that created nothing and reported nothing as "failed" (e.g. everything was
                // unchecked in Opening Settings, or every crossing was already covered) must still tell
                // the modeller why - a bare silent success here looks identical to "it worked".
                if (result.OpeningsCreated == 0)
                {
                    string reasons = string.Join("\n - ", result.GetTopSkipReasons());
                    TaskDialog.Show(
                        "Create Openings",
                        "No openings were created." +
                        (string.IsNullOrWhiteSpace(reasons) ? string.Empty : "\n\nReasons:\n - " + reasons));
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Create Openings", "Openings could not be created:\n\n" + ex.Message);
                return Result.Failed;
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

        private static bool ShouldRunDirectOpenings(MepOpeningSettings settings)
        {
            if (settings == null || !settings.UseCurrentModelHosts)
            {
                return false;
            }

            return settings.CreationMode == MepOpeningCreationMode.DirectOpening;
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
