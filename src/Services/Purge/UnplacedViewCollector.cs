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
    internal sealed class UnplacedViewCollector
    {
        private const string ThreeDViewKind = "3D View";
        private const string SectionViewKind = "Section";

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
            var placedViewIds = GetPlacedViewIds();
            var items = new List<UnplacedViewPurgeItem>();

            if (_mode == UnplacedViewPurgeMode.ThreeDViews)
            {
                AddUnplaced3DViews(items, placedViewIds);
            }
            else
            {
                AddUnplacedSectionViews(items, placedViewIds);
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
