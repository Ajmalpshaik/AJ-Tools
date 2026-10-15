#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 1, View Check
 * File Name     : SmartTagViewCheck.cs
 * Purpose       : Step 1 of a Smart MEP Tags run: checks the active view can be tagged (view type, not a template,
 *                 scale, crop region, annotation crop) before any element is collected.
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
 * Input         : Active view.
 * Output        : PreFlightResult - pass/fail with a plain message, plus scale and crop outlines.
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
using static AJTools.Services.SmartTag.SmartTagElementCollector;
using static AJTools.Services.SmartTag.SmartTagElementFilters;
using static AJTools.Services.SmartTag.SmartTagElementGeometry;
using static AJTools.Services.SmartTag.SmartTagPlacementEngine;
using static AJTools.Services.SmartTag.SmartTagPositionFinder;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagTypeSelector;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Checks that the active view can be tagged, before anything is collected.
    /// </summary>
    internal static class SmartTagViewCheck
    {
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
                    Element template = doc.GetElement(templateId);
                    string templateName = template != null ? template.Name : "Unknown";
                    result.Warnings.Add(
                        string.Format("View Template \"{0}\" is applied. If tagging categories are hidden by the template, some tags may fail to place.", templateName));
                }
            }
            catch (Exception)
            {
                // Non-fatal — proceed without template info.
            }

            return result;
        }
    }
}
