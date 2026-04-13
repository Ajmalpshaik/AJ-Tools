// Tool Name: Smart MEP Tag Service
// Description: Orchestrates intelligent MEP element tagging across all phases.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models.SmartTag, AJTools.Utils

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using AJTools.Models.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Main service that orchestrates the Smart MEP Tagging pipeline.
    /// Processes elements through all phases: pre-flight → collection → tag selection →
    /// placement scoring → clash detection → placement → reporting.
    /// </summary>
    internal static class SmartMepTagService
    {
        private const string ToolTitle = "Smart MEP Tag";
#if DEBUG
        private static readonly object GeometryCheckSync = new object();
        private static bool _geometryChecksCompleted;
#endif

        // ═══════════════════════════════════════════════════════════════
        // PHASE 0 — PRE-FLIGHT CHECKS
        // Validates that the active view is suitable for automated tagging.
        // Every check must pass before any MEP elements are collected.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs all pre-flight checks against the active view.
        /// Returns a PreFlightResult with validated view data or a clear error message.
        /// </summary>
        public static PreFlightResult RunPreFlightChecks(Document doc, View activeView)
        {
            // ── CHECK 1: Active view exists and is not a Sheet ──
            if (activeView == null)
                return PreFlightResult.Fail("No active view found. Please open a view before running Smart Tag.");

            if (activeView is ViewSheet)
                return PreFlightResult.Fail("Smart Tag cannot run on a Sheet view. Please open a Plan, Section, or Elevation view.");

            // ── CHECK 2: View type must be Plan, Section, or Elevation ──
            ViewType viewType = activeView.ViewType;
            bool isValidViewType = viewType == ViewType.FloorPlan
                                || viewType == ViewType.CeilingPlan
                                || viewType == ViewType.Section
                                || viewType == ViewType.Elevation;

            if (!isValidViewType)
            {
                string typeName = viewType.ToString();
                return PreFlightResult.Fail(
                    string.Format("Smart Tag only works in Plan, Section, or Elevation views. Current view type: {0}.", typeName));
            }

            // ── CHECK 3: View must not be a template ──
            if (activeView.IsTemplate)
                return PreFlightResult.Fail("Smart Tag cannot run on a View Template. Please open a normal view.");

            var result = new PreFlightResult
            {
                Passed = true,
                ActiveView = activeView,
                ViewType = viewType
            };

            // ── CHECK 4: Read view scale — controls leader length and tag offset distances ──
            try
            {
                int scale = activeView.Scale;
                if (scale <= 0)
                    return PreFlightResult.Fail("View scale is invalid (zero or negative). Cannot calculate tag offsets.");
                result.ViewScale = scale;
            }
            catch (Exception ex)
            {
                return PreFlightResult.Fail(
                    string.Format("Failed to read view scale: {0}", ex.Message));
            }

            // ── CHECK 5: Retrieve crop region boundary ──
            // The crop region defines the visible area — elements fully outside are excluded.
            try
            {
                if (!activeView.CropBoxActive)
                {
                    // No crop region — use a very large outline so nothing is excluded.
                    // This is valid: some views legitimately have no crop.
                    result.CropRegionPoints = null;
                    result.CropOutline = null;
                    result.Warnings.Add("Crop region is not active. All visible elements will be considered for tagging.");
                }
                else
                {
                    BoundingBoxXYZ cropBox = activeView.CropBox;
                    if (cropBox == null)
                        return PreFlightResult.Fail("Crop region is active but the crop box could not be retrieved.");

                    Outline cropOutline = TryCreateOutline(cropBox);
                    if (cropOutline == null)
                        return PreFlightResult.Fail("Crop region is active but its extents could not be resolved.");

                    result.CropOutline = cropOutline;
                    XYZ min = cropOutline.MinimumPoint;
                    XYZ max = cropOutline.MaximumPoint;

                    // Store the 4 corner points of the crop region for polygon checks if needed.
                    result.CropRegionPoints = new List<XYZ>
                    {
                        new XYZ(min.X, min.Y, min.Z),
                        new XYZ(max.X, min.Y, min.Z),
                        new XYZ(max.X, max.Y, min.Z),
                        new XYZ(min.X, max.Y, min.Z)
                    };
                }
            }
            catch (Exception ex)
            {
                return PreFlightResult.Fail(
                    string.Format("Failed to retrieve crop region: {0}", ex.Message));
            }

            // ── CHECK 6: Retrieve annotation crop boundary ──
            // Annotations outside this boundary won't appear on sheets.
            try
            {
                Parameter annoCropParam = activeView.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
                if (!activeView.CropBoxActive || annoCropParam == null || !annoCropParam.HasValue)
                {
                    result.AnnotationCropOutline = null;
                    result.Warnings.Add("Annotation crop is not active. Tags will be placed within the model crop region only.");
                }
                else
                {
                    bool annoCropActive = annoCropParam != null && annoCropParam.AsInteger() == 1;

                    if (annoCropActive)
                    {
                        // The annotation crop is typically slightly larger than the model crop.
                        // Revit does not expose it directly as a separate bounding box in 2020 API,
                        // so we approximate by inflating the crop outline by a scale-aware margin.
                        if (result.CropOutline != null)
                        {
                            double margin = result.ViewScale * Constants.MM_TO_FEET * 10.0; // 10mm at view scale
                            result.AnnotationCropOutline = new Outline(
                                new XYZ(result.CropOutline.MinimumPoint.X - margin,
                                        result.CropOutline.MinimumPoint.Y - margin,
                                        result.CropOutline.MinimumPoint.Z),
                                new XYZ(result.CropOutline.MaximumPoint.X + margin,
                                        result.CropOutline.MaximumPoint.Y + margin,
                                        result.CropOutline.MaximumPoint.Z));
                        }
                    }
                    else
                    {
                        result.AnnotationCropOutline = null;
                        result.Warnings.Add("Annotation crop is not active. Tags will be placed within the model crop region only.");
                    }
                }
            }
            catch (Exception)
            {
                // Non-fatal — if we can't read the annotation crop, fall back to model crop.
                result.AnnotationCropOutline = null;
                result.Warnings.Add("Could not read annotation crop boundary. Falling back to model crop region.");
            }

            // ── CHECK 7: Detect View Template ──
            // If a view template is applied, some annotation categories may be turned off,
            // which can prevent tags from being placed. Warn but do not abort.
            try
            {
                ElementId templateId = activeView.ViewTemplateId;
                if (templateId != null && templateId != ElementId.InvalidElementId)
                {
                    result.HasViewTemplate = true;
                    Element template = doc.GetElement(templateId);
                    string templateName = template != null ? template.Name : "Unknown";
                    result.Warnings.Add(
                        string.Format("View Template \"{0}\" is applied. If tagging categories are hidden by the template, some tags may fail to place.", templateName));
                }
                else
                {
                    result.HasViewTemplate = false;
                }
            }
            catch (Exception)
            {
                // Non-fatal — proceed without template info.
                result.HasViewTemplate = false;
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // PHASE 1 — ELEMENT COLLECTION & FILTERING
        // Collects MEP elements from the active view, applies 5 filters,
        // and returns a prioritised list of tagging candidates.
        // ═══════════════════════════════════════════════════════════════

        // Size thresholds in feet (Revit internal units).
        private static readonly double MinDuctWidth = 200.0 * Constants.MM_TO_FEET;      // 200mm
        private static readonly double MinPipeDiameter = 25.0 * Constants.MM_TO_FEET;    // 25mm
        private static readonly double MinCurveLength = 300.0 * Constants.MM_TO_FEET;    // 300mm
        private static readonly double DensityRadius = 500.0 * Constants.MM_TO_FEET;     // 500mm
        private const int DensityThreshold = 5;

        /// <summary>
        /// Collects MEP elements from the active view and applies all 5 filters.
        /// Returns a prioritised list of TagCandidate objects ready for Phase 2.
        /// Also populates the results list with skipped elements and their reasons.
        /// </summary>
        public static List<TagCandidate> CollectAndFilterElements(
            Document doc,
            PreFlightResult preflight,
            SmartTagSettingsState settingsState,
            List<TagPlacementResult> results)
        {
            View activeView = preflight.ActiveView;
            var candidates = new List<TagCandidate>();

            // ── Collect all existing tags in this view ONCE ──
            // Used by Filter 3 to skip already-tagged elements.
            HashSet<ElementId> alreadyTaggedIds = CollectAlreadyTaggedElementIds(doc, activeView);

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
                        // Hard constraint: never tag elements from linked models.
                        // Elements in the host model have a null GetTypeId check — linked elements
                        // would come from a RevitLinkInstance collector, which we never use.
                        // FilteredElementCollector scoped to the view only returns host elements,
                        // but we add an explicit guard for safety.
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

            // ── Build a spatial lookup of all collected element midpoints for density checks ──
            // We compute midpoints once and reuse them for Filter 5.
            var midpointLookup = new Dictionary<ElementId, XYZ>();
            foreach (var ewc in allElements)
            {
                XYZ mid = GetElementMidpoint(ewc.Element, activeView);
                if (mid != null)
                    midpointLookup[ewc.Element.Id] = mid;
            }

            // ── Apply filters to each element ──
            foreach (var ewc in allElements)
            {
                Element elem = ewc.Element;
                BuiltInCategory bic = ewc.Category;
                ElementId eid = elem.Id;

                // FILTER 1 — VISIBILITY
                // Element must be visible in the active view (not hidden by workset, filter, or category override).
                try
                {
                    if (elem.IsHidden(activeView))
                    {
                        results.Add(new TagPlacementResult
                        {
                            ElementId = eid, Category = bic, Success = false,
                            SkipReason = TagSkipReason.FilteredOutVisibility,
                            Note = "Element is hidden in view"
                        });
                        continue;
                    }
                }
                catch (Exception)
                {
                    // If visibility check fails, include the element (safe default).
                }

                // FILTER 2 — INSIDE CROP REGION
                // Element bounding box must intersect the crop region. Fully outside = exclude.
                if (preflight.CropOutline != null)
                {
                    BoundingBoxXYZ bbox = elem.get_BoundingBox(activeView);
                    if (bbox == null || !BoundingBoxIntersectsCrop(bbox, preflight.CropOutline))
                    {
                        results.Add(new TagPlacementResult
                        {
                            ElementId = eid, Category = bic, Success = false,
                            SkipReason = TagSkipReason.OutsideCropRegion,
                            Note = "Element is outside the crop region"
                        });
                        continue;
                    }
                }

                // FILTER 3 — NOT ALREADY TAGGED
                if (alreadyTaggedIds.Contains(eid))
                {
                    results.Add(new TagPlacementResult
                    {
                        ElementId = eid, Category = bic, Success = false,
                        SkipReason = TagSkipReason.AlreadyTagged,
                        Note = "Element already has a tag in this view"
                    });
                    continue;
                }

                // FILTER 4 — SIZE THRESHOLD
                // Skip small spurs, flex connections, tiny pipes, and short curve segments.
                string sizeSkipReason;
                if (!PassesSizeFilter(elem, bic, out sizeSkipReason))
                {
                    results.Add(new TagPlacementResult
                    {
                        ElementId = eid, Category = bic, Success = false,
                        SkipReason = TagSkipReason.FilteredOutSize,
                        Note = sizeSkipReason
                    });
                    continue;
                }

                // ── Build the candidate ──
                XYZ midpoint;
                midpointLookup.TryGetValue(eid, out midpoint);
                if (midpoint == null)
                {
                    // Fallback: use bounding box centre.
                    BoundingBoxXYZ bb = elem.get_BoundingBox(activeView);
                    midpoint = GetBoundingBoxCenter(bb);
                }

                if (midpoint == null)
                {
                    // Cannot determine position — skip.
                    results.Add(new TagPlacementResult
                    {
                        ElementId = eid, Category = bic, Success = false,
                        SkipReason = TagSkipReason.FilteredOutVisibility,
                        Note = "Cannot determine element position"
                    });
                    continue;
                }

                // FILTER 5 — DENSITY PRE-CHECK
                // Count elements within 500mm radius. Flag but don't skip yet — priority decides later.
                var candidate = new TagCandidate
                {
                    ElementId = eid,
                    Category = bic,
                    Priority = GetPriority(bic),
                    BoundingBox = elem.get_BoundingBox(activeView),
                    Midpoint = midpoint,
                    IsDenseZone = false,
                    Orientation = GetElementOrientation(elem, activeView)
                };

                candidates.Add(candidate);
            }

            // ── Sort by priority: HIGH first, then MEDIUM, then LOW ──
            MarkDenseZones(candidates);
            candidates.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            return candidates;
        }

        // ─────────────────────────────────────────────
        // Phase 1 — Helper methods
        // ─────────────────────────────────────────────

        /// <summary>
        /// Collects all element IDs that already have a tag in the active view.
        /// Done once at the start — never inside a loop.
        /// </summary>
        private static HashSet<ElementId> CollectAlreadyTaggedElementIds(Document doc, View view)
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
                        ElementId taggedId = tag.TaggedLocalElementId;
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
        private static void MarkDenseZones(List<TagCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return;

            for (int i = 0; i < candidates.Count; i++)
            {
                TagCandidate current = candidates[i];
                if (current == null || current.Midpoint == null)
                {
                    if (current != null)
                        current.IsDenseZone = false;
                    continue;
                }

                int nearbyCount = 0;
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (i == j)
                        continue;

                    TagCandidate other = candidates[j];
                    if (other == null || other.Midpoint == null)
                        continue;

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

        /// <summary>
        /// Checks whether an element passes the minimum size threshold for its category.
        /// Prevents tagging of small spurs, flex connections, and short segments.
        /// </summary>
        private static bool PassesSizeFilter(Element elem, BuiltInCategory category, out string reason)
        {
            reason = null;

            try
            {
                // Check curve length for all curve-based elements (ducts, pipes, cable trays).
                MEPCurve mepCurve = elem as MEPCurve;
                if (mepCurve != null)
                {
                    double length = GetCurveLength(mepCurve);
                    if (length >= 0 && length < MinCurveLength)
                    {
                        reason = string.Format("Curve length {0:F0}mm is below {1:F0}mm minimum",
                            length / Constants.MM_TO_FEET, MinCurveLength / Constants.MM_TO_FEET);
                        return false;
                    }
                }

                switch (category)
                {
                    case BuiltInCategory.OST_DuctCurves:
                        // Check duct width — skip small spurs and flex connections.
                        Duct duct = elem as Duct;
                        if (duct != null)
                        {
                            double width = GetDuctWidth(duct);
                            if (width >= 0 && width < MinDuctWidth)
                            {
                                reason = string.Format("Duct width {0:F0}mm is below {1:F0}mm minimum",
                                    width / Constants.MM_TO_FEET, MinDuctWidth / Constants.MM_TO_FEET);
                                return false;
                            }
                        }
                        break;

                    case BuiltInCategory.OST_PipeCurves:
                        // Check pipe diameter — skip small branch connections.
                        Pipe pipe = elem as Pipe;
                        if (pipe != null)
                        {
                            double diameter = GetPipeDiameter(pipe);
                            if (diameter >= 0 && diameter < MinPipeDiameter)
                            {
                                reason = string.Format("Pipe diameter {0:F0}mm is below {1:F0}mm minimum",
                                    diameter / Constants.MM_TO_FEET, MinPipeDiameter / Constants.MM_TO_FEET);
                                return false;
                            }
                        }
                        break;

                    case BuiltInCategory.OST_PipeAccessory:
                    case BuiltInCategory.OST_DuctAccessory:
                        // Only tag valves, dampers, and major fittings.
                        // We check the family name for common accessory types.
                        if (!IsMajorAccessory(elem))
                        {
                            reason = "Minor accessory — not a valve, damper, or major fitting";
                            return false;
                        }
                        break;
                }
            }
            catch (Exception)
            {
                // If size check fails, include the element (safe default).
            }

            return true;
        }

        /// <summary>
        /// Returns the length of an MEP curve element in feet.
        /// </summary>
        private static double GetCurveLength(MEPCurve mepCurve)
        {
            try
            {
                // Try the Length parameter first (most reliable).
                Parameter lengthParam = mepCurve.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (lengthParam != null && lengthParam.HasValue)
                    return lengthParam.AsDouble();

                // Fallback: compute from the location curve.
                LocationCurve locCurve = mepCurve.Location as LocationCurve;
                if (locCurve != null && locCurve.Curve != null)
                    return locCurve.Curve.Length;
            }
            catch (Exception)
            {
                // Return -1 to indicate unknown — element won't be filtered by length.
            }
            return -1;
        }

        /// <summary>
        /// Returns the duct width (or diameter for round ducts) in feet.
        /// </summary>
        private static double GetDuctWidth(Duct duct)
        {
            try
            {
                // Round duct: use diameter.
                Parameter diamParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                if (diamParam != null && diamParam.HasValue)
                    return diamParam.AsDouble();

                // Rectangular duct: use width.
                Parameter widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                if (widthParam != null && widthParam.HasValue)
                    return widthParam.AsDouble();
            }
            catch (Exception) { }
            return -1;
        }

        /// <summary>
        /// Returns the pipe diameter in feet.
        /// </summary>
        private static double GetPipeDiameter(Pipe pipe)
        {
            try
            {
                Parameter diamParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                if (diamParam != null && diamParam.HasValue)
                    return diamParam.AsDouble();
            }
            catch (Exception) { }
            return -1;
        }

        /// <summary>
        /// Determines whether a pipe/duct accessory is a major fitting worth tagging.
        /// Checks family name for common valve, damper, and major accessory keywords.
        /// </summary>
        private static bool IsMajorAccessory(Element elem)
        {
            try
            {
                string familyName = null;

                // Get the family name from the element's type.
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    FamilySymbol famSym = elem.Document.GetElement(typeId) as FamilySymbol;
                    if (famSym != null && famSym.Family != null)
                        familyName = famSym.Family.Name;
                }

                if (string.IsNullOrEmpty(familyName))
                    return true; // Unknown family — include to be safe.

                string upper = familyName.ToUpperInvariant();

                // Major accessories: valves, dampers, strainers, flow meters, etc.
                return upper.Contains("VALVE")
                    || upper.Contains("DAMPER")
                    || upper.Contains("STRAINER")
                    || upper.Contains("METER")
                    || upper.Contains("REGULATOR")
                    || upper.Contains("TRAP")
                    || upper.Contains("FILTER")
                    || upper.Contains("RELIEF")
                    || upper.Contains("CHECK")
                    || upper.Contains("GATE")
                    || upper.Contains("BALL")
                    || upper.Contains("BUTTERFLY")
                    || upper.Contains("GLOBE")
                    || upper.Contains("PRV")
                    || upper.Contains("CONTROL");
            }
            catch (Exception)
            {
                return true; // On error, include the element.
            }
        }

        /// <summary>
        /// Checks if an element's bounding box intersects the crop region outline.
        /// Partially inside = true (tag will be placed inside the crop even if element extends beyond).
        /// </summary>
        private static bool BoundingBoxIntersectsCrop(BoundingBoxXYZ bbox, Outline cropOutline)
        {
            try
            {
                // Use transformed world-space outlines so rotated bounding boxes are handled correctly.
                Outline elementOutline = TryCreateOutline(bbox);
                return OutlinesIntersect(elementOutline, cropOutline);
            }
            catch (Exception)
            {
                return true; // On error, include the element.
            }
        }

        private static bool OutlinesIntersect(Outline a, Outline b)
        {
            if (a == null || b == null)
                return true;

            XYZ aMin = a.MinimumPoint;
            XYZ aMax = a.MaximumPoint;
            XYZ bMin = b.MinimumPoint;
            XYZ bMax = b.MaximumPoint;

            if (aMax.X < bMin.X || aMin.X > bMax.X)
                return false;
            if (aMax.Y < bMin.Y || aMin.Y > bMax.Y)
                return false;
            if (aMax.Z < bMin.Z || aMin.Z > bMax.Z)
                return false;

            return true;
        }

        private static Outline TryCreateOutline(BoundingBoxXYZ bbox)
        {
            if (bbox == null || bbox.Min == null || bbox.Max == null)
                return null;

            try
            {
                XYZ[] corners = GetBoundingBoxCorners(bbox);
                if (corners == null || corners.Length == 0)
                    return null;

                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double minZ = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;
                double maxZ = double.MinValue;

                foreach (XYZ corner in corners)
                {
                    if (corner == null)
                        continue;

                    minX = Math.Min(minX, corner.X);
                    minY = Math.Min(minY, corner.Y);
                    minZ = Math.Min(minZ, corner.Z);
                    maxX = Math.Max(maxX, corner.X);
                    maxY = Math.Max(maxY, corner.Y);
                    maxZ = Math.Max(maxZ, corner.Z);
                }

                if (minX > maxX || minY > maxY || minZ > maxZ)
                    return null;

                return new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static XYZ[] GetBoundingBoxCorners(BoundingBoxXYZ bbox)
        {
            if (bbox == null || bbox.Min == null || bbox.Max == null)
                return null;

            Transform transform = bbox.Transform ?? Transform.Identity;
            XYZ min = bbox.Min;
            XYZ max = bbox.Max;

            var corners = new[]
            {
                new XYZ(min.X, min.Y, min.Z),
                new XYZ(min.X, min.Y, max.Z),
                new XYZ(min.X, max.Y, min.Z),
                new XYZ(min.X, max.Y, max.Z),
                new XYZ(max.X, min.Y, min.Z),
                new XYZ(max.X, min.Y, max.Z),
                new XYZ(max.X, max.Y, min.Z),
                new XYZ(max.X, max.Y, max.Z)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = transform.OfPoint(corners[i]);
            }

            return corners;
        }

        private static XYZ GetBoundingBoxCenter(BoundingBoxXYZ bbox)
        {
            Outline outline = TryCreateOutline(bbox);
            if (outline == null)
                return null;

            XYZ min = outline.MinimumPoint;
            XYZ max = outline.MaximumPoint;
            return (min + max) * 0.5;
        }

#if DEBUG
        private static void RunGeometryRegressionChecksOnce()
        {
            lock (GeometryCheckSync)
            {
                if (_geometryChecksCompleted)
                    return;

                _geometryChecksCompleted = true;
            }

            string error;
            if (!TryRunGeometryRegressionChecks(out error))
            {
                SmartTagTelemetryTracker.RecordDiagnostic("Geometry regression check failed: " + error);
                return;
            }

            SmartTagTelemetryTracker.RecordDiagnostic("Geometry regression check passed.");
        }

        private static bool TryRunGeometryRegressionChecks(out string error)
        {
            error = null;
            const double tol = 1e-6;

            try
            {
                // Case 1: pure translation.
                var translated = new BoundingBoxXYZ
                {
                    Min = new XYZ(0, 0, 0),
                    Max = new XYZ(2, 4, 0),
                    Transform = Transform.CreateTranslation(new XYZ(10, -3, 5))
                };

                Outline translatedOutline = TryCreateOutline(translated);
                if (translatedOutline == null)
                {
                    error = "Translated bounding box outline is null.";
                    return false;
                }

                if (!NearlyEqual(translatedOutline.MinimumPoint, new XYZ(10, -3, 5), tol)
                    || !NearlyEqual(translatedOutline.MaximumPoint, new XYZ(12, 1, 5), tol))
                {
                    error = "Translated bounding box outline did not match expected extents.";
                    return false;
                }

                // Case 2: 90° rotation about Z at origin.
                var rotated = new BoundingBoxXYZ
                {
                    Min = new XYZ(0, 0, 0),
                    Max = new XYZ(2, 1, 0),
                    Transform = Transform.CreateRotationAtPoint(XYZ.BasisZ, 0.5 * Math.PI, XYZ.Zero)
                };

                Outline rotatedOutline = TryCreateOutline(rotated);
                XYZ rotatedCenter = GetBoundingBoxCenter(rotated);
                if (rotatedOutline == null || rotatedCenter == null)
                {
                    error = "Rotated bounding box outline or center is null.";
                    return false;
                }

                if (!NearlyEqual(rotatedOutline.MinimumPoint, new XYZ(-1, 0, 0), tol)
                    || !NearlyEqual(rotatedOutline.MaximumPoint, new XYZ(0, 2, 0), tol))
                {
                    error = "Rotated bounding box outline did not match expected extents.";
                    return false;
                }

                if (!NearlyEqual(rotatedCenter, new XYZ(-0.5, 1, 0), tol))
                {
                    error = "Rotated bounding box center did not match expected position.";
                    return false;
                }

                string engineError;
                if (!SmartTagPlacementEngine.TryRunGeometryRegressionChecks(out engineError))
                {
                    error = "Placement engine check failed: " + engineError;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool NearlyEqual(XYZ a, XYZ b, double tolerance)
        {
            if (a == null || b == null)
                return false;

            return Math.Abs(a.X - b.X) <= tolerance
                && Math.Abs(a.Y - b.Y) <= tolerance
                && Math.Abs(a.Z - b.Z) <= tolerance;
        }
#endif

        /// <summary>
        /// Returns the midpoint of an MEP element.
        /// For curve elements: midpoint of the location curve.
        /// For equipment/accessories: centre of the bounding box.
        /// </summary>
        private static XYZ GetElementMidpoint(Element elem, View view)
        {
            try
            {
                // Curve-based elements (ducts, pipes, cable trays): use curve midpoint.
                LocationCurve locCurve = elem.Location as LocationCurve;
                if (locCurve != null && locCurve.Curve != null)
                {
                    return locCurve.Curve.Evaluate(0.5, true);
                }

                // Point-based elements (equipment, accessories): use location point.
                LocationPoint locPoint = elem.Location as LocationPoint;
                if (locPoint != null)
                {
                    return locPoint.Point;
                }

                // Fallback: bounding box centre.
                BoundingBoxXYZ bb = elem.get_BoundingBox(view);
                return GetBoundingBoxCenter(bb);
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// Determines the orientation of an MEP element in the active view.
        /// Horizontal ducts/pipes → prefer tag above/below.
        /// Vertical risers → prefer tag left/right.
        /// </summary>
        private static ElementOrientation GetElementOrientation(Element elem, View view)
        {
            try
            {
                LocationCurve locCurve = elem.Location as LocationCurve;
                if (locCurve == null || locCurve.Curve == null)
                    return ElementOrientation.Other; // Equipment/accessories with no curve direction.

                Curve curve = locCurve.Curve;
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                XYZ direction = (end - start).Normalize();

                if (direction == null)
                    return ElementOrientation.Other;

                // Section/elevation: vertical means mostly along global Z.
                ViewType vt = view.ViewType;
                if (vt == ViewType.Section || vt == ViewType.Elevation)
                {
                    if (Math.Abs(direction.Z) > 0.7)
                        return ElementOrientation.Vertical;
                    return ElementOrientation.Horizontal;
                }

                // Plan views: classify against view axes so rotated plans are handled correctly.
                XYZ viewRight = view.RightDirection;
                XYZ viewUp = view.UpDirection;
                XYZ viewNormal = view.ViewDirection;

                if (viewRight == null || viewUp == null || viewNormal == null)
                {
                    if (Math.Abs(direction.Z) > 0.7)
                        return ElementOrientation.Vertical;
                    return ElementOrientation.Horizontal;
                }

                viewRight = viewRight.Normalize();
                viewUp = viewUp.Normalize();
                viewNormal = viewNormal.Normalize();

                // True risers in plan (mostly perpendicular to the plan plane).
                double normalComponent = Math.Abs(direction.DotProduct(viewNormal));
                if (normalComponent > 0.7)
                    return ElementOrientation.Vertical;

                // Remove out-of-plane component and compare in-plane axis alignment.
                XYZ inPlane = direction - viewNormal.Multiply(direction.DotProduct(viewNormal));
                if (inPlane.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                    return ElementOrientation.Other;

                inPlane = inPlane.Normalize();
                double alongRight = Math.Abs(inPlane.DotProduct(viewRight));
                double alongUp = Math.Abs(inPlane.DotProduct(viewUp));

                if (alongRight >= alongUp)
                    return ElementOrientation.Horizontal;

                return ElementOrientation.Vertical;
            }
            catch (Exception)
            {
                return ElementOrientation.Other;
            }
        }

        /// <summary>
        /// Maps a built-in category to its tagging priority.
        /// </summary>
        private static TagPriority GetPriority(BuiltInCategory category)
        {
            switch (category)
            {
                case BuiltInCategory.OST_MechanicalEquipment:
                case BuiltInCategory.OST_DuctCurves:
                case BuiltInCategory.OST_PipeCurves:
                    return TagPriority.High;

                case BuiltInCategory.OST_PipeAccessory:
                case BuiltInCategory.OST_DuctAccessory:
                    return TagPriority.Medium;

                case BuiltInCategory.OST_CableTray:
                    return TagPriority.Low;

                default:
                    return TagPriority.Low;
            }
        }

        /// <summary>
        /// Simple wrapper to pair an element with its collection category
        /// (avoids re-reading category from the element during filtering).
        /// </summary>
        private class ElementWithCategory
        {
            public Element Element { get; private set; }
            public BuiltInCategory Category { get; private set; }

            public ElementWithCategory(Element element, BuiltInCategory category)
            {
                Element = element;
                Category = category;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PHASE 2 — TAG FAMILY SELECTION
        // For each candidate, finds the correct tag FamilySymbol based on
        // category and view type. Falls back to any loaded tag if the
        // preferred name isn't found. Removes candidates with no tag family.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Preferred tag family names by category and view context.
        /// Key = BuiltInCategory, Value = list of name fragments to search for (first match wins).
        /// Plan-specific and Section-specific names are tried first, then generic names.
        /// </summary>
        private static readonly Dictionary<BuiltInCategory, string[]> PreferredTagNamesPlan =
            new Dictionary<BuiltInCategory, string[]>
            {
                { BuiltInCategory.OST_DuctCurves,            new[] { "Duct Tag - Plan", "Duct Tag" } },
                { BuiltInCategory.OST_PipeCurves,            new[] { "Pipe Tag - Plan", "Pipe Tag" } },
                { BuiltInCategory.OST_MechanicalEquipment,   new[] { "Equipment Tag", "Mechanical Equipment Tag" } },
                { BuiltInCategory.OST_PipeAccessory,         new[] { "Pipe Accessory Tag", "Accessory Tag" } },
                { BuiltInCategory.OST_DuctAccessory,         new[] { "Duct Accessory Tag", "Accessory Tag" } },
                { BuiltInCategory.OST_CableTray,             new[] { "Cable Tray Tag" } }
            };

        private static readonly Dictionary<BuiltInCategory, string[]> PreferredTagNamesSection =
            new Dictionary<BuiltInCategory, string[]>
            {
                { BuiltInCategory.OST_DuctCurves,            new[] { "Duct Tag - Section", "Duct Tag" } },
                { BuiltInCategory.OST_PipeCurves,            new[] { "Pipe Tag - Section", "Pipe Tag" } },
                { BuiltInCategory.OST_MechanicalEquipment,   new[] { "Equipment Tag", "Mechanical Equipment Tag" } },
                { BuiltInCategory.OST_PipeAccessory,         new[] { "Pipe Accessory Tag", "Accessory Tag" } },
                { BuiltInCategory.OST_DuctAccessory,         new[] { "Duct Accessory Tag", "Accessory Tag" } },
                { BuiltInCategory.OST_CableTray,             new[] { "Cable Tray Tag" } }
            };

        /// <summary>
        /// Resolves the correct tag FamilySymbol for each candidate.
        /// Candidates with no available tag family are removed and logged.
        /// Returns a list of warning messages for fallback tag usage.
        /// </summary>
        public static List<string> SelectTagFamilies(
            Document doc,
            PreFlightResult preflight,
            List<TagCandidate> candidates,
            List<TagPlacementResult> results)
        {
            var warnings = new List<string>();
            bool isSectionOrElevation = preflight.ViewType == ViewType.Section
                                     || preflight.ViewType == ViewType.Elevation;

            // ── Build a cache of all loaded tag types per category ──
            // Done ONCE — never inside the candidate loop.
            Dictionary<BuiltInCategory, List<FamilySymbol>> tagCache = BuildTagFamilyCache(doc);
            Dictionary<BuiltInCategory, ElementId> configuredTagTypeByCategory =
                CollectConfiguredTagTypes(doc, preflight.ActiveView, tagCache);

            // ── Resolve tag for each candidate ──
            // Iterate backwards so we can remove candidates with no tag family.
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                TagCandidate candidate = candidates[i];
                BuiltInCategory bic = candidate.Category;

                List<FamilySymbol> availableTags;
                if (!tagCache.TryGetValue(bic, out availableTags) || availableTags.Count == 0)
                {
                    // No tag family loaded at all for this category.
                    results.Add(new TagPlacementResult
                    {
                        ElementId = candidate.ElementId,
                        Category = bic,
                        Success = false,
                        SkipReason = TagSkipReason.NoTagFamilyAvailable,
                        Note = string.Format("No tag family loaded for {0}", bic)
                    });
                    candidates.RemoveAt(i);
                    continue;
                }

                // First preference: use the tag type already configured by Revit
                // (existing tags in this view, then project default family type for the tag category).
                ElementId configuredTypeId;
                if (configuredTagTypeByCategory.TryGetValue(bic, out configuredTypeId))
                {
                    FamilySymbol configured = FindTagById(availableTags, configuredTypeId);
                    if (configured != null)
                    {
                        candidate.TagTypeId = configured.Id;
                        continue;
                    }
                }

                // Try preferred names first (view-type-specific, then generic).
                var preferredNames = isSectionOrElevation ? PreferredTagNamesSection : PreferredTagNamesPlan;
                string[] namesToTry;
                FamilySymbol matched = null;

                if (preferredNames.TryGetValue(bic, out namesToTry))
                {
                    foreach (string preferredName in namesToTry)
                    {
                        matched = FindTagByName(availableTags, preferredName);
                        if (matched != null)
                            break;
                    }
                }

                if (matched != null)
                {
                    candidate.TagTypeId = matched.Id;
                }
                else
                {
                    // Fallback: use the first available loaded tag for this category.
                    candidate.TagTypeId = availableTags[0].Id;
                    string fallbackName = availableTags[0].Family != null
                        ? availableTags[0].Family.Name
                        : availableTags[0].Name;
                    string msg = string.Format("Preferred tag not found for {0} — fallback used: \"{1}\"",
                        bic, fallbackName);
                    if (!warnings.Contains(msg))
                        warnings.Add(msg);
                }
            }

            return warnings;
        }

        /// <summary>
        /// Builds a lookup of all loaded tag FamilySymbol instances per taggable MEP category.
        /// Collected ONCE at the start — maps each BuiltInCategory to its available tag types.
        /// </summary>
        private static Dictionary<BuiltInCategory, List<FamilySymbol>> BuildTagFamilyCache(Document doc)
        {
            var cache = new Dictionary<BuiltInCategory, List<FamilySymbol>>();

            // Initialize empty lists for each category we care about.
            foreach (BuiltInCategory bic in SmartTagSettingsTracker.SupportedCategories)
                cache[bic] = new List<FamilySymbol>();

            try
            {
                // Collect all IndependentTag types in the project.
                // Each tag FamilySymbol has a category it can tag — we match on that.
                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol));

                foreach (Element elem in collector)
                {
                    FamilySymbol fs = elem as FamilySymbol;
                    if (fs == null || fs.Family == null)
                        continue;

                    // In Revit, a tag family's category indicates what it tags.
                    // E.g. a "Duct Tag" family has category OST_DuctTags which tags OST_DuctCurves.
                    Category famCat = fs.Category ?? fs.Family.FamilyCategory;
                    if (famCat == null)
                        continue;

                    BuiltInCategory tagCat = MapTagCategoryToModelCategory(famCat.Id.IntegerValue);
                    if (tagCat == BuiltInCategory.INVALID)
                        continue;

                    List<FamilySymbol> list;
                    if (cache.TryGetValue(tagCat, out list))
                        list.Add(fs);
                }
            }
            catch (Exception)
            {
                // If collection fails, cache will have empty lists — elements will be skipped.
            }

            foreach (KeyValuePair<BuiltInCategory, List<FamilySymbol>> kvp in cache)
            {
                kvp.Value.Sort((a, b) => string.Compare(
                    GetTagDisplayName(a),
                    GetTagDisplayName(b),
                    StringComparison.OrdinalIgnoreCase));
            }

            return cache;
        }

        /// <summary>
        /// Maps a tag annotation category to the model category it tags.
        /// E.g. OST_DuctTags → OST_DuctCurves.
        /// </summary>
        private static Dictionary<BuiltInCategory, ElementId> CollectConfiguredTagTypes(
            Document doc,
            View view,
            Dictionary<BuiltInCategory, List<FamilySymbol>> tagCache)
        {
            var preferred = new Dictionary<BuiltInCategory, ElementId>();
            CollectViewTagTypePreferences(doc, view, tagCache, preferred);
            CollectProjectDefaultTagTypePreferences(doc, tagCache, preferred);
            return preferred;
        }

        private static void CollectViewTagTypePreferences(
            Document doc,
            View view,
            Dictionary<BuiltInCategory, List<FamilySymbol>> tagCache,
            Dictionary<BuiltInCategory, ElementId> preferred)
        {
            if (doc == null || view == null || tagCache == null || preferred == null)
                return;

            try
            {
                var usage = new Dictionary<BuiltInCategory, Dictionary<int, int>>();
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                foreach (Element elem in tags)
                {
                    IndependentTag tag = elem as IndependentTag;
                    if (tag == null)
                        continue;

                    ElementId taggedId;
                    try
                    {
                        taggedId = tag.TaggedLocalElementId;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (taggedId == null || taggedId == ElementId.InvalidElementId)
                        continue;

                    Element taggedElement = doc.GetElement(taggedId);
                    if (taggedElement == null || taggedElement.Category == null)
                        continue;

                    BuiltInCategory modelCategory;
                    try
                    {
                        modelCategory = (BuiltInCategory)taggedElement.Category.Id.IntegerValue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    List<FamilySymbol> available;
                    if (!tagCache.TryGetValue(modelCategory, out available) || available.Count == 0)
                        continue;

                    ElementId typeId = tag.GetTypeId();
                    if (typeId == null || typeId == ElementId.InvalidElementId)
                        continue;

                    if (FindTagById(available, typeId) == null)
                        continue;

                    Dictionary<int, int> categoryUsage;
                    if (!usage.TryGetValue(modelCategory, out categoryUsage))
                    {
                        categoryUsage = new Dictionary<int, int>();
                        usage[modelCategory] = categoryUsage;
                    }

                    int key = typeId.IntegerValue;
                    int count;
                    categoryUsage.TryGetValue(key, out count);
                    categoryUsage[key] = count + 1;
                }

                foreach (KeyValuePair<BuiltInCategory, Dictionary<int, int>> kvp in usage)
                {
                    int bestTypeInt = ElementId.InvalidElementId.IntegerValue;
                    int bestCount = -1;
                    foreach (KeyValuePair<int, int> countKvp in kvp.Value)
                    {
                        if (countKvp.Value > bestCount)
                        {
                            bestCount = countKvp.Value;
                            bestTypeInt = countKvp.Key;
                        }
                    }

                    if (bestTypeInt != ElementId.InvalidElementId.IntegerValue)
                        preferred[kvp.Key] = new ElementId(bestTypeInt);
                }
            }
            catch (Exception)
            {
                // Non-fatal: if preference extraction fails, continue with defaults/fallbacks.
            }
        }

        private static void CollectProjectDefaultTagTypePreferences(
            Document doc,
            Dictionary<BuiltInCategory, List<FamilySymbol>> tagCache,
            Dictionary<BuiltInCategory, ElementId> preferred)
        {
            if (doc == null || tagCache == null || preferred == null)
                return;

            foreach (KeyValuePair<BuiltInCategory, List<FamilySymbol>> kvp in tagCache)
            {
                BuiltInCategory modelCategory = kvp.Key;
                if (preferred.ContainsKey(modelCategory))
                    continue;

                BuiltInCategory tagCategory = MapModelCategoryToTagCategory(modelCategory);
                if (tagCategory == BuiltInCategory.INVALID)
                    continue;

                try
                {
                    ElementId defaultTypeId = doc.GetDefaultFamilyTypeId(new ElementId((int)tagCategory));
                    if (defaultTypeId == null || defaultTypeId == ElementId.InvalidElementId)
                        continue;

                    if (FindTagById(kvp.Value, defaultTypeId) != null)
                        preferred[modelCategory] = defaultTypeId;
                }
                catch (Exception)
                {
                    // Some categories may not expose a default family type in this project/version.
                }
            }
        }

        private static FamilySymbol FindTagById(List<FamilySymbol> tags, ElementId typeId)
        {
            if (tags == null || typeId == null || typeId == ElementId.InvalidElementId)
                return null;

            foreach (FamilySymbol fs in tags)
            {
                if (fs != null && fs.Id != null && fs.Id.IntegerValue == typeId.IntegerValue)
                    return fs;
            }

            return null;
        }

        private static BuiltInCategory MapModelCategoryToTagCategory(BuiltInCategory modelCategory)
        {
            switch (modelCategory)
            {
                case BuiltInCategory.OST_DuctCurves:
                    return BuiltInCategory.OST_DuctTags;
                case BuiltInCategory.OST_PipeCurves:
                    return BuiltInCategory.OST_PipeTags;
                case BuiltInCategory.OST_MechanicalEquipment:
                    return BuiltInCategory.OST_MechanicalEquipmentTags;
                case BuiltInCategory.OST_PipeAccessory:
                    return BuiltInCategory.OST_PipeAccessoryTags;
                case BuiltInCategory.OST_DuctAccessory:
                    return BuiltInCategory.OST_DuctAccessoryTags;
                case BuiltInCategory.OST_CableTray:
                    return BuiltInCategory.OST_CableTrayTags;
                default:
                    return BuiltInCategory.INVALID;
            }
        }

        private static BuiltInCategory MapTagCategoryToModelCategory(int tagCategoryId)
        {
            // Tag category integer IDs for Revit 2020.
            switch ((BuiltInCategory)tagCategoryId)
            {
                case BuiltInCategory.OST_DuctTags:
                    return BuiltInCategory.OST_DuctCurves;
                case BuiltInCategory.OST_PipeTags:
                    return BuiltInCategory.OST_PipeCurves;
                case BuiltInCategory.OST_MechanicalEquipmentTags:
                    return BuiltInCategory.OST_MechanicalEquipment;
                case BuiltInCategory.OST_PipeAccessoryTags:
                    return BuiltInCategory.OST_PipeAccessory;
                case BuiltInCategory.OST_DuctAccessoryTags:
                    return BuiltInCategory.OST_DuctAccessory;
                case BuiltInCategory.OST_CableTrayTags:
                    return BuiltInCategory.OST_CableTray;
                default:
                    return BuiltInCategory.INVALID;
            }
        }

        /// <summary>
        /// Searches available tag families for one matching the preferred name (case-insensitive contains).
        /// Checks both the Family name and the FamilySymbol (type) name.
        /// </summary>
        private static FamilySymbol FindTagByName(List<FamilySymbol> tags, string preferredName)
        {
            string upper = preferredName.ToUpperInvariant();

            // First pass: exact family name match.
            foreach (FamilySymbol fs in tags)
            {
                try
                {
                    if (fs.Family != null && fs.Family.Name.ToUpperInvariant() == upper)
                        return fs;
                }
                catch (Exception) { }
            }

            // Second pass: family name contains the preferred name.
            foreach (FamilySymbol fs in tags)
            {
                try
                {
                    if (fs.Family != null && fs.Family.Name.ToUpperInvariant().Contains(upper))
                        return fs;
                }
                catch (Exception) { }
            }

            // Third pass: type name contains the preferred name.
            foreach (FamilySymbol fs in tags)
            {
                try
                {
                    if (fs.Name.ToUpperInvariant().Contains(upper))
                        return fs;
                }
                catch (Exception) { }
            }

            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        private static string GetTagDisplayName(FamilySymbol symbol)
        {
            if (symbol == null)
                return string.Empty;

            string familyName = symbol.Family != null ? symbol.Family.Name : string.Empty;
            string typeName = symbol.Name ?? string.Empty;
            return familyName + " :: " + typeName;
        }

        // MAIN ENTRY POINT
        // Called by the command — orchestrates all phases in sequence.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Executes the full Smart MEP Tagging pipeline on the active view.
        /// Returns Result.Succeeded if at least one tag was placed, Result.Cancelled otherwise.
        /// </summary>
        public static Result Execute(ExternalCommandData commandData, ref string message)
        {
            if (commandData?.Application == null)
            {
                message = "Revit command context is not available.";
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Failed;
            }

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

#if DEBUG
            RunGeometryRegressionChecksOnce();
#endif

            View activeView = doc.ActiveView;

            // ── PHASE 0: Pre-flight checks ──
            PreFlightResult preflight = RunPreFlightChecks(doc, activeView);
            if (!preflight.Passed)
            {
                DialogHelper.ShowError(ToolTitle, preflight.ErrorMessage);
                message = preflight.ErrorMessage;
                return Result.Cancelled;
            }

            // Show any warnings from pre-flight.
            if (preflight.Warnings.Count > 0)
            {
                string warningText = string.Join("\n- ", preflight.Warnings);
                bool proceed = DialogHelper.ShowYesNo(
                    ToolTitle + " - Warnings",
                    string.Format("Pre-flight checks passed with warnings:\n\n- {0}\n\nDo you want to continue?", warningText));
                if (!proceed)
                    return Result.Cancelled;
            }

            // ── PHASE 1: Collect and filter MEP elements ──
            var settingsTracker = new SmartTagSettingsTracker(doc);
            SmartTagSettingsState settingsState =
                SmartTagSettingsTracker.EnsureDefaults(settingsTracker.LastState);
            bool hasEnabledCategory = SmartTagSettingsTracker.SupportedCategories
                .Any(c => SmartTagSettingsTracker.IsCategoryEnabled(settingsState, c));
            if (!hasEnabledCategory)
            {
                DialogHelper.ShowError(ToolTitle, "No category is enabled in Smart MEP Tag Settings.");
                return Result.Cancelled;
            }

            var results = new List<TagPlacementResult>();
            List<TagCandidate> candidates = CollectAndFilterElements(doc, preflight, settingsState, results);
            int candidatesAfterFilter = candidates.Count;

            if (candidates.Count == 0)
            {
                // Build a summary of why no candidates were found.
                int alreadyTagged = results.Count(r => r.SkipReason == TagSkipReason.AlreadyTagged);
                int filtered = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutSize
                                               || r.SkipReason == TagSkipReason.FilteredOutVisibility
                                               || r.SkipReason == TagSkipReason.OutsideCropRegion);

                DialogHelper.ShowInfo(ToolTitle,
                    string.Format("No elements to tag in this view.\n\n"
                        + "Already Tagged: {0}\nFiltered Out: {1}\nTotal Analysed: {2}",
                        alreadyTagged, filtered, results.Count));
                RecordTelemetrySafe(preflight, settingsState, results, candidatesAfterFilter, candidates.Count);
                return Result.Succeeded;
            }

            // ── PHASE 2: Select tag families for each candidate ──
            List<string> tagWarnings = SelectTagFamilies(doc, preflight, candidates, results);

            if (candidates.Count == 0)
            {
                DialogHelper.ShowInfo(ToolTitle,
                    "No tag families are loaded for the MEP categories in this view.\n"
                    + "Please load the required tag families and try again.");
                RecordTelemetrySafe(preflight, settingsState, results, candidatesAfterFilter, candidates.Count);
                return Result.Succeeded;
            }

            // ── PHASES 3–6: Score positions, detect clashes, reposition, and place tags ──
            SmartTagPlacementEngine.ProcessAndPlaceTags(doc, preflight, settingsState, candidates, results);
            RecordTelemetrySafe(preflight, settingsState, results, candidatesAfterFilter, candidates.Count);

            // ── PHASE 7: Generate output report ──
            SmartTagReportGenerator.ShowReport(preflight, results, tagWarnings);

            int successCount = results.Count(r => r.Success);
            return successCount > 0 ? Result.Succeeded : Result.Cancelled;
        }

        private static void RecordTelemetrySafe(
            PreFlightResult preflight,
            SmartTagSettingsState settingsState,
            IList<TagPlacementResult> results,
            int candidatesAfterFilter,
            int candidatesAfterTagTypeResolution)
        {
            try
            {
                SmartTagTelemetryTracker.RecordRun(
                    preflight,
                    settingsState,
                    results,
                    candidatesAfterFilter,
                    candidatesAfterTagTypeResolution);
            }
            catch
            {
                // Telemetry is best effort and must never block command execution.
            }
        }

        private static IEnumerable<BuiltInCategory> GetEnabledCategories(SmartTagSettingsState settingsState)
        {
            foreach (BuiltInCategory category in SmartTagSettingsTracker.SupportedCategories)
            {
                if (SmartTagSettingsTracker.IsCategoryEnabled(settingsState, category))
                    yield return category;
            }
        }
    }
}

