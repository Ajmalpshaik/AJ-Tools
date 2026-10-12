// ==================================================
// Tool Name    : Purge Unplaced Views
// Purpose      : Checks each unplaced view with a rolled-back delete and deletes the selected safe ones
//                (shared engine behind the five Purge Unplaced tools).
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-05-11
// Last Updated : 2026-10-12
// Target       : Revit 2020 - 2027
// Framework    : .NET Framework 4.7.2 (2020) / 4.8 (2021-2024) / .NET 8 (2025-2026) / .NET 10 (2027)
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Ported from an earlier AJ Tools script.
//                v1.0.1 (2026-10-12) - Fixed: a view whose delete also takes other views with it
//                (dependent views, callout views drawn in it - and their viewports when those are on a
//                sheet) was marked Safe and deleted, because only the target's own id was checked in
//                Revit's deleted-id set. Probe and delete now refuse it and say how many would go.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.Purge;

using AJTools.Utils;
namespace AJTools.Services.Purge
{
    internal sealed class UnplacedViewPurgeService
    {
        private readonly Document _doc;
        private readonly ElementId _activeViewId;
        private readonly UnplacedViewPurgeMode _mode;
        private readonly UnplacedViewCollector _collector;

        public UnplacedViewPurgeService(
            Document doc,
            ElementId activeViewId,
            UnplacedViewPurgeMode mode)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _activeViewId = activeViewId ?? ElementId.InvalidElementId;
            _mode = mode;
            _collector = new UnplacedViewCollector(_doc, _activeViewId, mode);
        }

        /// <summary>
        /// Collects the candidates and works out which of them Revit will actually let go.
        /// </summary>
        /// <param name="onProgress">
        /// Optional (done, total) callback, raised once per candidate - purely for a progress bar. It
        /// cannot change what is scanned or what the scan concludes, and existing callers that pass
        /// nothing behave exactly as before. Worth having because every candidate costs a trial delete
        /// inside a rolled-back transaction.
        /// </param>
        public IList<UnplacedViewPurgeItem> Scan(Action<int, int> onProgress = null)
        {
            IList<UnplacedViewPurgeItem> items = _collector.Collect();

            // Built once for the whole scan, not once per candidate.
            HashSet<long> viewAndPlacementIds = CollectViewAndPlacementIds();

            int done = 0;
            int total = items.Count;

            foreach (UnplacedViewPurgeItem item in items)
            {
                ProbeDelete(item, viewAndPlacementIds);

                done++;
                if (onProgress != null)
                {
                    try { onProgress(done, total); }
                    catch { /* A reporting fault must never abort the scan. */ }
                }
            }

            return items
                .OrderBy(i => i.Status)
                .ThenBy(i => i.ViewKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ViewName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ViewIdValue)
                .ToList();
        }

        public UnplacedViewPurgeResult DeleteSelected(
            IList<UnplacedViewPurgeItem> selectedItems,
            int foundCount)
        {
            var result = new UnplacedViewPurgeResult
            {
                FoundCount = foundCount
            };

            if (selectedItems == null || selectedItems.Count == 0)
            {
                return result;
            }

            if (_doc.IsReadOnly)
            {
                foreach (UnplacedViewPurgeItem item in selectedItems)
                {
                    result.AttemptedCount++;
                    result.AddSkipped(GetItemName(item), GetItemIdValue(item), "Document is read-only.");
                }

                return result;
            }

            // Built once for the whole run - same check as the scan's probe, applied before each commit.
            HashSet<long> viewAndPlacementIds = CollectViewAndPlacementIds();

            using (var group = new TransactionGroup(_doc, _mode.GetTransactionName()))
            {
                group.Start();
                bool hasCommit = false;

                foreach (UnplacedViewPurgeItem item in selectedItems)
                {
                    result.AttemptedCount++;

                    string validationReason;
                    View currentView = GetValidCurrentView(item, true, out validationReason);
                    if (currentView == null)
                    {
                        result.AddSkipped(GetItemName(item), GetItemIdValue(item), validationReason);
                        continue;
                    }

                    using (var transaction = new Transaction(_doc, "Delete Unplaced View"))
                    {
                        try
                        {
                            ElementId viewIdToDelete = currentView.Id;
                            transaction.Start();
                            ICollection<ElementId> deletedIds = _doc.Delete(viewIdToDelete);
                            if (!ContainsId(deletedIds, viewIdToDelete))
                            {
                                RollBackIfStarted(transaction);
                                result.AddFailure(
                                    GetItemName(item),
                                    GetItemIdValue(item),
                                    "Revit did not report the selected view as deleted.");
                                continue;
                            }

                            int alsoDeleted = CountOtherViewsOrPlacements(deletedIds, viewIdToDelete, viewAndPlacementIds);
                            if (alsoDeleted > 0)
                            {
                                RollBackIfStarted(transaction);
                                result.AddSkipped(
                                    GetItemName(item),
                                    GetItemIdValue(item),
                                    DescribeOtherViewsDeleted(alsoDeleted));
                                continue;
                            }

                            transaction.Commit();
                            hasCommit = true;
                            result.DeletedCount++;
                        }
                        catch (Exception ex)
                        {
                            RollBackIfStarted(transaction);
                            result.AddFailure(GetItemName(item), GetItemIdValue(item), ex.Message);
                        }
                    }
                }

                if (hasCommit)
                {
                    group.Assimilate();
                }
                else
                {
                    group.RollBack();
                }
            }

            return result;
        }

