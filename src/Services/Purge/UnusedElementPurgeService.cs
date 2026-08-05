#region Metadata
/*
 * Tool Name     : Purge Unused Elements (shared service)
 * File Name     : UnusedElementPurgeService.cs
 * Purpose       : Scans for unused View Templates/Filters/Groups, probes each with a rolled-back
 *                 Document.Delete to confirm Revit really allows it, and deletes the selected safe ones.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-28
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
 * - The rolled-back Document.Delete probe is the real safety net (Revit's own dependency graph decides,
 *   not this tool's static "is it referenced" scan) - the same pattern already proven in
 *   UnplacedViewPurgeService. A view template that turns out to be Revit's silent default for a view
 *   type, or a filter/group referenced somewhere this scan didn't check, still gets caught here as
 *   Cannot Delete before anything is actually removed.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
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

            int done = 0;
            int total = items.Count;

            foreach (UnusedElementPurgeItem item in items)
            {
                ProbeDelete(item);

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

        private void ProbeDelete(UnusedElementPurgeItem item)
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
                    transaction.Start();
                    ICollection<ElementId> deletedIds = _doc.Delete(idToProbe);
                    transaction.RollBack();

                    if (ContainsId(deletedIds, idToProbe))
                    {
                        item.MarkSafe(
                            "Passed Revit's delete probe with nothing else found depending on it.",
                            "AJ Tools verified that Revit allows this element to be deleted inside a rolled-back transaction.");
                    }
                    else
                    {
                        item.MarkCannotDelete(
                            "Delete probe did not include this element.",
                            "Revit did not confirm that this element would be deleted - something still depends on it " +
                            "even though this scan did not detect a direct reference.");
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
