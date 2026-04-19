// Tool Name: Pin Elements Service
// Description: Collects target groups and applies pin/unpin state in bulk.
// Author: Ajmal P.S.
// Version: 1.2.0
// Last Updated: 2026-04-18
// Revit Version: 2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using AJTools.Models.PinTools;

namespace AJTools.Services.PinTools
{
    /// <summary>
    /// Fixed metadata for one target group row.
    /// </summary>
    internal sealed class PinTargetDefinition
    {
        public PinTargetDefinition(PinTargetGroup group, string name, string description, bool defaultSelected)
        {
            Group = group;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            DefaultSelected = defaultSelected;
        }

        public PinTargetGroup Group { get; }

        public string Name { get; }

        public string Description { get; }

        public bool DefaultSelected { get; }
    }

    /// <summary>
    /// Provides candidate collection and pin/unpin execution for Pin Elements.
    /// </summary>
    internal static class PinElementsService
    {
        private static readonly PropertyInfo _revisionScheduleFlagProperty =
            typeof(ScheduleSheetInstance).GetProperty("IsTitleblockRevisionSchedule");

        private static readonly IReadOnlyList<PinTargetDefinition> _sheetDefinitions =
            new List<PinTargetDefinition>
            {
                new PinTargetDefinition(
                    PinTargetGroup.TitleBlocks,
                    "Title Blocks",
                    "Title block instances on sheets (based on selected scope).",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.PlacedViews,
                    "Placed Views",
                    "Normal viewports on sheets (excludes legends and schedules).",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.Legends,
                    "Legends",
                    "Legend viewports on sheets.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.Schedules,
                    "Schedules",
                    "Schedule placements on sheets (revision schedules excluded).",
                    false)
            };

        private static readonly IReadOnlyList<PinTargetDefinition> _modelDefinitions =
            new List<PinTargetDefinition>
            {
                new PinTargetDefinition(
                    PinTargetGroup.DuctSystem,
                    "Duct System",
                    "Duct curves, fittings, accessories, flex ducts, and duct insulation/linings.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.PipeSystem,
                    "Pipe System",
                    "Pipe curves, fittings, accessories, flex pipes, and pipe insulation.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.CableTraySystem,
                    "Cable Tray System",
                    "Cable trays and cable tray fittings.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.AirTerminal,
                    "Air Terminal",
                    "Elements in Air Terminal category.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.Links,
                    "Links",
                    "Revit link instances.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.GenericModels,
                    "Generic Models",
                    "Elements in Generic Models category.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.MechanicalEquipment,
                    "Mechanical Equipment",
                    "Elements in Mechanical Equipment category.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.PlumbingFixtures,
                    "Plumbing Fixtures",
                    "Elements in Plumbing Fixtures category.",
                    false),
                new PinTargetDefinition(
                    PinTargetGroup.ElectricalEquipment,
                    "Electrical Equipment",
                    "Elements in Electrical Equipment category.",
                    false)
            };

        public static bool IsSheetContext(View activeView)
        {
            return activeView is ViewSheet;
        }

        /// <summary>
        /// Returns sheet-only groups.
        /// </summary>
        public static IReadOnlyList<PinTargetDefinition> GetSheetTargetDefinitions()
        {
            return _sheetDefinitions;
        }

        /// <summary>
        /// Returns model-only groups.
        /// </summary>
        public static IReadOnlyList<PinTargetDefinition> GetModelTargetDefinitions()
        {
            return _modelDefinitions;
        }

        /// <summary>
        /// Counts distinct candidate elements for one group.
        /// </summary>
        public static int CountCandidates(Document doc, View activeView, PinTargetGroup group, bool includeAllSheets = false)
        {
            return CollectCandidateIds(doc, activeView, new[] { group }, includeAllSheets).Count;
        }

        /// <summary>
        /// Counts distinct candidate elements across multiple groups.
        /// </summary>
        public static int CountCandidates(Document doc, View activeView, IEnumerable<PinTargetGroup> groups, bool includeAllSheets = false)
        {
            return CollectCandidateIds(doc, activeView, groups, includeAllSheets).Count;
        }