        private void ProbeDelete(UnplacedViewPurgeItem item, ISet<long> viewAndPlacementIds)
        {
            if (item == null ||
                item.Status == UnplacedViewPurgeStatus.Skipped ||
                _doc.IsReadOnly)
            {
                return;
            }

            string validationReason;
            View currentView = GetValidCurrentView(item, false, out validationReason);
            if (currentView == null)
            {
                item.MarkCannotDelete(
                    validationReason,
                    "The view was found during collection but failed the current safety validation.");
                return;
            }

            using (var transaction = new Transaction(_doc, "Probe Unplaced View Delete"))
            {
                try
                {
                    ElementId viewIdToProbe = currentView.Id;
                    transaction.Start();
                    ICollection<ElementId> deletedIds = _doc.Delete(viewIdToProbe);
                    transaction.RollBack();

                    int alsoDeleted = CountOtherViewsOrPlacements(deletedIds, viewIdToProbe, viewAndPlacementIds);

                    if (!ContainsId(deletedIds, viewIdToProbe))
                    {
                        item.MarkCannotDelete(
                            "Delete probe did not include this view.",
                            "Revit did not confirm that this view would be deleted.");
                    }
                    else if (alsoDeleted > 0)
                    {
                        item.MarkCannotDelete(
                            DescribeOtherViewsDeleted(alsoDeleted),
                            "Revit's delete check showed that removing this view also removes other views - usually " +
                            "its dependent views or callout views drawn inside it - and their places on sheets if they " +
                            "are placed. AJ Tools only purges a view that goes on its own, so this one is kept.");
                    }
                    else
                    {
                        item.MarkSafe(
                            "Unplaced, non-template view passed Revit delete probe.",
                            "AJ Tools verified that Revit allows this view to be deleted inside a rolled-back transaction.");
                    }
                }
                catch (Exception ex)
                {
                    RollBackIfStarted(transaction);
                    item.MarkCannotDelete(
                        ex.Message,
                        "Revit rejected the rolled-back delete probe for this view.");
                }
            }
        }

        private View GetValidCurrentView(
            UnplacedViewPurgeItem item,
            bool requireSafeSelection,
            out string reason)
        {
            reason = string.Empty;
            if (item == null)
            {
                reason = "Selected row is invalid.";
                return null;
            }

            if (requireSafeSelection && !item.CanSelectForDeletion)
            {
                reason = string.IsNullOrWhiteSpace(item.StatusReason)
                    ? "View is not marked safe to purge."
                    : item.StatusReason;
                return null;
            }

            var viewId = ElementIdHelper.FromInt(item.ViewIdValue);
            View view = _doc.GetElement(viewId) as View;
            if (view == null)
            {
                reason = "View no longer exists.";
                return null;
            }

            if (view.IsTemplate)
            {
                reason = "View became a template and was skipped.";
                return null;
            }

            if (IsSameId(view.Id, _activeViewId))
            {
                reason = "Active view is never deleted.";
                return null;
            }

            if (_collector.IsViewPlaced(view.Id))
            {
                reason = "View is now placed on a sheet.";
                return null;
            }

            if (IsDefault3DView(view))
            {
                reason = "Default {3D} view is skipped.";
                return null;
            }

            if (!MatchesExpectedKind(view, item.ViewKind))
            {
                reason = "View type changed after scan.";
                return null;
            }

            return view;
        }

