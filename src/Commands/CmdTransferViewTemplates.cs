// Tool Name: Transfer View Templates
// Description: Transfers selected view templates between open project documents with optional override.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-14
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.UI, AJTools.Utils

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