        /// <summary>
        /// Applies pin or unpin to distinct elements in selected groups.
        /// </summary>
        public static PinOperationSummary ApplyPinState(
            Document doc,
            View activeView,
            IEnumerable<PinTargetGroup> groups,
            bool pinState,
            bool includeAllSheets = false)
        {
            var summary = new PinOperationSummary();
            if (doc == null || groups == null)
                return summary;

            HashSet<int> candidateIds = CollectCandidateIds(doc, activeView, groups, includeAllSheets);
            summary.TargetedCount = candidateIds.Count;

            if (candidateIds.Count == 0)
                return summary;

            using (var transaction = new Transaction(doc, pinState ? "Pin Elements" : "Unpin Elements"))
            {
                transaction.Start();

                foreach (int idValue in candidateIds)
                {
                    Element element = doc.GetElement(new ElementId(idValue));
                    if (element == null)
                    {
                        summary.SkippedCount++;
                        continue;
                    }

                    if (!TrySetPinned(element, pinState, out bool changed))
                    {
                        summary.SkippedCount++;
                        continue;
                    }

                    if (changed)
                        summary.UpdatedCount++;
                    else
                        summary.UnchangedCount++;
                }

                transaction.Commit();
            }

            return summary;
        }

        private static HashSet<int> CollectCandidateIds(
            Document doc,
            View activeView,
            IEnumerable<PinTargetGroup> groups,
            bool includeAllSheets)
        {
            var uniqueIds = new HashSet<int>();
            if (doc == null || groups == null)
                return uniqueIds;

            bool sheetContext = IsSheetContext(activeView);
            var activeSheet = activeView as ViewSheet;

            foreach (PinTargetGroup group in groups.Distinct())
            {
                if (sheetContext)
                {
                    if (!IsSheetGroup(group))
                        continue;

                    AddSheetGroupCandidates(doc, activeSheet, group, includeAllSheets, uniqueIds);
                    continue;
                }

                if (!IsModelGroup(group))
                    continue;

                AddModelGroupCandidates(doc, group, uniqueIds);
            }

            return uniqueIds;
        }

