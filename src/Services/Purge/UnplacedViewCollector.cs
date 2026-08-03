// ==================================================
// Tool Name    : Purge Unplaced Views
// Purpose      : Convert Python shell purge workflow into AJ Tools C# Revit add-in.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-05-11
// Last Updated : 2026-07-28
// Target       : Revit 2020 - latest
// Framework    : .NET Framework 4.7.2 (2020) / .NET 8 (2025-2026) / .NET 10 (2027+)
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Converted from Interactive Python Shell script.
//                v1.1.0 (2026-07-28) - Added Schedules/Legends/DraftingViews modes. Schedules are placed
//                via ScheduleSheetInstance (a different mechanism to Viewport), so they get their own
//                placed-id set (GetPlacedScheduleIds) and candidate check (IsUnplacedScheduleCandidate);
//                Legends/DraftingViews reuse the existing Viewport-based GetPlacedViewIds/
//                IsUnplacedViewCandidate unchanged. ThreeDViews/SectionViews behaviour unchanged.
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
    internal sealed class UnplacedViewCollector
    {
        private const string ThreeDViewKind = "3D View";
        private const string SectionViewKind = "Section";
        private const string ScheduleKind = "Schedule";
        private const string LegendKind = "Legend";
        private const string DraftingViewKind = "Drafting View";

        private readonly Document _doc;
        private readonly ElementId _activeViewId;
        private readonly UnplacedViewPurgeMode _mode;

        public UnplacedViewCollector(
            Document doc,
            ElementId activeViewId,
            UnplacedViewPurgeMode mode)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _activeViewId = activeViewId ?? ElementId.InvalidElementId;
            _mode = mode;
        }

        public IList<UnplacedViewPurgeItem> Collect()
        {
            var items = new List<UnplacedViewPurgeItem>();

            if (_mode == UnplacedViewPurgeMode.Schedules)
            {
                AddUnplacedSchedules(items, GetPlacedScheduleIds());
            }
            else
            {
                var placedViewIds = GetPlacedViewIds();
                switch (_mode)
                {
                    case UnplacedViewPurgeMode.ThreeDViews:
                        AddUnplaced3DViews(items, placedViewIds);
                        break;
                    case UnplacedViewPurgeMode.SectionViews:
                        AddUnplacedSectionViews(items, placedViewIds);
                        break;
                    case UnplacedViewPurgeMode.Legends:
                        AddUnplacedLegends(items, placedViewIds);
                        break;
                    default:
                        AddUnplacedDraftingViews(items, placedViewIds);
                        break;
                }
            }

            return items
                .OrderBy(i => i.ViewKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ViewName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ViewIdValue)
                .ToList();
        }

        public bool IsViewPlaced(ElementId viewId)
        {
            if (viewId == null || viewId == ElementId.InvalidElementId)
            {
                return false;
            }

            if (_mode == UnplacedViewPurgeMode.Schedules)
            {
                return GetPlacedScheduleIds().Contains(viewId.IntValue());
            }

            return GetPlacedViewIds().Contains(viewId.IntValue());
        }

        private void AddUnplaced3DViews(ICollection<UnplacedViewPurgeItem> items, ISet<int> placedViewIds)
        {
            IEnumerable<View3D> views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>();

            foreach (View3D view in views)
            {
                if (!IsUnplacedViewCandidate(view, ViewType.ThreeD, placedViewIds))
                {
                    continue;
                }

                items.Add(CreateItem(view, ThreeDViewKind));
            }
        }

        private void AddUnplacedSectionViews(ICollection<UnplacedViewPurgeItem> items, ISet<int> placedViewIds)
        {
            IEnumerable<ViewSection> views = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>();

            foreach (ViewSection view in views)
            {
                if (!IsUnplacedViewCandidate(view, ViewType.Section, placedViewIds))
                {
                    continue;
                }

                items.Add(CreateItem(view, SectionViewKind));
            }
        }

        private void AddUnplacedLegends(ICollection<UnplacedViewPurgeItem> items, ISet<int> placedViewIds)
        {
            IEnumerable<View> views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>();

            foreach (View view in views)
            {
                if (!IsUnplacedViewCandidate(view, ViewType.Legend, placedViewIds))
                {
                    continue;
                }

                items.Add(CreateItem(view, LegendKind));
            }
        }

        private void AddUnplacedDraftingViews(ICollection<UnplacedViewPurgeItem> items, ISet<int> placedViewIds)
        {
            IEnumerable<View> views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>();

            foreach (View view in views)
            {
                if (!IsUnplacedViewCandidate(view, ViewType.DraftingView, placedViewIds))
                {
                    continue;
                }

                items.Add(CreateItem(view, DraftingViewKind));
            }
        }

        private void AddUnplacedSchedules(ICollection<UnplacedViewPurgeItem> items, ISet<int> placedScheduleIds)
        {
            IEnumerable<ViewSchedule> schedules = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>();

            foreach (ViewSchedule schedule in schedules)
            {
                if (!IsUnplacedScheduleCandidate(schedule, placedScheduleIds))
                {
                    continue;
                }

                items.Add(CreateItem(schedule, ScheduleKind));
            }
        }

        private bool IsUnplacedViewCandidate(View view, ViewType expectedViewType, ISet<int> placedViewIds)
        {
            if (view == null)
            {
                return false;
            }

            if (view.IsTemplate || view.ViewType != expectedViewType)
            {
                return false;
            }

            return view.Id != null &&
                   view.Id != ElementId.InvalidElementId &&
                   !placedViewIds.Contains(view.Id.IntValue());
        }

        private static bool IsUnplacedScheduleCandidate(ViewSchedule schedule, ISet<int> placedScheduleIds)
        {
            if (schedule == null || schedule.IsTemplate)
            {
                return false;
            }

            if (schedule.IsTitleblockRevisionSchedule || schedule.IsInternalKeynoteSchedule)
            {
                return false;
            }

            return schedule.Id != null &&
                   schedule.Id != ElementId.InvalidElementId &&
                   !placedScheduleIds.Contains(schedule.Id.IntValue());
        }

        private UnplacedViewPurgeItem CreateItem(View view, string viewKind)
        {
            var item = new UnplacedViewPurgeItem
            {
                ViewIdValue = view.Id.IntValue(),
                ViewName = SafeGetName(view),
                ViewKind = viewKind,
                ViewTypeText = view.ViewType.ToString(),
                IsActiveView = IsSameId(view.Id, _activeViewId),
                IsDefault3DView = IsDefault3DView(view),
                Status = UnplacedViewPurgeStatus.CannotDelete,
                StatusReason = "Pending deletion safety check.",
                DetailedNotes = "Collected as a non-template unplaced view."
            };

            if (_doc.IsReadOnly)
            {
                item.MarkCannotDelete(
                    "Document is read-only.",
                    "The view was found, but Revit will not allow deletion in a read-only document.");
            }
            else if (item.IsActiveView)
            {
                item.MarkSkipped(
                    "Active view is never deleted.",
                    "Open another view and rescan if this view should be considered for purge.");
            }
            else if (item.IsDefault3DView)
            {
                item.MarkSkipped(
                    "Default {3D} view is skipped.",
                    "AJ Tools keeps Revit's default 3D view because many projects use it as a coordination fallback.");
            }

            return item;
        }

        private HashSet<int> GetPlacedViewIds()
        {
            var placedViewIds = new HashSet<int>();
            IEnumerable<Viewport> viewports = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>();

            foreach (Viewport viewport in viewports)
            {
                if (viewport == null ||
                    viewport.ViewId == null ||
                    viewport.ViewId == ElementId.InvalidElementId)
                {
                    continue;
                }

                placedViewIds.Add(viewport.ViewId.IntValue());
            }

            return placedViewIds;
        }

        private HashSet<int> GetPlacedScheduleIds()
        {
            var placedScheduleIds = new HashSet<int>();
            IEnumerable<ScheduleSheetInstance> placements = new FilteredElementCollector(_doc)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>();

            foreach (ScheduleSheetInstance placement in placements)
            {
                if (placement == null ||
                    placement.ScheduleId == null ||
                    placement.ScheduleId == ElementId.InvalidElementId)
                {
                    continue;
                }

                placedScheduleIds.Add(placement.ScheduleId.IntValue());
            }

            return placedScheduleIds;
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
