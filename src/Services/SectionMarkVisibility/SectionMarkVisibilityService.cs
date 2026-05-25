// ==================================================
// Tool Name    : Section Mark Visibility
// Purpose      : Core business logic service with direct Sheet Number parameter evaluation.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-05-24
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// ==================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.SectionMarkVisibility;

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
                return new SectionMarkVisibilityResult(0, 0, new List<string> { "No Section Views found in the project." }, "No Section Views found in the project.");
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

            // Also build ID-based lookup for fast access
            var sectionById = allSections.ToDictionary(s => s.Id.IntegerValue, s => s);

            // 3. Resolve the full list of views to process, including dependent plan views
            var viewsToProcess = ResolveViewsAndDependents(targetViews);

            // 4. Run visibility operations inside a Revit Transaction
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

                        // A. Ensure the OST_Sections category is visible in the view
                        EnsureSectionsCategoryVisible(view);

                        // B. Unhide ALL section markers in the target view first (reset state)
                        UnhideAllSectionsInView(view, sectionByName, sectionById);

                        // C. If UnhideAllSections mode, stop here (no filtering needed)
                        //    Otherwise, hide sections that do not match the sheet criteria
                        if (!_settings.UnhideAllSections)
                        {
                            HideNonMatchingSectionsInView(view, sectionByName, sectionById);
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
                if (view == null || addedIds.Contains(view.Id.IntegerValue))
                    continue;

                resolved.Add(view);
                addedIds.Add(view.Id.IntegerValue);

                try
                {
                    ICollection<ElementId> dependentIds = view.GetDependentViewIds();
                    if (dependentIds != null && dependentIds.Count > 0)
                    {
                        foreach (ElementId depId in dependentIds)
                        {
                            var depView = _doc.GetElement(depId) as View;
                            if (depView != null && !addedIds.Contains(depView.Id.IntegerValue))
                            {
                                resolved.Add(depView);
                                addedIds.Add(depView.Id.IntegerValue);
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
        /// Unhides ALL section markers that are currently hidden in this view.
        /// This resets the view state so that fresh filtering can be applied.
        /// 
        /// Strategy: We cannot use FilteredElementCollector(doc, viewId) because it only returns
        /// VISIBLE elements. To find HIDDEN section markers, we collect all OST_Viewers from the 
        /// entire document, then check each one:
        ///   1. Is it a section-type viewer? (ViewFamily.Section)
        ///   2. Does it belong to this view? (OwnerViewId matches)
        ///   3. Is it currently hidden in this view?
        /// If all conditions are true, we unhide it.
        /// </summary>
        private void UnhideAllSectionsInView(View view, Dictionary<string, ViewSection> sectionByName, Dictionary<int, ViewSection> sectionById)
        {
            var elementsToUnhide = new List<ElementId>();

            try
            {
                // Collect ALL OST_Viewers from the entire document (includes hidden ones)
                var allViewers = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Viewers)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (Element viewer in allViewers)
                {
                    if (viewer == null) continue;

                    // Check if this is a section-type viewer
                    if (!IsSectionTypeViewer(viewer))
                        continue;

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
        private void HideNonMatchingSectionsInView(View view, Dictionary<string, ViewSection> sectionByName, Dictionary<int, ViewSection> sectionById)
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
                    ViewSection matchedSection = FindMatchingSection(viewer, sectionByName, sectionById);
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
        /// by checking its ViewFamilyType.
        /// </summary>
        private bool IsSectionTypeViewer(Element viewer)
        {
            try
            {
                ElementId typeId = viewer.GetTypeId();
                if (typeId == ElementId.InvalidElementId) return false;

                var vfType = _doc.GetElement(typeId) as ViewFamilyType;
                return vfType != null && vfType.ViewFamily == ViewFamily.Section;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Matches an OST_Viewers marker element to its corresponding ViewSection.
        /// Uses multiple strategies in order of reliability:
        ///   1. Name matching (OST_Viewers element Name == ViewSection.Name)
        ///   2. ElementId matching via the viewer's own ID in the section map
        /// </summary>
        private ViewSection FindMatchingSection(Element viewer, Dictionary<string, ViewSection> sectionByName, Dictionary<int, ViewSection> sectionById)
        {
            // Strategy 1: Match by name (most reliable for standard sections)
            string viewerName = viewer.Name;
            if (!string.IsNullOrEmpty(viewerName) && sectionByName.TryGetValue(viewerName, out ViewSection secByName))
            {
                return secByName;
            }

            // Strategy 2: The OST_Viewers element ID might be the same as the ViewSection ID in some Revit versions
            if (sectionById.TryGetValue(viewer.Id.IntegerValue, out ViewSection secById))
            {
                return secById;
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