        private static bool MatchesExpectedKind(View view, string viewKind)
        {
            if (view == null)
            {
                return false;
            }

            if (string.Equals(viewKind, "3D View", StringComparison.OrdinalIgnoreCase))
            {
                return view is View3D && view.ViewType == ViewType.ThreeD;
            }

            if (string.Equals(viewKind, "Section", StringComparison.OrdinalIgnoreCase))
            {
                return view is ViewSection && view.ViewType == ViewType.Section;
            }

            if (string.Equals(viewKind, "Schedule", StringComparison.OrdinalIgnoreCase))
            {
                return view is ViewSchedule;
            }

            if (string.Equals(viewKind, "Legend", StringComparison.OrdinalIgnoreCase))
            {
                return view.ViewType == ViewType.Legend;
            }

            if (string.Equals(viewKind, "Drafting View", StringComparison.OrdinalIgnoreCase))
            {
                return view.ViewType == ViewType.DraftingView;
            }

            return false;
        }

        private static bool ContainsId(IEnumerable<ElementId> ids, ElementId targetId)
        {
            if (ids == null || targetId == null || targetId == ElementId.InvalidElementId)
            {
                return false;
            }

            int target = targetId.IntValue();
            return ids.Any(id => id != null && id.IntValue() == target);
        }

        /// <summary>
        /// Ids of every view (of any kind - sheets, schedules and templates included), every viewport and
        /// every schedule placement in the document.
        /// </summary>
        /// <remarks>
        /// Document.Delete on a view also deletes "any elements that are totally dependent upon" it (the API's
        /// own wording) and reports them only in the id set it returns. That set legitimately holds the view's
        /// own markers and internal elements, but it also holds its dependent views and any callout view drawn
        /// inside it - with their viewports, if those views sit on a sheet. Checking the target's own id alone
        /// cannot tell those apart. The target is unplaced, so it has no viewport or schedule placement of its
        /// own: any of THESE ids other than the target in the deleted set means another view would go with it.
        /// </remarks>
        private HashSet<long> CollectViewAndPlacementIds()
        {
            var ids = new HashSet<long>();
            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(View)).ToElementIds());
            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(Viewport)).ToElementIds());
            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(ScheduleSheetInstance)).ToElementIds());
            return ids;
        }

        private static void AddIds(ISet<long> target, IEnumerable<ElementId> ids)
        {
            foreach (ElementId id in ids)
            {
                if (id != null && id != ElementId.InvalidElementId)
                {
                    target.Add(id.LongValue());
                }
            }
        }

        /// <summary>
        /// How many views or sheet placements OTHER than the target are in a Document.Delete result.
        /// </summary>
        private static int CountOtherViewsOrPlacements(
            IEnumerable<ElementId> deletedIds,
            ElementId targetId,
            ISet<long> viewAndPlacementIds)
        {
            if (deletedIds == null || viewAndPlacementIds == null || viewAndPlacementIds.Count == 0)
            {
                return 0;
            }

            long target = targetId != null ? targetId.LongValue() : ElementId.InvalidElementId.LongValue();
            return deletedIds.Count(id =>
                id != null &&
                id.LongValue() != target &&
                viewAndPlacementIds.Contains(id.LongValue()));
        }

        private static string DescribeOtherViewsDeleted(int count)
        {
            return "Deleting it would also delete " + count +
                   " other view(s) or sheet placement(s) (dependent views or callouts).";
        }

        private static bool IsDefault3DView(View view)
        {
            return view != null &&
                   view.ViewType == ViewType.ThreeD &&
                   string.Equals(SafeGetName(view), "{3D}", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameId(ElementId first, ElementId second)
        {
            return first != null &&
                   second != null &&
                   first != ElementId.InvalidElementId &&
                   second != ElementId.InvalidElementId &&
                   first.IntValue() == second.IntValue();
        }

        private static void RollBackIfStarted(Transaction transaction)
        {
            if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
            {
                transaction.RollBack();
            }
        }

        private static string GetItemName(UnplacedViewPurgeItem item)
        {
            return item != null ? item.ViewName : "Unknown";
        }

        private static int GetItemIdValue(UnplacedViewPurgeItem item)
        {
            return item != null ? item.ViewIdValue : int.MinValue;
        }

        private static string SafeGetName(View view)
        {
            try
            {
                return view != null && !string.IsNullOrWhiteSpace(view.Name)
                    ? view.Name
                    : "<unnamed view>";
            }
            catch
            {
                return "<unnamed view>";
            }
        }
    }
}
