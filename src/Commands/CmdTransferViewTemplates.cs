#region Metadata
/*
 * Tool Name     : Transfer View Templates
 * File Name     : CmdTransferViewTemplates.cs
 * Purpose       : Copies selected view templates from one open project document to another, optionally
 *                 overriding same-named templates in the target and re-pointing the views that used them.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-14
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI (TransferViewTemplatesWindow), AJTools.Utils
 *
 * Input         : Two or more open projects - source, target, templates, and override flag chosen in the window.
 * Output        : Selected templates copied into the target; views re-pointed when overriding; final
 *                 report of transferred / updated / added.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Needs at least two open, non-linked project documents; refuses a read-only target.
 * - The whole transfer (remove + copy + reassign) runs in one TransactionGroup, so it reverses in one Ctrl+Z.
 * - Override mode is chosen explicitly in the window before anything is removed.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-14) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Transfer behaviour unchanged.
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

            // Capture which selected names already exist in the target BEFORE copying, so the summary
            // can tell genuinely-new templates apart from ones reused from the target in non-override mode.
            var existingTargetTemplateNames = new HashSet<string>(
                CollectViewTemplates(targetDoc).Select(v => v.Name), StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var tg = new TransactionGroup(targetDoc, "AJ Tools - Transfer View Templates"))
                {
                    tg.Start();

                    if (overrideExisting)
                    {
                        using (var tx = new Transaction(targetDoc, "Remove Existing View Templates"))
                        {
                            tx.Start();
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
                message = ex.Message;
                DialogHelper.ShowError(ToolTitle, "Transfer failed:\n\n" + ex.Message);
                return Result.Failed;
            }

            int updatedCount = selectedTemplateNames.Count(name => replacedAssignments.ContainsKey(name));

            // In non-override mode a selected template whose name already existed in the target is reused
            // from the target (the duplicate-name handler keeps the destination copy), not added as new.
            int reusedCount = overrideExisting
                ? 0
                : selectedTemplateNames.Count(name => !replacedAssignments.ContainsKey(name)
                                                      && existingTargetTemplateNames.Contains(name));
            int addedCount = selectedTemplateNames.Count - updatedCount - reusedCount;

            string summary = string.Format(
                "{0} view template(s) transferred.\n\nFrom: {1}\nTo: {2}\n\nUpdated: {3}\nAdded New: {4}",
                selectedTemplateIds.Count,
                sourceDoc.Title,
                targetDoc.Title,
                updatedCount,
                addedCount);

            if (reusedCount > 0)
            {
                summary += string.Format("\nAlready existed (kept target's): {0}", reusedCount);
            }

            if (overrideExisting)
            {
                summary += "\n\nOverride mode: ON";
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

        private sealed class UseDestinationDuplicateTypeNamesHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}