        private static bool IsSheetGroup(PinTargetGroup group)
        {
            switch (group)
            {
                case PinTargetGroup.TitleBlocks:
                case PinTargetGroup.PlacedViews:
                case PinTargetGroup.Legends:
                case PinTargetGroup.Schedules:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsModelGroup(PinTargetGroup group)
        {
            switch (group)
            {
                case PinTargetGroup.DuctSystem:
                case PinTargetGroup.PipeSystem:
                case PinTargetGroup.CableTraySystem:
                case PinTargetGroup.AirTerminal:
                case PinTargetGroup.Links:
                case PinTargetGroup.GenericModels:
                case PinTargetGroup.MechanicalEquipment:
                case PinTargetGroup.PlumbingFixtures:
                case PinTargetGroup.ElectricalEquipment:
                    return true;
                default:
                    return false;
            }
        }

        private static void AddSheetGroupCandidates(
            Document doc,
            ViewSheet activeSheet,
            PinTargetGroup group,
            bool includeAllSheets,
            ISet<int> sink)
        {
            if (includeAllSheets)
            {
                switch (group)
                {
                    case PinTargetGroup.TitleBlocks:
                        AddAllSheetTitleBlocks(doc, sink);
                        break;

                    case PinTargetGroup.PlacedViews:
                        AddAllSheetViewports(doc, sink, legendsOnly: false);
                        break;

                    case PinTargetGroup.Legends:
                        AddAllSheetViewports(doc, sink, legendsOnly: true);
                        break;

                    case PinTargetGroup.Schedules:
                        AddAllSheetSchedules(doc, sink);
                        break;
                }

                return;
            }

            if (activeSheet == null)
                return;

            switch (group)
            {
                case PinTargetGroup.TitleBlocks:
                    AddSheetTitleBlocks(doc, activeSheet, sink);
                    break;

                case PinTargetGroup.PlacedViews:
                    AddSheetViewports(doc, activeSheet, sink, legendsOnly: false);
                    break;

                case PinTargetGroup.Legends:
                    AddSheetViewports(doc, activeSheet, sink, legendsOnly: true);
                    break;

                case PinTargetGroup.Schedules:
                    AddSheetSchedules(doc, activeSheet, sink);
                    break;
            }
        }

        private static void AddModelGroupCandidates(Document doc, PinTargetGroup group, ISet<int> sink)
        {
            switch (group)
            {
                case PinTargetGroup.DuctSystem:
                    AddElementsByBuiltInCategoryNames(
                        doc,
                        sink,
                        "OST_DuctCurves",
                        "OST_DuctFitting",
                        "OST_DuctAccessory",
                        "OST_FlexDuctCurves",
                        "OST_DuctInsulations",
                        "OST_DuctLinings");
                    break;

                case PinTargetGroup.PipeSystem:
                    AddElementsByBuiltInCategoryNames(
                        doc,
                        sink,
                        "OST_PipeCurves",
                        "OST_PipeFitting",
                        "OST_PipeAccessory",
                        "OST_FlexPipeCurves",
                        "OST_PipeInsulations");
                    break;

                case PinTargetGroup.CableTraySystem:
                    AddElementsByBuiltInCategoryNames(
                        doc,
                        sink,
                        "OST_CableTray",
                        "OST_CableTrayFitting");
                    break;

                case PinTargetGroup.AirTerminal:
                    AddElementsByBuiltInCategoryNames(doc, sink, "OST_DuctTerminal");
                    break;

                case PinTargetGroup.Links:
                    AddElementsByBuiltInCategoryNames(doc, sink, "OST_RvtLinks");
                    break;

                case PinTargetGroup.GenericModels:
                    AddElementsByBuiltInCategoryNames(doc, sink, "OST_GenericModel");
                    break;

                case PinTargetGroup.MechanicalEquipment:
                    AddElementsByBuiltInCategoryNames(doc, sink, "OST_MechanicalEquipment");
                    break;

                case PinTargetGroup.PlumbingFixtures:
                    AddElementsByBuiltInCategoryNames(doc, sink, "OST_PlumbingFixtures");
                    break;

                case PinTargetGroup.ElectricalEquipment:
                    AddElementsByBuiltInCategoryNames(doc, sink, "OST_ElectricalEquipment");
                    break;
            }
        }

        private static void AddSheetTitleBlocks(Document doc, ViewSheet sheet, ISet<int> sink)
        {
            if (doc == null || sheet == null || sink == null)
                return;

            try
            {
                ICollection<ElementId> ids = new FilteredElementCollector(doc)
                    .OwnedByView(sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                AddElementIds(ids, sink);
            }
            catch
            {
                // Ignore unsupported sheet title block collection.
            }
        }

        private static void AddAllSheetTitleBlocks(Document doc, ISet<int> sink)
        {
            if (doc == null || sink == null)
                return;

            try
            {
                ICollection<ElementId> ids = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                AddElementIds(ids, sink);
            }
            catch
            {
                // Ignore unsupported title block collection.
            }
        }

        private static void AddSheetViewports(Document doc, ViewSheet sheet, ISet<int> sink, bool legendsOnly)
        {
            if (doc == null || sheet == null || sink == null)
                return;

            ICollection<ElementId> viewportIds;
            try
            {
                viewportIds = sheet.GetAllViewports();
            }
            catch
            {
                return;
            }

            foreach (ElementId viewportId in viewportIds)
            {
                if (viewportId == null || viewportId == ElementId.InvalidElementId)
                    continue;

                var viewport = doc.GetElement(viewportId) as Viewport;
                if (viewport == null)
                    continue;

                AddViewportIfMatching(doc, viewport, legendsOnly, sink);
            }
        }

        private static void AddAllSheetViewports(Document doc, ISet<int> sink, bool legendsOnly)
        {
            if (doc == null || sink == null)
                return;

            try
            {
                IEnumerable<Viewport> viewports = new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .WhereElementIsNotElementType()
                    .Cast<Viewport>();

                foreach (Viewport viewport in viewports)
                {
                    if (viewport == null)
                        continue;

                    AddViewportIfMatching(doc, viewport, legendsOnly, sink);
                }
            }
            catch
            {
                // Ignore unsupported viewport collection.
            }
        }

        private static void AddViewportIfMatching(Document doc, Viewport viewport, bool legendsOnly, ISet<int> sink)
        {
            if (doc == null || viewport == null || sink == null)
                return;

            var referencedView = doc.GetElement(viewport.ViewId) as View;
            if (referencedView == null)
                return;

            bool isLegend = referencedView.ViewType == ViewType.Legend;
            if (legendsOnly)
            {
                if (!isLegend)
                    return;
            }
            else
            {
                if (isLegend || referencedView.ViewType == ViewType.Schedule)
                    return;
            }

            ElementId id = viewport.Id;
            if (id != null && id != ElementId.InvalidElementId)
                sink.Add(id.IntegerValue);
        }

        private static void AddSheetSchedules(Document doc, ViewSheet sheet, ISet<int> sink)
        {
            if (doc == null || sheet == null || sink == null)
                return;

            try
            {
                IEnumerable<ScheduleSheetInstance> schedules = new FilteredElementCollector(doc)
                    .OwnedByView(sheet.Id)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .WhereElementIsNotElementType()
                    .Cast<ScheduleSheetInstance>();

                AddScheduleInstances(schedules, sink);
            }
            catch
            {
                // Ignore unsupported schedule collection.
            }
        }

        private static void AddAllSheetSchedules(Document doc, ISet<int> sink)
        {
            if (doc == null || sink == null)
                return;

            try
            {
                IEnumerable<ScheduleSheetInstance> schedules = new FilteredElementCollector(doc)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .WhereElementIsNotElementType()
                    .Cast<ScheduleSheetInstance>()
                    .Where(schedule =>
                    {
                        if (schedule == null)
                            return false;

                        ElementId ownerViewId = schedule.OwnerViewId;
                        if (ownerViewId == null || ownerViewId == ElementId.InvalidElementId)
                            return false;

                        return doc.GetElement(ownerViewId) is ViewSheet;
                    });

                AddScheduleInstances(schedules, sink);
            }
            catch
            {
                // Ignore unsupported schedule collection.
            }
        }

        private static void AddScheduleInstances(IEnumerable<ScheduleSheetInstance> schedules, ISet<int> sink)
        {
            if (schedules == null || sink == null)
                return;

            foreach (ScheduleSheetInstance scheduleInstance in schedules)
            {
                if (scheduleInstance == null || IsRevisionSchedule(scheduleInstance))
                    continue;

                ElementId id = scheduleInstance.Id;
                if (id != null && id != ElementId.InvalidElementId)
                    sink.Add(id.IntegerValue);
            }
        }

        private static bool IsRevisionSchedule(ScheduleSheetInstance scheduleInstance)
        {
            if (scheduleInstance == null || _revisionScheduleFlagProperty == null)
                return false;

            try
            {
                object value = _revisionScheduleFlagProperty.GetValue(scheduleInstance, null);
                if (value is bool flag)
                    return flag;
            }
            catch
            {
                // Ignore API access issues and treat as normal schedule.
            }

            return false;
        }

        private static void AddElementsByBuiltInCategoryNames(Document doc, ISet<int> sink, params string[] categoryNames)
        {
            if (doc == null || sink == null || categoryNames == null || categoryNames.Length == 0)
                return;

            IEnumerable<string> names = categoryNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string name in names)
            {
                if (!Enum.TryParse(name, out BuiltInCategory category))
                    continue;

                try
                {
                    ICollection<ElementId> ids = new FilteredElementCollector(doc)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .ToElementIds();

                    AddElementIds(ids, sink);
                }
                catch
                {
                    // Ignore unsupported categories for this Revit version/document.
                }
            }
        }

        private static void AddElementIds(ICollection<ElementId> ids, ISet<int> sink)
        {
            if (ids == null || sink == null)
                return;

            foreach (ElementId id in ids)
            {
                if (id != null && id != ElementId.InvalidElementId)
                    sink.Add(id.IntegerValue);
            }
        }

        private static bool TrySetPinned(Element element, bool pinState, out bool changed)
        {
            changed = false;
            if (element == null)
                return false;

            bool current;
            try
            {
                current = element.Pinned;
            }
            catch
            {
                return false;
            }

            if (current == pinState)
                return true;

            try
            {
                element.Pinned = pinState;
                changed = true;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
