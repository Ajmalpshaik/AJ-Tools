#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 2, Find Elements
 * File Name     : SmartTagElementCollector.cs
 * Purpose       : Step 2: finds the elements to tag - this model's own elements of the ticked categories and, when
 *                 "Also tag elements in linked models" is on, the linked ones - runs every skip rule on them and
 *                 returns them in priority order (High first).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-10-15
 * Last Updated  : 2026-10-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Utils, the other Smart MEP Tags files
 *
 * Input         : Active view, Smart MEP Tag settings.
 * Output        : Tag candidates in priority order; one result line per skipped element.
 *
 * Notes         :
 * - Part of the Smart MEP Tags restructure (suite 1.58.0): the code that used to sit in
 *   SmartMepTagService.cs and SmartTagPlacementEngine.cs is split one job per file, in step
 *   folders. Every routine was moved as-is - tagging results are unchanged.
 * - The numbers that tune the algorithm live in SmartTagTuning.cs.
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Split out of the old Smart MEP Tag service / placement engine.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using AJTools.Models.SmartTag;
using AJTools.Services.CreateTags;
using AJTools.Services.LeaderLogic;
using AJTools.Services.TagClash;
using AJTools.Services.ViewCrop;
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagCandidatePositions;
using static AJTools.Services.SmartTag.SmartTagCreator;
using static AJTools.Services.SmartTag.SmartTagElementFilters;
using static AJTools.Services.SmartTag.SmartTagElementGeometry;
using static AJTools.Services.SmartTag.SmartTagPlacementEngine;
using static AJTools.Services.SmartTag.SmartTagPositionFinder;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagTypeSelector;
using static AJTools.Services.SmartTag.SmartTagViewCheck;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Finds the elements to tag, runs the skip rules, and puts them in priority order.
    /// </summary>
    internal static class SmartTagElementCollector
    {
        // MinDuctWidth (100mm), MinPipeDiameter (0mm) and MinCurveLength (1000mm) became user
        // settings in v1.49.7 and no longer live here. Their values are now the DEFAULTS in
        // SmartTagSettingsTracker, and PassesSizeFilter reads whatever the user set. The old
        // GetPipeDiameter helper went with them - it had no other caller. GetDuctWidth stayed,
        // because the placement engine still uses it for HostWidth, which is not a filter.
        internal static readonly double DensityRadius = 500.0 * Constants.MM_TO_FEET;     // 500mm

        internal const int DensityThreshold = 5;

        /// <summary>
        /// Collects MEP elements from the active view and applies all 5 filters.
        /// Returns a prioritised list of TagCandidate objects ready for Phase 2.
        /// Also populates the results list with skipped elements and their reasons.
        /// </summary>
        /// <param name="linkedScan">
        /// What the linked-model scan found, when "also tag elements in linked models" is on; null when
        /// it is off. The report uses it to say how many links were read.
        /// </param>
        public static List<TagCandidate> CollectAndFilterElements(
            Document doc,
            PreFlightResult preflight,
            SmartTagSettingsState settingsState,
            List<TagPlacementResult> results,
            out SmartTagLinkedScan linkedScan)
        {
            View activeView = preflight.ActiveView;
            var candidates = new List<TagCandidate>();
            linkedScan = null;

            // Read once per run, not per element - these are file-backed settings.
            bool skipVerticalRuns = TagClashSettings.ShouldSkipVerticalRuns();
            bool includeLinked = TagClashSettings.ShouldSmartTagIncludeLinked();

            // ── Collect all existing tags in this view ONCE ──
            // Used by Filter 3 to skip already-tagged elements. Linked elements are keyed by
            // (link instance, element) - a bare id would mix them up with this model's own elements.
            HashSet<ElementId> alreadyTaggedIds = CollectAlreadyTaggedElementIds(doc, activeView);
            HashSet<(long LinkId, long ElementId)> alreadyTaggedLinked = includeLinked
                ? SmartTagLinkedElementCollector.CollectAlreadyTaggedLinkedKeys(doc, activeView)
                : new HashSet<(long LinkId, long ElementId)>();

            // ── Crop region, measured in the view's own plane ──
            // A crop region bounds what you see ACROSS the view - X and Y in view coordinates. It
            // says nothing about depth; depth is the view range, and FilteredElementCollector scoped
            // to the view has already applied it. Filter 2 used to compare world-space X, Y AND Z
            // against the crop box, so a view whose crop box Z range did not happen to straddle the
            // model could reject every element in one go and report "No elements to tag". The View
            // Crop tools already measure the correct way - ViewCropGeometryProjectionHelper.PlaneBounds
            // is X/Y only - so this now does the same.
            Transform cropModelToView = null;
            double cropMinX = 0.0, cropMinY = 0.0, cropMaxX = 0.0, cropMaxY = 0.0;
            if (preflight.CropOutline != null)
            {
                try
                {
                    BoundingBoxXYZ cropBox = activeView.CropBox;
                    if (cropBox != null && cropBox.Min != null && cropBox.Max != null)
                    {
                        cropModelToView = ViewCropGeometryProjectionHelper.GetModelToViewTransform(activeView);
                        cropMinX = Math.Min(cropBox.Min.X, cropBox.Max.X);
                        cropMaxX = Math.Max(cropBox.Min.X, cropBox.Max.X);
                        cropMinY = Math.Min(cropBox.Min.Y, cropBox.Max.Y);
                        cropMaxY = Math.Max(cropBox.Min.Y, cropBox.Max.Y);
                    }
                }
                catch (Exception)
                {
                    // Cannot resolve the crop plane - tag everything rather than drop everything.
                    cropModelToView = null;
                }
            }

            // ── Collect raw elements per category ──
            var allElements = new List<ElementWithCategory>();
            foreach (BuiltInCategory bic in GetEnabledCategories(settingsState))
            {
                try
                {
                    var collector = new FilteredElementCollector(doc, activeView.Id)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType();

                    foreach (Element elem in collector)
                    {
                        // A collector scoped to the active view only ever returns elements of this
                        // document, never the contents of a link - linked elements are added below,
                        // and only when the user switched that on.
                        if (elem == null)
                            continue;

                        allElements.Add(new ElementWithCategory(elem, bic));
                    }
                }
                catch (Exception)
                {
                    // If a category fails to collect (e.g. not present in project), skip silently.
                }
            }

            // ── Linked models (only when "also tag elements in linked models" is ticked) ──
            // Their geometry is carried into this model's coordinates once, here, so every filter and
            // the placement engine below work on host positions exactly as they do for host elements.
            if (includeLinked)
            {
                linkedScan = SmartTagLinkedElementCollector.Collect(
                    doc, activeView, GetEnabledCategories(settingsState));

                foreach (SmartTagLinkedElement linked in linkedScan.Elements)
                    allElements.Add(ElementWithCategory.FromLink(linked));
            }

            // ── Build a spatial lookup of all collected element midpoints for density checks ──
            // We compute midpoints once and reuse them for Filter 5. Keyed by the entry itself, not by
            // ElementId: a linked element can share its id with one of this model's elements.
            var midpointLookup = new Dictionary<ElementWithCategory, XYZ>();
            foreach (var ewc in allElements)
            {
                XYZ mid = ewc.IsLinked
                    ? ToHostPoint(GetElementMidpoint(ewc.Element, null), ewc.ToHost)
                    : GetElementMidpoint(ewc.Element, activeView);
                if (mid != null)
                    midpointLookup[ewc] = mid;
            }

            // ── Apply filters to each element ──
            foreach (var ewc in allElements)
            {
                Element elem = ewc.Element;
                BuiltInCategory bic = ewc.Category;
                ElementId eid = elem.Id;

                // FILTER 1 — VISIBILITY
                // Element must be visible in the active view (not hidden by workset, filter, or category override).
                // A linked element was already checked against the view when the link was read -
                // IsHidden cannot be asked of it with a view from another model.
                if (!ewc.IsLinked)
                {
                    try
                    {
                        if (elem.IsHidden(activeView))
                        {
                            results.Add(ewc.Skip(TagSkipReason.FilteredOutVisibility, "Element is hidden in view"));
                            continue;
                        }
                    }
                    catch (Exception)
                    {
                        // If visibility check fails, include the element (safe default).
                    }
                }

                // The element's box in this model's coordinates. For a linked element it is the box
                // carried through the link transform, read once when the link was scanned.
                BoundingBoxXYZ viewBox = ewc.IsLinked ? ewc.HostBox : elem.get_BoundingBox(activeView);

                // FILTER 2 — INSIDE CROP REGION
                // Element must overlap the crop rectangle in the view plane. Fully outside = exclude.
                // A missing bounding box is no longer treated as "outside": it means unmeasurable,
                // and the midpoint fallback further down can still place the tag.
                if (cropModelToView != null)
                {
                    if (!BoundingBoxOverlapsCropInViewPlane(
                            viewBox, cropModelToView, cropMinX, cropMinY, cropMaxX, cropMaxY))
                    {
                        results.Add(ewc.Skip(TagSkipReason.OutsideCropRegion, "Element is outside the crop region"));
                        continue;
                    }
                }

                // FILTER 3 — NOT ALREADY TAGGED
                bool alreadyTagged = ewc.IsLinked
                    ? alreadyTaggedLinked.Contains(SmartTagLinkedElementCollector.MakeKey(ewc.Link.Id, eid))
                    : alreadyTaggedIds.Contains(eid);
                if (alreadyTagged)
                {
                    results.Add(ewc.Skip(TagSkipReason.AlreadyTagged, "Element already has a tag in this view"));
                    continue;
                }

                // FILTER 4 — SIZE THRESHOLD
                // Skip small spurs, flex connections, tiny pipes, and short curve segments.
                string sizeSkipReason;
                if (!PassesSizeFilter(elem, bic, settingsState, out sizeSkipReason))
                {
                    results.Add(ewc.Skip(TagSkipReason.FilteredOutSize, sizeSkipReason));
                    continue;
                }

                // FILTER 4.2 — MINOR PIPE ACCESSORIES
                // A name filter, not a size one: it used to run inside the size filter and was
                // reported as "Below size threshold", which sent the user looking at sizes.
                string accessorySkipReason;
                if (IsMinorPipeAccessory(elem, bic, out accessorySkipReason))
                {
                    results.Add(ewc.Skip(TagSkipReason.FilteredOutType, accessorySkipReason));
                    continue;
                }

                // FILTER 4.5 — VERTICAL RUNS
                // One rule for every tagging tool now. This used to be hard-coded ON and duct-only,
                // while Create Tags and Stack Tags already skipped duct, pipe AND cable tray - so the
                // same vertical pipe was treated differently depending on which button was pressed.
                // The check itself lives in CreateTagsEligibilityFilter so there is a single
                // implementation; it moves to the shared eligibility block in the wider tidy-up.
                // A link can be turned in plan but never tilted, so vertical in the link is vertical here.
                if (skipVerticalRuns
                    && elem is MEPCurve verticalCandidate
                    && CreateTagsEligibilityFilter.IsVerticalMepCurve(verticalCandidate))
                {
                    results.Add(ewc.Skip(TagSkipReason.FilteredOutType, "Vertical run is excluded"));
                    continue;
                }

                // ── Build the candidate ──
                XYZ midpoint;
                midpointLookup.TryGetValue(ewc, out midpoint);
                if (midpoint == null)
                {
                    // Fallback: use bounding box centre.
                    midpoint = GetBoundingBoxCenter(viewBox);
                }

                if (midpoint == null)
                {
                    // Cannot determine position — skip.
                    results.Add(ewc.Skip(TagSkipReason.FilteredOutVisibility, "Cannot determine element position"));
                    continue;
                }

                Curve elementCurve = null;
                LocationCurve locCurve = elem.Location as LocationCurve;
                if (locCurve != null && locCurve.Curve != null)
                {
                    elementCurve = ewc.IsLinked
                        ? ToHostCurve(locCurve.Curve, ewc.ToHost)
                        : locCurve.Curve;
                }

                // Every category gets a width now, so the placement engine measures each tag's gap
                // from the element's EDGE ("150 mm from its edge"), not from its centre. Only ducts
                // had one before; an accessory or a piece of equipment got 0, so its tag sat the gap
                // from its middle and its own outline could block every side.
                double hostWidth = GetHostWidth(elem, bic, activeView, viewBox, ewc.IsLinked ? ewc.ToHost : null);

                // A linked run's direction has to be read in this model's coordinates - the link may
                // be turned in plan - so its orientation comes from the carried-over curve.
                ElementOrientation orientation = ewc.IsLinked
                    ? (elementCurve != null ? GetCurveOrientation(elementCurve, activeView) : ElementOrientation.Other)
                    : GetElementOrientation(elem, activeView);

                // FILTER 5 — DENSITY PRE-CHECK
                // Count elements within 500mm radius. Flag but don't skip yet — priority decides later.
                var candidate = new TagCandidate
                {
                    ElementId = eid,
                    LinkInstanceId = ewc.IsLinked ? ewc.Link.Id : null,
                    LinkName = ewc.LinkName,
                    Category = bic,
                    Priority = SmartTagSettingsTracker.ResolvePriority(settingsState, bic),
                    BoundingBox = viewBox,
                    Midpoint = midpoint,
                    ElementCurve = elementCurve,
                    IsDenseZone = false,
                    Orientation = orientation,
                    HostWidth = hostWidth
                };

                candidates.Add(candidate);
            }

            // ── Sort by priority: HIGH first, then MEDIUM, then LOW ──
            MarkDenseZones(candidates);
            candidates.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            return candidates;
        }

        /// <summary>A linked-model point in this model's coordinates; null stays null.</summary>
        internal static XYZ ToHostPoint(XYZ point, Transform toHost)
        {
            if (point == null)
                return null;

            return toHost == null ? point : toHost.OfPoint(point);
        }

        /// <summary>A linked-model curve in this model's coordinates; null when it cannot be moved.</summary>
        internal static Curve ToHostCurve(Curve curve, Transform toHost)
        {
            if (curve == null || toHost == null)
                return curve;

            try
            {
                return curve.CreateTransformed(toHost);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Collects all element IDs that already have a tag in the active view.
        /// Done once at the start — never inside a loop.
        /// </summary>
        internal static HashSet<ElementId> CollectAlreadyTaggedElementIds(Document doc, View view)
        {
            var taggedIds = new HashSet<ElementId>();
            try
            {
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                foreach (Element tagElem in tags)
                {
                    IndependentTag tag = tagElem as IndependentTag;
                    if (tag == null)
                        continue;

                    try
                    {
                        // Revit 2020: use TaggedLocalElementId property.
                        ElementId taggedId = TagCompat.GetTaggedLocalElementId(tag);
                        if (taggedId != null && taggedId != ElementId.InvalidElementId)
                            taggedIds.Add(taggedId);
                    }
                    catch (Exception)
                    {
                        // Some tags may not have a valid tagged element — skip.
                    }
                }
            }
            catch (Exception)
            {
                // If tag collection fails, return empty set (all elements treated as untagged).
            }
            return taggedIds;
        }

        /// <summary>
        /// Marks each candidate as dense when more than the threshold number of nearby
        /// candidates exist within the configured radius.
        /// </summary>
        /// <remarks>
        /// Uses AnnotationSpatialIndex as a coarse X/Y pre-filter instead of comparing every
        /// candidate against every other candidate. Midpoint is a true 3D point and the original
        /// check is a full 3D DistanceTo, but for any two points, their X/Y-only distance can never
        /// exceed their full 3D distance (adding a Z term only ever adds to the total under the
        /// square root) - so any pair within DensityRadius in 3D is guaranteed to also be within
        /// DensityRadius in X/Y, and therefore inside the 2*DensityRadius query square used below.
        /// The square can admit extra candidates whose 3D distance is actually greater than
        /// DensityRadius (a duct directly above/below another, say), but the exact same 3D
        /// DistanceTo &lt;= DensityRadius check that ran in the original O(n^2) version still runs on
        /// every candidate the query returns, so those extras get filtered back out - the final
        /// IsDenseZone result is identical to before, just without the full pairwise scan.
        /// </remarks>
        internal static void MarkDenseZones(List<TagCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return;

            var candidateByBox = new Dictionary<AnnotationBox, TagCandidate>(candidates.Count);
            var index = new AnnotationSpatialIndex(DensityRadius);

            foreach (TagCandidate candidate in candidates)
            {
                if (candidate == null || candidate.Midpoint == null)
                {
                    if (candidate != null)
                        candidate.IsDenseZone = false;
                    continue;
                }

                // A point box (min == max), indexed by X/Y only - see remarks above.
                var box = new AnnotationBox(
                    candidate.Midpoint.X, candidate.Midpoint.Y,
                    candidate.Midpoint.X, candidate.Midpoint.Y);
                candidateByBox[box] = candidate;
                index.Add(box);
            }

            foreach (TagCandidate current in candidates)
            {
                if (current == null || current.Midpoint == null)
                    continue;

                AnnotationBox queryRegion = new AnnotationBox(
                    current.Midpoint.X - DensityRadius, current.Midpoint.Y - DensityRadius,
                    current.Midpoint.X + DensityRadius, current.Midpoint.Y + DensityRadius);

                int nearbyCount = 0;
                foreach (AnnotationBox hitBox in index.Query(queryRegion))
                {
                    TagCandidate other;
                    if (!candidateByBox.TryGetValue(hitBox, out other) || ReferenceEquals(other, current))
                        continue;

                    // Exact same distance check as the original O(n^2) version - the index only
                    // narrowed down candidates worth checking, it did not decide membership itself.
                    if (current.Midpoint.DistanceTo(other.Midpoint) <= DensityRadius)
                    {
                        nearbyCount++;
                        if (nearbyCount > DensityThreshold)
                        {
                            current.IsDenseZone = true;
                            break;
                        }
                    }
                }

                if (nearbyCount <= DensityThreshold)
                    current.IsDenseZone = false;
            }
        }

        internal static IEnumerable<BuiltInCategory> GetEnabledCategories(SmartTagSettingsState settingsState)
        {
            foreach (BuiltInCategory category in SmartTagSettingsTracker.SupportedCategories)
            {
                if (SmartTagSettingsTracker.IsCategoryEnabled(settingsState, category))
                    yield return category;
            }
        }

        /// <summary>
        /// Simple wrapper to pair an element with its collection category
        /// (avoids re-reading category from the element during filtering).
        /// </summary>
        internal class ElementWithCategory
        {
            public Element Element { get; private set; }
            public BuiltInCategory Category { get; private set; }

            /// <summary>The link the element is seen through; null for this model's own elements.</summary>
            public RevitLinkInstance Link { get; private set; }

            /// <summary>Link-to-host transform; null for this model's own elements.</summary>
            public Transform ToHost { get; private set; }

            /// <summary>The element's box in this model's coordinates (linked elements only).</summary>
            public BoundingBoxXYZ HostBox { get; private set; }

            public string LinkName { get; private set; }

            public bool IsLinked => Link != null;

            public ElementWithCategory(Element element, BuiltInCategory category)
            {
                Element = element;
                Category = category;
                LinkName = string.Empty;
            }

            public static ElementWithCategory FromLink(SmartTagLinkedElement linked)
            {
                return new ElementWithCategory(linked.Element, linked.Category)
                {
                    Link = linked.Link,
                    ToHost = linked.ToHost,
                    LinkName = linked.LinkName,
                    HostBox = linked.HostBox
                };
            }

            /// <summary>A skipped-element result that keeps the link, so the report can name it.</summary>
            public TagPlacementResult Skip(TagSkipReason reason, string note)
            {
                return new TagPlacementResult
                {
                    ElementId = Element.Id,
                    LinkInstanceId = Link?.Id,
                    LinkName = LinkName,
                    Category = Category,
                    Success = false,
                    SkipReason = reason,
                    Note = note
                };
            }
        }
    }
}
