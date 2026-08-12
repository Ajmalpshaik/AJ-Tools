#region Metadata
/*
 * Tool Name     : Transfer Views (shared runner)
 * File Name     : TransferViewsCommandRunner.cs
 * Purpose       : Shared runner used by Transfer Schedules / Transfer Legends / Transfer Drafting Views.
 *                 Copies selected elements from one open project document to another, optionally
 *                 overriding same-named elements in the target and restoring their sheet placement(s).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-08-11
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI.Transfer (TransferViewsWindow), AJTools.Utils
 *
 * Input         : Two or more open projects - source, target, items, and override flag chosen in the window.
 * Output        : Selected elements copied into the target; sheet placements restored when overriding;
 *                 final report of transferred / updated / added / any placement-restore warnings.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Needs at least two open, non-linked project documents; refuses a
 *   read-only target - same shape as the existing, separate CmdTransferViewTemplates.cs.
 * - Legends/Drafting Views are placed via Viewport; Schedules via ScheduleSheetInstance. Override mode
 *   records every existing placement (a Legend can be placed on several sheets at once) before deleting
 *   the old element, then recreates one placement per recorded entry against the freshly copied element.
 * - A placement that fails to recreate (e.g. no longer fits on the sheet) does not fail the whole
 *   transfer - the copy still succeeds and the failure is listed in the final report for Ajmal to place
 *   manually.
 * - LEGENDS AND DRAFTING VIEWS ARE COPIED IN TWO PASSES, and both are required. The document-to-document
 *   ElementTransformUtils.CopyElements overload copies the view SHELL ONLY - it does not bring across the
 *   lines, text, filled regions or legend components drawn inside it. So each view is copied on its own
 *   first, then its drawn contents are copied into the freshly created view with the VIEW-TO-VIEW overload.
 *   Verified live on Revit 2020 (2026-08-11): a 131-element drafting view copied doc-to-doc returned a
 *   single element id and arrived holding only its internal ExtentElem; the second pass brought all 130
 *   real items over and the copy read back at 131/131. Do not "simplify" this back into one bulk call.
 * - Schedules deliberately keep the single bulk copy - a ViewSchedule's rows are generated from the target
 *   model's own elements, so there is nothing drawn inside it to copy.
 * - The whole transfer (remove + copy + reassign) runs in one TransactionGroup, so it reverses in one Ctrl+Z.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.1.0 (2026-08-11) - Fixed: transferred legends / drafting views arrived EMPTY. Added the second,
 *                       view-to-view content pass described above, plus content warnings in the report.
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.Transfer;
using AJTools.Services.Transfer;
using AJTools.UI.Transfer;
using AJTools.Utils;

namespace AJTools.Commands
{
    internal static class TransferViewsCommandRunner
    {
        public static Result Execute(ExternalCommandData commandData, ref string message, TransferViewKind kind)
        {
            string toolTitle = kind.GetToolTitle();

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
                DialogHelper.ShowError(toolTitle, "At least two open project documents are required.");
                return Result.Cancelled;
            }

            var window = new TransferViewsWindow(projectDocs, kind);
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
            List<ElementId> selectedIds = window.SelectedElementIds?.ToList() ?? new List<ElementId>();
            List<string> selectedNames = window.SelectedElementNames?.ToList() ?? new List<string>();

            if (sourceDoc == null || targetDoc == null)
            {
                DialogHelper.ShowError(toolTitle, "Select both source and target projects.");
                return Result.Cancelled;
            }

            if (sourceDoc.Equals(targetDoc))
            {
                DialogHelper.ShowError(toolTitle, "Source and target projects must be different.");
                return Result.Cancelled;
            }

            if (targetDoc.IsReadOnly)
            {
                DialogHelper.ShowError(toolTitle, $"Target project \"{targetDoc.Title}\" is read-only.");
                return Result.Cancelled;
            }

            if (selectedIds.Count == 0)
            {
                DialogHelper.ShowError(toolTitle, "No " + kind.GetNounPlural() + " were selected.");
                return Result.Cancelled;
            }

            var selectedNamesSet = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
            var replacedPlacements = new Dictionary<string, List<PlacementRecord>>(StringComparer.OrdinalIgnoreCase);
            var placementWarnings = new List<string>();
            var contentWarnings = new List<string>();
            int contentsCopied = 0;

            try
            {
                using (var tg = new TransactionGroup(targetDoc, toolTitle))
                {
                    tg.Start();

                    if (overrideExisting)
                    {
                        using (var tx = new Transaction(targetDoc, "Remove Existing " + toolTitle))
                        {
                            tx.Start();
                            replacedPlacements = RemoveExistingWithSameName(targetDoc, kind, selectedNamesSet);
                            tx.Commit();
                        }
                    }

                    using (var tx = new Transaction(targetDoc, "Copy " + toolTitle))
                    {
                        tx.Start();

                        var copyOptions = new CopyPasteOptions();
                        copyOptions.SetDuplicateTypeNamesHandler(new UseDestinationDuplicateTypeNamesHandler());

                        if (kind == TransferViewKind.Schedule)
                        {
                            // A schedule has no drawn contents - its rows come from the target model itself.
                            ElementTransformUtils.CopyElements(
                                sourceDoc,
                                selectedIds,
                                targetDoc,
                                Transform.Identity,
                                copyOptions);
                        }
                        else
                        {
                            contentWarnings = CopyViewsWithContents(
                                sourceDoc,
                                selectedIds,
                                targetDoc,
                                copyOptions,
                                out contentsCopied);
                        }

                        tx.Commit();
                    }

                    if (overrideExisting && replacedPlacements.Count > 0)
                    {
                        using (var tx = new Transaction(targetDoc, "Restore Sheet Placements"))
                        {
                            tx.Start();
                            placementWarnings = ReassignPlacements(targetDoc, kind, replacedPlacements);
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
                DialogHelper.ShowError(toolTitle, "Transfer failed:\n\n" + ex.Message);
                return Result.Failed;
            }

            int updatedCount = selectedNames.Count(name => replacedPlacements.ContainsKey(name));
            int addedCount = selectedNames.Count - updatedCount;

            var summary = new StringBuilder();
            summary.AppendFormat(
                "{0} {1} transferred.\n\nFrom: {2}\nTo: {3}\n\nUpdated: {4}\nAdded New: {5}",
                selectedIds.Count,
                selectedIds.Count == 1 ? kind.GetNounSingular() : kind.GetNounPlural(),
                sourceDoc.Title,
                targetDoc.Title,
                updatedCount,
                addedCount);

            if (kind != TransferViewKind.Schedule)
            {
                summary.Append("\nItems copied inside: ").Append(contentsCopied);
            }

            if (overrideExisting)
            {
                summary.Append("\n\nOverride mode: ON");
            }

            if (contentWarnings.Count > 0)
            {
                summary.Append("\n\nContent warnings:\n");
                foreach (string warning in contentWarnings.Take(10))
                {
                    summary.Append("- ").Append(warning).Append('\n');
                }

                if (contentWarnings.Count > 10)
                {
                    summary.Append("- +").Append(contentWarnings.Count - 10).Append(" more.\n");
                }
            }

            if (placementWarnings.Count > 0)
            {
                summary.Append("\n\nSheet placement warnings:\n");
                foreach (string warning in placementWarnings.Take(10))
                {
                    summary.Append("- ").Append(warning).Append('\n');
                }

                if (placementWarnings.Count > 10)
                {
                    summary.Append("- +").Append(placementWarnings.Count - 10).Append(" more.\n");
                }
            }

            DialogHelper.ShowInfo(toolTitle, summary.ToString());
            return Result.Succeeded;
        }

        /// <summary>
        /// Copies legend / drafting views together with everything drawn inside them.
        /// </summary>
        /// <remarks>
        /// Two passes are required, and this must not be collapsed back into one bulk call.
        /// The document-to-document CopyElements overload copies the view SHELL ONLY - verified live on
        /// Revit 2020 (2026-08-11), a 131-element drafting view came across as an empty view holding only
        /// its internal ExtentElem. The view-to-view overload is the one that carries detail lines, text
        /// notes, filled regions and legend components, so it is run as a second pass per view.
        /// Views are copied ONE AT A TIME so the id returned by the first pass can be paired back to the
        /// source view it came from - a bulk call returns a flat collection with no such guarantee.
        /// </remarks>
        private static List<string> CopyViewsWithContents(
            Document sourceDoc,
            IEnumerable<ElementId> viewIds,
            Document targetDoc,
            CopyPasteOptions copyOptions,
            out int contentsCopied)
        {
            var warnings = new List<string>();
            contentsCopied = 0;

            foreach (ElementId viewId in viewIds)
            {
                var sourceView = sourceDoc.GetElement(viewId) as View;
                if (sourceView == null)
                {
                    continue;
                }

                // Pass 1 - the view itself.
                ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(
                    sourceDoc,
                    new List<ElementId> { viewId },
                    targetDoc,
                    Transform.Identity,
                    copyOptions);

                View newView = copiedIds
                    .Select(id => targetDoc.GetElement(id))
                    .OfType<View>()
                    .FirstOrDefault();

                if (newView == null)
                {
                    warnings.Add($"\"{sourceView.Name}\": the copied view could not be identified, so its contents were not copied.");
                    continue;
                }

                // Pass 2 - everything drawn inside it. ExtentElem (the view's own internal extent marker)
                // has no category and must be left out; it is recreated with the view.
                List<ElementId> contentIds = new FilteredElementCollector(sourceDoc, sourceView.Id)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(e => e != null && e.Category != null && e.Id != sourceView.Id)
                    .Select(e => e.Id)
                    .ToList();

                if (contentIds.Count == 0)
                {
                    continue;
                }

                try
                {
                    ICollection<ElementId> copiedContents = ElementTransformUtils.CopyElements(
                        sourceView,
                        contentIds,
                        newView,
                        Transform.Identity,
                        copyOptions);

                    contentsCopied += copiedContents?.Count ?? 0;
                }
                catch (Exception ex)
                {
                    warnings.Add($"\"{sourceView.Name}\": the view was copied but its {contentIds.Count} item(s) inside were not - {ex.Message}");
                }
            }

            return warnings;
        }

        private static Dictionary<string, List<PlacementRecord>> RemoveExistingWithSameName(
            Document targetDoc,
            TransferViewKind kind,
            ISet<string> selectedNames)
        {
            var replaced = new Dictionary<string, List<PlacementRecord>>(StringComparer.OrdinalIgnoreCase);

            List<Element> targetCandidates = TransferElementCollector.Collect(targetDoc, kind);
            Dictionary<ElementId, string> matchingByName = targetCandidates
                .Where(e => selectedNames.Contains(e.Name))
                .ToDictionary(e => e.Id, e => e.Name);

            if (matchingByName.Count == 0)
            {
                return replaced;
            }

            if (kind == TransferViewKind.Schedule)
            {
                IEnumerable<ScheduleSheetInstance> placements = new FilteredElementCollector(targetDoc)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>();

                foreach (ScheduleSheetInstance placement in placements)
                {
                    if (placement?.ScheduleId == null ||
                        !matchingByName.TryGetValue(placement.ScheduleId, out string name))
                    {
                        continue;
                    }

                    AddPlacement(replaced, name, placement.OwnerViewId, placement.Point);
                }
            }
            else
            {
                IEnumerable<Viewport> viewports = new FilteredElementCollector(targetDoc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>();

                foreach (Viewport viewport in viewports)
                {
                    if (viewport?.ViewId == null ||
                        !matchingByName.TryGetValue(viewport.ViewId, out string name))
                    {
                        continue;
                    }

                    AddPlacement(replaced, name, viewport.SheetId, viewport.GetBoxCenter());
                }
            }

            foreach (KeyValuePair<ElementId, string> match in matchingByName)
            {
                if (!replaced.ContainsKey(match.Value))
                {
                    replaced[match.Value] = new List<PlacementRecord>();
                }

                targetDoc.Delete(match.Key);
            }

            return replaced;
        }

        private static List<string> ReassignPlacements(
            Document targetDoc,
            TransferViewKind kind,
            IDictionary<string, List<PlacementRecord>> replacedPlacements)
        {
            var warnings = new List<string>();

            Dictionary<string, Element> newElementsByName = TransferElementCollector.Collect(targetDoc, kind)
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<PlacementRecord>> entry in replacedPlacements)
            {
                if (entry.Value == null || entry.Value.Count == 0)
                {
                    continue;
                }

                if (!newElementsByName.TryGetValue(entry.Key, out Element newElement))
                {
                    warnings.Add($"\"{entry.Key}\": copy not found afterward, could not restore {entry.Value.Count} sheet placement(s).");
                    continue;
                }

                foreach (PlacementRecord placement in entry.Value)
                {
                    try
                    {
                        if (kind == TransferViewKind.Schedule)
                        {
                            ScheduleSheetInstance.Create(targetDoc, placement.SheetId, newElement.Id, placement.Point);
                        }
                        else
                        {
                            Viewport.Create(targetDoc, placement.SheetId, newElement.Id, placement.Point);
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"\"{entry.Key}\" on {DescribeSheet(targetDoc, placement.SheetId)}: {ex.Message}");
                    }
                }
            }

            return warnings;
        }

        private static void AddPlacement(
            Dictionary<string, List<PlacementRecord>> replaced,
            string name,
            ElementId sheetId,
            XYZ point)
        {
            if (!replaced.TryGetValue(name, out List<PlacementRecord> list))
            {
                list = new List<PlacementRecord>();
                replaced[name] = list;
            }

            list.Add(new PlacementRecord(sheetId, point));
        }

        private static string DescribeSheet(Document doc, ElementId sheetId)
        {
            var sheet = doc.GetElement(sheetId) as ViewSheet;
            return sheet != null ? $"{sheet.SheetNumber} - {sheet.Name}" : "a sheet";
        }

        private sealed class PlacementRecord
        {
            public PlacementRecord(ElementId sheetId, XYZ point)
            {
                SheetId = sheetId;
                Point = point;
            }

            public ElementId SheetId { get; }
            public XYZ Point { get; }
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
