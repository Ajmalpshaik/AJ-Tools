#region Metadata
/*
 * Tool Name     : Purge Unused Elements (shared service)
 * File Name     : UnusedElementPurgeService.cs
 * Purpose       : Scans for unused View Templates/Filters/Groups, probes each with a rolled-back
 *                 Document.Delete to confirm Revit really allows it, and deletes the selected safe ones.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Purge, AJTools.Utils
 *
 * Input         : Document, UnusedElementPurgeMode; selected items to delete.
 * Output        : Scanned/probed item list; delete result with counts and skip/failure reasons.
 *
 * Notes         :
 * - The rolled-back Document.Delete probe is the safety net for what the static scan cannot see: an
 *   element Revit refuses to delete, or one whose delete would also take other in-use elements with it
 *   (views, sheet placements, filters, groups, types, model elements - see CollectInUseIds for the rule
 *   per kind). Both are marked Cannot Delete before anything is removed, and the real delete re-applies
 *   the same check before each commit. What the probe CANNOT catch is a reference that Revit simply
 *   clears on delete (nothing else is deleted) - e.g. a view type's "View template applied to new
 *   views"; those have to be found by the scan in UnusedElementCollector.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
 * v1.0.1 (2026-10-12) - Fixed: the probe only checked that the target itself was in Revit's deleted-id
 *                       set, so an element whose delete also removed other in-use elements was still
 *                       marked "nothing else found depending on it" and deleted. Probe and delete now
 *                       refuse it and say how many other elements would go; a model group type's own
 *                       attached detail group types are the one allowed exception.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.Purge;

using AJTools.Utils;
namespace AJTools.Services.Purge
{
    internal sealed class UnusedElementPurgeService
    {
        private readonly Document _doc;
        private readonly UnusedElementPurgeMode _mode;
        private readonly UnusedElementCollector _collector;

        public UnusedElementPurgeService(Document doc, UnusedElementPurgeMode mode)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _mode = mode;
            _collector = new UnusedElementCollector(_doc, mode);
        }

        /// <summary>
        /// Collects the candidates and works out which of them Revit will actually let go.
        /// </summary>
        /// <param name="onProgress">
        /// Optional (done, total) callback, raised once per candidate. Purely for a progress bar - it
        /// cannot change what is scanned or what the scan concludes. Existing callers pass nothing and
        /// behave exactly as before. Worth having because every candidate costs a trial delete inside a
        /// rolled-back transaction, so a busy model can sit here for several seconds.
        /// </param>
        public IList<UnusedElementPurgeItem> Scan(Action<int, int> onProgress = null)
        {
            IList<UnusedElementPurgeItem> items = _collector.Collect();

            // Built once for the whole scan, not once per candidate - see CollectInUseIds.
            HashSet<long> inUseIds = items.Count > 0 && !_doc.IsReadOnly
                ? CollectInUseIds()
                : new HashSet<long>();

            int done = 0;
            int total = items.Count;

            foreach (UnusedElementPurgeItem item in items)
            {
                ProbeDelete(item, inUseIds);

                done++;
                if (onProgress != null)
                {
                    try { onProgress(done, total); }
                    catch { /* A reporting fault must never abort the scan. */ }
                }
            }

            return items
                .OrderBy(i => i.Status)
                .ThenBy(i => i.ElementKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ElementName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ElementIdValue)
                .ToList();
        }

        public UnusedElementPurgeResult DeleteSelected(
            IList<UnusedElementPurgeItem> selectedItems,
            int foundCount)
        {
            var result = new UnusedElementPurgeResult
            {
                FoundCount = foundCount
            };

            if (selectedItems == null || selectedItems.Count == 0)
            {
                return result;
            }

            if (_doc.IsReadOnly)
            {
                foreach (UnusedElementPurgeItem item in selectedItems)
                {
                    result.AttemptedCount++;
                    result.AddSkipped(GetItemName(item), GetItemIdValue(item), "Document is read-only.");
                }

                return result;
            }

            // Built once for the whole run - the same check the scan's probe made, applied before each commit.
            HashSet<long> inUseIds = CollectInUseIds();

            using (var group = new TransactionGroup(_doc, _mode.GetTransactionName()))
            {
                group.Start();
                bool hasCommit = false;

                foreach (UnusedElementPurgeItem item in selectedItems)
                {
                    result.AttemptedCount++;

                    string validationReason;
                    Element current = GetValidCurrentElement(item, true, out validationReason);
                    if (current == null)
                    {
                        result.AddSkipped(GetItemName(item), GetItemIdValue(item), validationReason);
                        continue;
                    }

                    using (var transaction = new Transaction(_doc, "Delete Unused Element"))
                    {
                        try
                        {
                            ElementId idToDelete = current.Id;
                            ISet<long> ownedIds = GetOwnedCompanionIds(current);
                            transaction.Start();
                            ICollection<ElementId> deletedIds = _doc.Delete(idToDelete);
                            if (!ContainsId(deletedIds, idToDelete))
                            {
                                RollBackIfStarted(transaction);
                                result.AddFailure(
                                    GetItemName(item),
                                    GetItemIdValue(item),
                                    "Revit did not report the selected element as deleted.");
                                continue;
                            }

                            int alsoDeleted = CountOtherInUse(deletedIds, idToDelete, inUseIds, ownedIds);
                            if (alsoDeleted > 0)
                            {
                                RollBackIfStarted(transaction);
                                result.AddSkipped(
                                    GetItemName(item),
                                    GetItemIdValue(item),
                                    DescribeOtherElementsDeleted(alsoDeleted));
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

        private void ProbeDelete(UnusedElementPurgeItem item, ISet<long> inUseIds)
        {
            if (item == null ||
                item.Status == UnusedElementPurgeStatus.Skipped ||
                _doc.IsReadOnly)
            {
                return;
            }

            string validationReason;
            Element current = GetValidCurrentElement(item, false, out validationReason);
            if (current == null)
            {
                item.MarkCannotDelete(
                    validationReason,
                    "The element was found during collection but failed the current safety validation.");
                return;
            }

            using (var transaction = new Transaction(_doc, "Probe Unused Element Delete"))
            {
                try
                {
                    ElementId idToProbe = current.Id;
                    ISet<long> ownedIds = GetOwnedCompanionIds(current);
                    transaction.Start();
                    ICollection<ElementId> deletedIds = _doc.Delete(idToProbe);
                    transaction.RollBack();

                    int alsoDeleted = CountOtherInUse(deletedIds, idToProbe, inUseIds, ownedIds);

                    if (!ContainsId(deletedIds, idToProbe))
                    {
                        item.MarkCannotDelete(
                            "Delete probe did not include this element.",
                            "Revit did not confirm that this element would be deleted - something still depends on it " +
                            "even though this scan did not detect a direct reference.");
                    }
                    else if (alsoDeleted > 0)
                    {
                        item.MarkCannotDelete(
                            DescribeOtherElementsDeleted(alsoDeleted),
                            "Revit's delete check showed that removing this element also removes other elements the " +
                            "project uses, not just its own internal data. AJ Tools only purges an element that goes " +
                            "on its own, so this one is kept.");
                    }
                    else
                    {
                        item.MarkSafe(
                            "Passed Revit's delete probe with nothing else found depending on it.",
                            "AJ Tools verified that Revit allows this element to be deleted inside a rolled-back transaction.");
                    }
                }
                catch (Exception ex)
                {
                    RollBackIfStarted(transaction);
                    item.MarkCannotDelete(
                        ex.Message,
                        "Revit rejected the rolled-back delete probe for this element.");
                }
            }
        }

        private Element GetValidCurrentElement(
            UnusedElementPurgeItem item,
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
                    ? "Element is not marked safe to purge."
                    : item.StatusReason;
                return null;
            }

            var id = ElementIdHelper.FromInt(item.ElementIdValue);
            Element element = _doc.GetElement(id);
            if (element == null)
            {
                reason = "Element no longer exists.";
                return null;
            }

            return element;
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
        /// Ids of everything in the document a purge of THESE three kinds must never take along: every view
        /// (sheets, schedules, legends and templates included), viewport, schedule placement, filter, group
        /// instance, element type (group types included) and every non-type element in a model category.
        /// </summary>
        /// <remarks>
        /// Why: Document.Delete also deletes "any elements that are totally dependent upon" the target (the
        /// API's own wording) and reports them only in the id set it returns, so "the target is in that set"
        /// alone does not mean nothing else goes. The rule, per kind this service purges:
        /// <list type="bullet">
        /// <item>View Templates and Filters own nothing a user can see - nothing is drawn in a template, and a
        /// filter is only referenced from views, which lose the reference rather than being deleted. So any
        /// id from this set other than the target means real loss and the element is refused.</item>
        /// <item>Group types (Model / Detail) are only offered with zero placed instances, so they own no placed
        /// members either. The one thing Revit legitimately deletes with a MODEL group type is its own attached
        /// detail group types - defined under it (Revit will not even copy one without its model group type)
        /// and unplaceable without a placed model group. Those, and only those, are allowed through (see
        /// GetOwnedCompanionIds). Anything else from this set - another group type, a group instance (a placed
        /// attached detail group included), a view, a filter or a model element - refuses the type.</item>
        /// </list>
        /// Uncategorised and annotation-category elements are deliberately left out: none of these three kinds
        /// can own placed annotation, so such ids can only be the target's own internal data, and counting them
        /// would risk refusing every candidate over Revit's own bookkeeping.
        /// Not live-tested yet: if a whole kind ever comes back Cannot Delete with this reason, look at what
        /// Revit's deleted-id set really holds for one of them before loosening the rule.
        /// Built once per scan / delete run; a stale id (already deleted earlier in the run) can never be
        /// reported as deleted again, so it is harmless.
        /// </remarks>
        private HashSet<long> CollectInUseIds()
        {
            var ids = new HashSet<long>();

            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(View)).ToElementIds());
            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(Viewport)).ToElementIds());
            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(ScheduleSheetInstance)).ToElementIds());
            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(ParameterFilterElement)).ToElementIds());
            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(SelectionFilterElement)).ToElementIds());
            AddIds(ids, new FilteredElementCollector(_doc).OfClass(typeof(Group)).ToElementIds());
            AddIds(ids, new FilteredElementCollector(_doc).WhereElementIsElementType().ToElementIds());

            // Category filters throw for a category "not valid for filtering" (API docs), so each one is checked
            // on its own first - one odd category must not break the whole scan.
            var modelCategoryIds = new List<ElementId>();
            foreach (Category category in _doc.Settings.Categories)
            {
                if (category != null &&
                    category.CategoryType == CategoryType.Model &&
                    IsFilterableCategory(category.Id))
                {
                    modelCategoryIds.Add(category.Id);
                }
            }

            if (modelCategoryIds.Count > 0)
            {
                using (var modelCategoryFilter = new ElementMulticategoryFilter(modelCategoryIds))
                {
                    AddIds(ids, new FilteredElementCollector(_doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(modelCategoryFilter)
                        .ToElementIds());
                }
            }

            return ids;
        }

        /// <summary>
        /// Elements Revit deletes together with <paramref name="target"/> that belong to it and cannot exist
        /// without it - the only in-use ids a purge may take along. Today that is a model group type's own
        /// attached detail group types; every other kind has none. Read before the delete, while the target
        /// still exists.
        /// </summary>
        private static ISet<long> GetOwnedCompanionIds(Element target)
        {
            var ids = new HashSet<long>();
            var groupType = target as GroupType;
            if (groupType == null)
            {
                return ids;
            }

            try
            {
                AddIds(ids, groupType.GetAvailableAttachedDetailGroupTypeIds());
            }
            catch
            {
                // A detail group type has no attached detail groups. Any failure here only means no
                // exemption - the check stays stricter, never looser.
            }

            return ids;
        }

        private static bool IsFilterableCategory(ElementId categoryId)
        {
            try
            {
                using (new ElementCategoryFilter(categoryId))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void AddIds(ISet<long> target, IEnumerable<ElementId> ids)
        {
            if (ids == null)
            {
                return;
            }

            foreach (ElementId id in ids)
            {
                if (id != null && id != ElementId.InvalidElementId)
                {
                    target.Add(id.LongValue());
                }
            }
        }

        /// <summary>
        /// How many in-use elements OTHER than the target and its own companions are in a Document.Delete result.
        /// </summary>
        private static int CountOtherInUse(
            IEnumerable<ElementId> deletedIds,
            ElementId targetId,
            ISet<long> inUseIds,
            ISet<long> ownedIds)
        {
            if (deletedIds == null || inUseIds == null || inUseIds.Count == 0)
            {
                return 0;
            }

            long target = targetId != null ? targetId.LongValue() : ElementId.InvalidElementId.LongValue();
            return deletedIds.Count(id =>
            {
                if (id == null)
                {
                    return false;
                }

                long value = id.LongValue();
                return value != target &&
                       inUseIds.Contains(value) &&
                       (ownedIds == null || !ownedIds.Contains(value));
            });
        }

        private static string DescribeOtherElementsDeleted(int count)
        {
            return "Deleting it would also delete " + count +
                   " other element(s) the project uses (views, sheet placements, filters, groups, types or model elements).";
        }

        private static void RollBackIfStarted(Transaction transaction)
        {
            if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
            {
                transaction.RollBack();
            }
        }

        private static string GetItemName(UnusedElementPurgeItem item)
        {
            return item != null ? item.ElementName : "Unknown";
        }

        private static int GetItemIdValue(UnusedElementPurgeItem item)
        {
            return item != null ? item.ElementIdValue : int.MinValue;
        }
    }
}
