#region Metadata
/*
 * Tool Name     : Transfer View Templates
 * File Name     : CmdTransferViewTemplates.cs
 * Purpose       : Copies selected view templates from one open project document to another, optionally
 *                 overriding same-named templates in the target and re-pointing the views that used them.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.2
 *
 * Created Date  : 2026-04-14
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI (TransferViewTemplatesWindow), AJTools.Utils
 *
 * Input         : Two or more open projects - source, target, templates, and override flag chosen in the window.
 * Output        : Selected templates copied into the target; views and view types' "View template applied
 *                 to new views" re-pointed when overriding; final report of transferred / updated / added.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Needs at least two open, non-linked project documents; refuses a read-only target (the window already
 *   blocks Transfer for one; the check here stays as a second guard).
 * - The whole transfer (remove + copy + reassign) runs in one TransactionGroup, so it reverses in one Ctrl+Z.
 * - Override mode is chosen explicitly in the window before anything is removed.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-14) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Transfer behaviour unchanged.
 * v1.1.1 (2026-07-28) - Audit: window is now owned by the Revit main window (WindowInteropHelper), so it cannot drop behind Revit.
 * v1.1.2 (2026-10-12) - Fixed: Override mode re-pointed only views, so a view type whose "View template
 *                       applied to new views" was a replaced template silently lost that setting when the
 *                       old template was deleted. Those links are now recorded first and re-pointed to the
 *                       new same-named template; any Revit will not accept is left empty and counted in
 *                       the report instead of failing the transfer.
 *                       Also: an unexpected error now shows one message, not the same text twice
 *                       (Revit's own error box is no longer given it).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Transfers selected view templates from one open project to another.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdTransferViewTemplates : IExternalCommand
    {
        private const string ToolTitle = "Transfer View Templates";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData?.Application;
            UIDocument uiDoc = uiApp?.ActiveUIDocument;
            Document activeDoc = uiDoc?.Document;

            if (uiApp == null || activeDoc == null)
            {
                message = "No active project document.";
                return Result.Failed;
            }

            var projectDocs = uiApp.Application.Documents
                .Cast<Document>()
                .Where(d => d != null && !d.IsFamilyDocument && !d.IsLinked)
                .OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (projectDocs.Count < 2)
            {
                DialogHelper.ShowError(ToolTitle, "At least two open project documents are required.");
                return Result.Cancelled;
            }

            var window = new TransferViewTemplatesWindow(projectDocs);
            new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true)
            {
                return Result.Cancelled;
            }

            Document sourceDoc = window.SourceDocument;
            Document targetDoc = window.TargetDocument;
            bool overrideExisting = window.OverrideExisting;
            List<ElementId> selectedTemplateIds = window.SelectedTemplateIds?.ToList() ?? new List<ElementId>();
            List<string> selectedTemplateNames = window.SelectedTemplateNames?.ToList() ?? new List<string>();

            if (sourceDoc == null || targetDoc == null)
            {
                DialogHelper.ShowError(ToolTitle, "Select both source and target projects.");
                return Result.Cancelled;
            }

            if (sourceDoc.Equals(targetDoc))
            {
                DialogHelper.ShowError(ToolTitle, "Source and target projects must be different.");
                return Result.Cancelled;
            }

            if (targetDoc.IsReadOnly)
            {
                DialogHelper.ShowError(ToolTitle, $"Target project \"{targetDoc.Title}\" is read-only.");
                return Result.Cancelled;
            }

            if (selectedTemplateIds.Count == 0)
            {
                DialogHelper.ShowError(ToolTitle, "No view templates were selected.");
                return Result.Cancelled;
            }

            var selectedNamesSet = new HashSet<string>(selectedTemplateNames, StringComparer.OrdinalIgnoreCase);
            var replacedAssignments = new Dictionary<string, List<ElementId>>(StringComparer.OrdinalIgnoreCase);
            var replacedTypeDefaults = new Dictionary<string, List<ElementId>>(StringComparer.OrdinalIgnoreCase);
            int typeDefaultsNotRestored = 0;

            try
            {
                using (var tg = new TransactionGroup(targetDoc, ToolTitle))
                {
                    tg.Start();

                    if (overrideExisting)
                    {
                        using (var tx = new Transaction(targetDoc, "Remove Existing View Templates"))
                        {
                            tx.Start();
                            // Must be read BEFORE the old templates are deleted - see RecordViewTypeDefaults.
                            replacedTypeDefaults = RecordViewTypeDefaults(targetDoc, selectedNamesSet);
                            replacedAssignments = RemoveViewTemplatesWithSameName(targetDoc, selectedNamesSet);
                            tx.Commit();
                        }
                    }

                    using (var tx = new Transaction(targetDoc, "Copy View Templates"))
                    {
                        tx.Start();

                        var copyOptions = new CopyPasteOptions();
                        copyOptions.SetDuplicateTypeNamesHandler(new UseDestinationDuplicateTypeNamesHandler());

                        ElementTransformUtils.CopyElements(
                            sourceDoc,
                            selectedTemplateIds,
                            targetDoc,
                            Transform.Identity,
                            copyOptions);

                        tx.Commit();
                    }

                    if (overrideExisting && replacedAssignments.Count > 0)
                    {
                        using (var tx = new Transaction(targetDoc, "Reassign Updated View Templates"))
                        {
                            tx.Start();
                            ReassignViewTemplates(targetDoc, replacedAssignments);
                            typeDefaultsNotRestored = ReassignViewTypeDefaults(targetDoc, replacedTypeDefaults);
                            tx.Commit();
                        }
                    }

                    tg.Assimilate();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(ToolTitle, "Transfer failed:\n\n" + ex.Message);
                return Result.Failed;
            }

            int updatedCount = selectedTemplateNames.Count(name => replacedAssignments.ContainsKey(name));
            int addedCount = selectedTemplateNames.Count - updatedCount;

            string summary = string.Format(
                "{0} view template(s) transferred.\n\nFrom: {1}\nTo: {2}\n\nUpdated: {3}\nAdded New: {4}",
                selectedTemplateIds.Count,
                sourceDoc.Title,
                targetDoc.Title,
                updatedCount,
                addedCount);

            if (overrideExisting)
            {
                summary += "\n\nOverride mode: ON";
            }

            if (typeDefaultsNotRestored > 0)
            {
                summary += string.Format(
                    "\n\n{0} view type(s) lost their \"View template applied to new views\" - the new template " +
                    "of that name could not be set on them. Set it again in the view type's Type Properties.",
                    typeDefaultsNotRestored);
            }

            DialogHelper.ShowInfo(ToolTitle, summary);
            return Result.Succeeded;
        }

        private static List<View> CollectViewTemplates(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null && v.IsTemplate)
                .ToList();
        }

        private static Dictionary<string, List<ElementId>> RemoveViewTemplatesWithSameName(
            Document targetDoc,
            ISet<string> selectedTemplateNames)
        {
            var replacedAssignments = new Dictionary<string, List<ElementId>>(StringComparer.OrdinalIgnoreCase);

            List<View> targetTemplates = CollectViewTemplates(targetDoc);
            List<View> targetViews = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null && !v.IsTemplate)
                .ToList();

            var selectedTemplatesById = targetTemplates
                .Where(vt => selectedTemplateNames.Contains(vt.Name))
                .ToDictionary(vt => vt.Id, vt => vt.Name);

            foreach (View view in targetViews)
            {
                ElementId templateId = view.ViewTemplateId;
                if (templateId == null || templateId == ElementId.InvalidElementId)
                {
                    continue;
                }

                if (!selectedTemplatesById.TryGetValue(templateId, out string templateName))
                {
                    continue;
                }

                if (!replacedAssignments.TryGetValue(templateName, out List<ElementId> assignedViewIds))
                {
                    assignedViewIds = new List<ElementId>();
                    replacedAssignments[templateName] = assignedViewIds;
                }

                assignedViewIds.Add(view.Id);
            }

            foreach (View template in targetTemplates)
            {
                if (!selectedTemplateNames.Contains(template.Name))
                {
                    continue;
                }

                if (!replacedAssignments.ContainsKey(template.Name))
                {
                    replacedAssignments[template.Name] = new List<ElementId>();
                }

                targetDoc.Delete(template.Id);
            }

            return replacedAssignments;
        }

        private static void ReassignViewTemplates(Document targetDoc, IDictionary<string, List<ElementId>> assignments)
        {
            var templatesByName = CollectViewTemplates(targetDoc)
                .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<ElementId>> assignment in assignments)
            {
                if (!templatesByName.TryGetValue(assignment.Key, out View newTemplate))
                {
                    continue;
                }

                foreach (ElementId viewId in assignment.Value)
                {
                    View view = targetDoc.GetElement(viewId) as View;
                    if (view == null || view.IsTemplate)
                    {
                        continue;
                    }

                    view.ViewTemplateId = newTemplate.Id;
                }
            }
        }

        /// <summary>
        /// View types whose "View template applied to new views" (ViewFamilyType.DefaultTemplateId) points at
        /// a target template about to be replaced, keyed by that template's name.
        /// </summary>
        /// <remarks>
        /// The view type is not deleted with the old template, so without this record its setting would
        /// simply be lost when the old template goes. Read it before the delete; it is re-pointed to the new
        /// same-named template afterwards, the same way views are (ReassignViewTypeDefaults).
        /// </remarks>
        private static Dictionary<string, List<ElementId>> RecordViewTypeDefaults(
            Document targetDoc,
            ISet<string> selectedTemplateNames)
        {
            var typeDefaults = new Dictionary<string, List<ElementId>>(StringComparer.OrdinalIgnoreCase);

            Dictionary<ElementId, string> selectedTemplatesById = CollectViewTemplates(targetDoc)
                .Where(vt => selectedTemplateNames.Contains(vt.Name))
                .ToDictionary(vt => vt.Id, vt => vt.Name);

            if (selectedTemplatesById.Count == 0)
            {
                return typeDefaults;
            }

            IEnumerable<ViewFamilyType> viewTypes = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>();

            foreach (ViewFamilyType viewType in viewTypes)
            {
                ElementId templateId = SafeGetDefaultTemplateId(viewType);
                if (templateId == null || templateId == ElementId.InvalidElementId)
                {
                    continue;
                }

                if (!selectedTemplatesById.TryGetValue(templateId, out string templateName))
                {
                    continue;
                }

                if (!typeDefaults.TryGetValue(templateName, out List<ElementId> viewTypeIds))
                {
                    viewTypeIds = new List<ElementId>();
                    typeDefaults[templateName] = viewTypeIds;
                }

                viewTypeIds.Add(viewType.Id);
            }

            return typeDefaults;
        }

        /// <summary>
        /// Points each recorded view type's "View template applied to new views" at the new same-named
        /// template. Returns how many could not be restored.
        /// </summary>
        /// <remarks>
        /// A same-named template from the source can be for a different kind of view than the one it
        /// replaces, and Revit refuses it as that view type's default (IsValidDefaultTemplate). That one is
        /// left empty and counted - the old link is gone either way - rather than failing the whole transfer.
        /// </remarks>
        private static int ReassignViewTypeDefaults(Document targetDoc, IDictionary<string, List<ElementId>> typeDefaults)
        {
            int notRestored = 0;
            if (typeDefaults == null || typeDefaults.Count == 0)
            {
                return notRestored;
            }

            var templatesByName = CollectViewTemplates(targetDoc)
                .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<ElementId>> entry in typeDefaults)
            {
                templatesByName.TryGetValue(entry.Key, out View newTemplate);

                foreach (ElementId viewTypeId in entry.Value)
                {
                    var viewType = targetDoc.GetElement(viewTypeId) as ViewFamilyType;
                    if (viewType == null)
                    {
                        continue;
                    }

                    if (newTemplate == null || !viewType.IsValidDefaultTemplate(newTemplate.Id))
                    {
                        notRestored++;
                        continue;
                    }

                    viewType.DefaultTemplateId = newTemplate.Id;
                }
            }

            return notRestored;
        }

        private static ElementId SafeGetDefaultTemplateId(ViewFamilyType viewType)
        {
            try
            {
                return viewType != null ? viewType.DefaultTemplateId : ElementId.InvalidElementId;
            }
            catch
            {
                // A view type that cannot report one simply has nothing to restore - never fail the transfer.
                return ElementId.InvalidElementId;
            }
        }

        private sealed class UseDestinationDuplicateTypeNamesHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}
