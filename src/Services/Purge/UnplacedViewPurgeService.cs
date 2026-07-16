// ==================================================
// Tool Name    : Purge Unplaced Views
// Purpose      : Convert Python shell purge workflow into AJ Tools C# Revit add-in.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-11
// Last Updated : 2026-05-11
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Converted from Interactive Python Shell script.
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

        public IList<UnplacedViewPurgeItem> Scan()
        {
            IList<UnplacedViewPurgeItem> items = _collector.Collect();
            foreach (UnplacedViewPurgeItem item in items)
            {
                ProbeDelete(item);
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

        private void ProbeDelete(UnplacedViewPurgeItem item)
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

                    if (ContainsId(deletedIds, viewIdToProbe))
                    {
                        item.MarkSafe(
                            "Unplaced, non-template view passed Revit delete probe.",
                            "AJ Tools verified that Revit allows this view to be deleted inside a rolled-back transaction.");
                    }
                    else
                    {
                        item.MarkCannotDelete(
                            "Delete probe did not include this view.",
                            "Revit did not confirm that this view would be deleted.");
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
