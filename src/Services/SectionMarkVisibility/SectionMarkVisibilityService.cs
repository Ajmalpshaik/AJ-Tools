#region Metadata
/*
 * Tool Name     : Section Mark Visibility
 * File Name     : SectionMarkVisibilityService.cs
 * Purpose       : Core logic — unhides/filters section markers in plan views by
 *                 evaluating each Section View's native 'Sheet Number' parameter.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-05-24
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Tool Scope: Active View / selected plan views (+ dependent views); user settings
 * Output        : Section markers hidden/unhidden in target views; processing result counts
 *
 * Notes         :
 * - All section markers are collected and classified ONCE before the view loop (no full-model
 *   scan inside the loop).
 * - Workshared models: views owned by another user are skipped with a clear reason.
 * - All changes run inside ONE transaction (single undo step).
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-24) - Initial release.
 * v1.1.0 (2026-05-24) - Direct Sheet Number parameter evaluation.
 * v1.2.0 (2026-06-30) - Cleanup pass: section markers collected/classified once before the
 *                       view loop (performance); workshared view editability check with clear
 *                       skip reason; removed never-matching Id fallback; metadata block.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.SectionMarkVisibility;

using AJTools.Utils;
namespace AJTools.Services.SectionMarkVisibility
{
    /// <summary>
    /// Executes the unhide and filtering operations on target plan views by evaluating the native 
    /// 'Sheet Number' parameter of each Section View directly.
    /// </summary>
    internal sealed class SectionMarkVisibilityService
    {
        private readonly Document _doc;
        private readonly SectionMarkVisibilitySettings _settings;

        // Caches whether a given ViewFamilyType (by Id) is a Section type, to avoid repeated lookups.
        private readonly Dictionary<int, bool> _sectionTypeCache = new Dictionary<int, bool>();

        public SectionMarkVisibilityService(Document doc, SectionMarkVisibilitySettings settings)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Processes a batch of plan views to filter their visible sections, returning counts and a diagnostics log.
        /// </summary>
        public SectionMarkVisibilityResult Process(IList<View> targetViews, string transactionName)
        {
            var errors = new List<string>();

            if (targetViews == null || targetViews.Count == 0)
            {
                return new SectionMarkVisibilityResult(0, 0, new List<string> { "No target views provided." }, "ERROR: No target views provided.");
            }

            int processedCount = 0;
            int skippedCount = 0;

            // 1. Gather all section views in the document (exclude templates and other view types)
            var allSections = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => v != null && !v.IsTemplate && v.ViewType == ViewType.Section)
                .ToList();

            if (allSections.Count == 0)
            {
                // Not an error — there is simply nothing to process. Reported as an info message.
                return new SectionMarkVisibilityResult(0, 0, new List<string>(), "No section marks were found in this project.");
            }

            // 2. Build a lookup: SectionName -> ViewSection (for matching markers to sections)
            var sectionByName = new Dictionary<string, ViewSection>(StringComparer.OrdinalIgnoreCase);
            foreach (ViewSection sec in allSections)
            {
                string name = sec.Name;
                if (!string.IsNullOrEmpty(name) && !sectionByName.ContainsKey(name))
                {
                    sectionByName[name] = sec;
                }
            }

            // 3. Collect and classify ALL section markers ONCE (avoids a full-model scan inside the loop)
            var sectionMarkers = CollectSectionMarkers();

            // 4. Resolve the full list of views to process, including dependent plan views
            var viewsToProcess = ResolveViewsAndDependents(targetViews);

            // 5. Run visibility operations inside a Revit Transaction
            using (var trans = new Transaction(_doc, transactionName))
            {
                trans.Start();

                foreach (View view in viewsToProcess)
                {
                    try
                    {
                        if (!IsSupportedPlanView(view))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Worksharing: skip views owned by another user (cannot be modified)
                        if (!IsViewEditable(view, out string ownerReason))
                        {
                            skippedCount++;
                            errors.Add($"Skipped view '{view.Name}': {ownerReason}");
                            continue;
                        }

                        // A. Ensure the OST_Sections category is visible in the view
                        EnsureSectionsCategoryVisible(view);

                        // B. Unhide ALL section markers in the target view first (reset state)
                        UnhideAllSectionsInView(view, sectionMarkers);

                        // CRITICAL: We must regenerate the document here.
                        // Otherwise, the view-specific FilteredElementCollector in HideNonMatchingSectionsInView
                        // will not 'see' the newly unhidden section markers in the same transaction!
                        _doc.Regenerate();

                        // C. If UnhideAllSections mode, stop here (no filtering needed)
                        //    Otherwise, hide sections that do not match the sheet criteria
                        if (!_settings.UnhideAllSections)
                        {
                            HideNonMatchingSectionsInView(view, sectionByName);
                        }

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error processing view '{view.Name}': {ex.Message}");
                    }
                }

                _doc.Regenerate();
                trans.Commit();
            }

            string summary = $"Processed {processedCount} view(s).";
            if (skippedCount > 0)
                summary += $" Skipped {skippedCount} unsupported view(s).";
            if (errors.Count > 0)
                summary += $" {errors.Count} error(s) encountered.";

            return new SectionMarkVisibilityResult(processedCount, skippedCount, errors, summary);
        }

        /// <summary>
        /// Reads the native 'Sheet Number' parameter of a view to determine if it is placed on a sheet.
        /// </summary>
        private static bool IsSectionPlacedOnSheet(View view, out string sheetNumber)
        {
            sheetNumber = string.Empty;
            if (view == null) return false;

            Parameter sheetParam = view.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER);
            if (sheetParam != null && sheetParam.HasValue)
            {
                string val = sheetParam.AsString();
                if (!string.IsNullOrWhiteSpace(val) && val != "---" && val != "-")
                {
                    sheetNumber = val.Trim();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Includes dependent views for each target view if applicable.
        /// </summary>
        private List<View> ResolveViewsAndDependents(IList<View> primaryViews)
        {
            var resolved = new List<View>();
            var addedIds = new HashSet<int>();

            foreach (View view in primaryViews)
            {
                if (view == null || addedIds.Contains(view.Id.IntValue()))
                    continue;

                resolved.Add(view);
                addedIds.Add(view.Id.IntValue());

                try
                {
                    ICollection<ElementId> dependentIds = view.GetDependentViewIds();
                    if (dependentIds != null && dependentIds.Count > 0)
                    {
                        foreach (ElementId depId in dependentIds)
                        {
                            var depView = _doc.GetElement(depId) as View;
                            if (depView != null && !addedIds.Contains(depView.Id.IntValue()))
                            {
                                resolved.Add(depView);
                                addedIds.Add(depView.Id.IntValue());
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore failures retrieving dependent views
                }
            }

            return resolved;
        }

        /// <summary>
        /// Ensures the OST_Sections category is visible in the view.
        /// </summary>
        private void EnsureSectionsCategoryVisible(View view)
        {
            try
            {
                Category sectionCat = Category.GetCategory(_doc, BuiltInCategory.OST_Sections);
                if (sectionCat != null)
                {
                    if (view.GetCategoryHidden(sectionCat.Id))
                    {
                        view.SetCategoryHidden(sectionCat.Id, false);
                    }
                }
            }
            catch
            {
                // Category visibility might be locked by a View Template
            }
        }

        /// <summary>
        /// Collects every section-type marker (OST_Viewers) in the document, ONCE.
        /// FilteredElementCollector(doc, viewId) only returns VISIBLE elements, so to be able to
        /// unhide HIDDEN markers later we gather them all here and classify by ViewFamily.Section.
        /// This runs a single full-document collection, outside the per-view loop.
        /// </summary>
        private List<Element> CollectSectionMarkers()
        {
            var markers = new List<Element>();

            try
            {
                var allViewers = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Viewers)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (Element viewer in allViewers)
                {
                    if (viewer != null && IsSectionTypeViewer(viewer))
                        markers.Add(viewer);
                }
            }
            catch
            {
                // Ignore collector errors — return whatever was gathered.
            }

            return markers;
        }

        /// <summary>
        /// Returns false if the view is owned by another user in a workshared model (cannot be edited).
        /// </summary>
        private bool IsViewEditable(View view, out string reason)
        {
            reason = string.Empty;
            try
            {
                if (!_doc.IsWorkshared)
                    return true;

                CheckoutStatus status = WorksharingUtils.GetCheckoutStatus(_doc, view.Id);
                if (status == CheckoutStatus.OwnedByOtherUser)
                {
                    reason = "view is owned by another user.";
                    return false;
                }
            }
            catch
            {
                // If status cannot be determined, allow the attempt — any failure is caught per-view.
            }
            return true;
        }

        /// <summary>
        /// Unhides ALL section markers that are currently hidden in this view.
        /// This resets the view state so that fresh filtering can be applied.
        /// Uses the pre-collected marker set, so no document scan happens inside the loop.
        /// </summary>
        private void UnhideAllSectionsInView(View view, IList<Element> sectionMarkers)
        {
            var elementsToUnhide = new List<ElementId>();

            try
            {
                foreach (Element viewer in sectionMarkers)
                {
                    if (viewer == null || !viewer.IsValidObject) continue;

                    // Check if it is currently hidden in this view
                    try
                    {
                        if (viewer.IsHidden(view))
                        {
                            elementsToUnhide.Add(viewer.Id);
                        }
                    }
                    catch
                    {
                        // Skip elements that throw during visibility check
                    }
                }

                if (elementsToUnhide.Count > 0)
                {
                    try
                    {
                        view.UnhideElements(elementsToUnhide);
                    }
                    catch
                    {
                        // Fallback: unhide one by one
                        foreach (ElementId elId in elementsToUnhide)
                        {
                            try { view.UnhideElements(new[] { elId }); } catch { }
                        }
                    }
                }
            }
            catch
            {
                // Ignore collector errors
            }
        }

        /// <summary>
        /// Hides section markers in the view that do not match the user's sheet filter criteria.
        /// 
        /// Strategy: Collect VISIBLE OST_Viewers from this specific view, match each marker to 
        /// its corresponding ViewSection by name, evaluate the ViewSection's Sheet Number parameter,
        /// and hide markers whose section doesn't match the filter.
        /// </summary>
        private void HideNonMatchingSectionsInView(View view, Dictionary<string, ViewSection> sectionByName)
        {
            var markersToHide = new List<ElementId>();

            try
            {
                // Collect only VISIBLE section markers in this view
                var visibleViewers = new FilteredElementCollector(_doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Viewers)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (Element viewer in visibleViewers)
                {
                    if (viewer == null) continue;

                    // Only process section-type viewers
                    if (!IsSectionTypeViewer(viewer))
                        continue;

                    // Match this marker to its ViewSection by name
                    ViewSection matchedSection = FindMatchingSection(viewer, sectionByName);
                    if (matchedSection == null)
                    {
                        // Cannot determine which section this marker represents — hide it to be safe
                        markersToHide.Add(viewer.Id);
                        continue;
                    }

                    // Evaluate the section's Sheet Number to decide keep/hide
                    bool isPlaced = IsSectionPlacedOnSheet(matchedSection, out string sheetNumber);

                    if (_settings.KeepAllPlacedSections)
                    {
                        // Mode 2: Keep All Placed Sections — hide only unplaced ones
                        if (!isPlaced)
                        {
                            markersToHide.Add(viewer.Id);
                        }
                    }
                    else
                    {
                        // Mode 1: Sheet Number Filter — keep only sections on matching sheets
                        if (isPlaced)
                        {
                            bool matches = false;
                            foreach (string enteredNum in _settings.SheetNumbers)
                            {
                                if (string.Equals(sheetNumber, enteredNum.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    matches = true;
                                    break;
                                }
                            }

                            if (!matches)
                            {
                                markersToHide.Add(viewer.Id);
                            }
                        }
                        else
                        {
                            // Unplaced section — hide it
                            markersToHide.Add(viewer.Id);
                        }
                    }
                }

                // Perform the batch hide
                if (markersToHide.Count > 0)
                {
                    try
                    {
                        view.HideElements(markersToHide);
                    }
                    catch
                    {
                        // Fallback: hide one by one
                        foreach (ElementId elId in markersToHide)
                        {
                            try { view.HideElements(new[] { elId }); } catch { }
                        }
                    }
                }
            }
            catch
            {
                // Ignore collector errors
            }
        }

        /// <summary>
        /// Determines if an OST_Viewers element is a section-type viewer (not an elevation, callout, etc.)
        /// by checking its ViewFamilyType. Results are cached per type to avoid repeated lookups.
        /// </summary>
        private bool IsSectionTypeViewer(Element viewer)
        {
            try
            {
                ElementId typeId = viewer.GetTypeId();
                if (typeId == ElementId.InvalidElementId) return false;

                int typeKey = typeId.IntValue();
                if (_sectionTypeCache.TryGetValue(typeKey, out bool cached))
                    return cached;

                var vfType = _doc.GetElement(typeId) as ViewFamilyType;
                bool isSection = vfType != null && vfType.ViewFamily == ViewFamily.Section;
                _sectionTypeCache[typeKey] = isSection;
                return isSection;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Matches an OST_Viewers marker element to its corresponding ViewSection by name
        /// (the marker's Name equals the ViewSection's Name for standard sections).
        /// </summary>
        private static ViewSection FindMatchingSection(Element viewer, Dictionary<string, ViewSection> sectionByName)
        {
            string viewerName = viewer.Name;
            if (!string.IsNullOrEmpty(viewerName) && sectionByName.TryGetValue(viewerName, out ViewSection secByName))
            {
                return secByName;
            }

            return null;
        }

        /// <summary>
        /// Returns true if the view is a supported plan view type.
        /// </summary>
        public static bool IsSupportedPlanView(View view)
        {
            if (view == null || view.IsTemplate)
                return false;

            return view.ViewType == ViewType.FloorPlan ||
                   view.ViewType == ViewType.CeilingPlan ||
                   view.ViewType == ViewType.AreaPlan ||
                   view.ViewType == ViewType.EngineeringPlan;
        }

        /// <summary>
        /// Converts the view type to a friendly name for standard Revit styles.
        /// </summary>
        public static string GetFriendlyPlanTypeName(View view)
        {
            if (view == null) return string.Empty;
            switch (view.ViewType)
            {
                case ViewType.FloorPlan:
                    return "Floor Plan";
                case ViewType.CeilingPlan:
                    return "Ceiling Plan";
                case ViewType.AreaPlan:
                    return "Area Plan";
                case ViewType.EngineeringPlan:
                    return "Structural Plan";
                default:
                    return view.ViewType.ToString();
            }
        }
    }

    /// <summary>
    /// Captures the execution results of the visibility service.
    /// </summary>
    internal sealed class SectionMarkVisibilityResult
    {
        public int ProcessedCount { get; }
        public int SkippedCount { get; }
        public IList<string> Errors { get; }
        public string DiagnosticsReport { get; }

        public SectionMarkVisibilityResult(int processed, int skipped, IList<string> errors, string diagnosticsReport)
        {
            ProcessedCount = processed;
            SkippedCount = skipped;
            Errors = errors ?? new List<string>();
            DiagnosticsReport = diagnosticsReport ?? string.Empty;
        }
    }
}
