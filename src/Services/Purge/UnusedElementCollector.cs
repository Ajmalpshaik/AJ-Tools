#region Metadata
/*
 * Tool Name     : Purge Unused Elements (shared collector)
 * File Name     : UnusedElementCollector.cs
 * Purpose       : Scans a document for View Templates / Filters / Group types that are declared but not
 *                 actually referenced anywhere, per UnusedElementPurgeMode.
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
 * Dependencies  : Autodesk Revit API, AJTools.Models.Purge, AJTools.Utils (ElementIdExtensions)
 *
 * Input         : Document, UnusedElementPurgeMode.
 * Output        : Ordered list of UnusedElementPurgeItem candidates (not yet delete-probed).
 *
 * Notes         :
 * - ViewTemplates: a template is "used" if any non-template View's ViewTemplateId points at it. A
 *   template set as Revit's own default-for-new-views is not visible through the public API the same
 *   way, so it could still show here even though Revit relies on it - the probe-delete step in
 *   UnusedElementPurgeService is the real safety net for that case, not this static scan (see Modeler
 *   Mindset in CLAUDE.md: don't trust a single API signal as the full picture).
 * - Filters: a filter is "used" if it appears in View.GetFilters() on ANY view OR view template (both
 *   carry V/G override filter lists independently).
 * - Groups: Model Group and Detail Group types (OST_IOSModelGroups / OST_IOSDetailGroups) whose
 *   GroupType.Groups instance set is empty. Attached detail groups (auto-owned by a parent model group)
 *   are intentionally out of scope - they aren't independently meaningful the same way.
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
    internal sealed class UnusedElementCollector
    {
        private readonly Document _doc;
        private readonly UnusedElementPurgeMode _mode;

        public UnusedElementCollector(Document doc, UnusedElementPurgeMode mode)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _mode = mode;
        }

        public IList<UnusedElementPurgeItem> Collect()
        {
            var items = new List<UnusedElementPurgeItem>();

            switch (_mode)
            {
                case UnusedElementPurgeMode.ViewTemplates:
                    AddUnusedViewTemplates(items);
                    break;
                case UnusedElementPurgeMode.Filters:
                    AddUnusedFilters(items);
                    break;
                default:
                    AddUnusedGroups(items);
                    break;
            }

            return items
                .OrderBy(i => i.ElementKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ElementName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ElementIdValue)
                .ToList();
        }

        private void AddUnusedViewTemplates(ICollection<UnusedElementPurgeItem> items)
        {
            List<View> allViews = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null)
                .ToList();

            var usedTemplateIds = new HashSet<int>();
            foreach (View view in allViews.Where(v => !v.IsTemplate))
            {
                ElementId templateId = SafeGetViewTemplateId(view);
                if (templateId != null && templateId != ElementId.InvalidElementId)
                {
                    usedTemplateIds.Add(templateId.IntValue());
                }
            }

            foreach (View template in allViews.Where(v => v.IsTemplate))
            {
                if (usedTemplateIds.Contains(template.Id.IntValue()))
                {
                    continue;
                }

                items.Add(CreateItem(template.Id, SafeGetName(template), "View Template"));
            }
        }

        private void AddUnusedFilters(ICollection<UnusedElementPurgeItem> items)
        {
            List<ParameterFilterElement> allFilters = new FilteredElementCollector(_doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .Where(f => f != null)
                .ToList();

            var usedFilterIds = new HashSet<int>();
            IEnumerable<View> allViews = new FilteredElementCollector(_doc).OfClass(typeof(View)).Cast<View>();
            foreach (View view in allViews)
            {
                foreach (ElementId filterId in SafeGetFilters(view))
                {
                    if (filterId != null && filterId != ElementId.InvalidElementId)
                    {
                        usedFilterIds.Add(filterId.IntValue());
                    }
                }
            }

            foreach (ParameterFilterElement filter in allFilters)
            {
                if (usedFilterIds.Contains(filter.Id.IntValue()))
                {
                    continue;
                }

                items.Add(CreateItem(filter.Id, SafeGetName(filter), "Filter"));
            }
        }

        private void AddUnusedGroups(ICollection<UnusedElementPurgeItem> items)
        {
            AddUnusedGroupsOfCategory(items, BuiltInCategory.OST_IOSModelGroups, "Model Group");
            AddUnusedGroupsOfCategory(items, BuiltInCategory.OST_IOSDetailGroups, "Detail Group");
        }

        private void AddUnusedGroupsOfCategory(
            ICollection<UnusedElementPurgeItem> items,
            BuiltInCategory category,
            string kindLabel)
        {
            IEnumerable<GroupType> groupTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(GroupType))
                .Cast<GroupType>()
                .Where(gt => gt != null && gt.Category != null && gt.Category.Id.IntValue() == (int)category);

            foreach (GroupType groupType in groupTypes)
            {
                GroupSet instances = SafeGetGroups(groupType);
                if (instances != null && !instances.IsEmpty)
                {
                    continue;
                }

                items.Add(CreateItem(groupType.Id, SafeGetName(groupType), kindLabel));
            }
        }

        private UnusedElementPurgeItem CreateItem(ElementId id, string name, string kindLabel)
        {
            var item = new UnusedElementPurgeItem
            {
                ElementIdValue = id.IntValue(),
                ElementName = name,
                ElementKind = kindLabel,
                Status = UnusedElementPurgeStatus.CannotDelete,
                StatusReason = "Pending deletion safety check.",
                DetailedNotes = "Collected as a candidate with no detected usage in this document."
            };

            if (_doc.IsReadOnly)
            {
                item.MarkCannotDelete(
                    "Document is read-only.",
                    "The element was found, but Revit will not allow deletion in a read-only document.");
            }

            return item;
        }

        private static ElementId SafeGetViewTemplateId(View view)
        {
            try
            {
                return view.ViewTemplateId;
            }
            catch
            {
                return ElementId.InvalidElementId;
            }
        }

        private static ICollection<ElementId> SafeGetFilters(View view)
        {
            try
            {
                return view.GetFilters() ?? Array.Empty<ElementId>();
            }
            catch
            {
                return Array.Empty<ElementId>();
            }
        }

        private static GroupSet SafeGetGroups(GroupType groupType)
        {
            try
            {
                return groupType.Groups;
            }
            catch
            {
                return null;
            }
        }

        private static string SafeGetName(Element element)
        {
            try
            {
                return element != null && !string.IsNullOrWhiteSpace(element.Name)
                    ? element.Name
                    : "<unnamed element>";
            }
            catch
            {
                return "<unnamed element>";
            }
        }
    }
}
