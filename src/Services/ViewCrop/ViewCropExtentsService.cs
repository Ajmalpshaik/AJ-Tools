// ==================================================
// Tool Name    : View Crop
// Purpose      : Calculates and applies crop extents for supported target views.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-08
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// Input        : Active Revit document, active or selected target views, and View Crop settings.
// Output       : Updated view crop or annotation crop settings for supported target views.
// Notes        : Skips unsupported, template, scope-box-controlled, and view-template-locked views.
// Changelog    : v1.0.1 - Standardized metadata after production cleanup.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;
using AJTools.Utils;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Executes crop fitting for a set of views using either view-scoped or model-wide elements.
    /// </summary>
    internal sealed class ViewCropExtentsService
    {
        private const bool RectangularFallbackAllowed = true;

        private readonly Document _doc;
        private readonly ViewCropSettings _settings;
        private readonly ViewCropExtentSource _source;
        private readonly List<GlobalExtentCandidate> _globalCandidates;

        private sealed class GlobalExtentCandidate
        {
            internal GlobalExtentCandidate(int categoryId, XYZ[] corners)
            {
                CategoryId = categoryId;
                Corners = corners;
            }

            internal int CategoryId { get; }

            internal XYZ[] Corners { get; }
        }

        internal ViewCropExtentsService(Document doc, ViewCropSettings settings, ViewCropExtentSource source)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _settings = (settings ?? new ViewCropSettings()).Clone();
            _source = source;

            _globalCandidates = source == ViewCropExtentSource.AllModelElements
                ? CollectAllModelCandidates()
                : new List<GlobalExtentCandidate>();
        }

        internal ViewCropBatchResult Process(IList<View> targetViews, string transactionName)
        {
            var batch = new ViewCropBatchResult();
            if (targetViews == null || targetViews.Count == 0)
                return batch;

            using (var group = new TransactionGroup(_doc, transactionName))
            {
                group.Start();

                foreach (View view in targetViews)
                {
                    var item = new ViewCropViewResult(
                        view?.Id ?? ElementId.InvalidElementId,
                        view?.Name ?? "(missing)",
                        ViewCropViewSupport.ToFriendlyTypeName(view));

                    batch.Add(item);

                    if (!ViewCropViewSupport.TryValidateType(view, out string typeError))
                    {
                        item.MarkSkipped(typeError);
                        continue;
                    }

                    try
                    {
                        using (Transaction tx = new Transaction(_doc, transactionName))
                        {
                            tx.Start();

                            ViewCropResultState state = ProcessSingleView(view, out string reason);

                            if (state == ViewCropResultState.Updated)
                            {
                                tx.Commit();
                                item.MarkUpdated(reason);
                            }
                            else
                            {
                                tx.RollBack();
                                if (state == ViewCropResultState.Skipped)
                                    item.MarkSkipped(reason);
                                else
                                    item.MarkFailed(reason);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        item.MarkFailed($"Unexpected error: {ex.Message}");
                    }
                }

                if (batch.UpdatedCount > 0)
                    group.Assimilate();
                else
                    group.RollBack();
            }

            return batch;
        }

        private ViewCropResultState ProcessSingleView(View view, out string reason)
        {
            reason = string.Empty;

            if (!TryEnsureEditableCrop(view, out reason))
                return ViewCropResultState.Skipped;

            ViewCropGeometryProjectionHelper.PlaneBounds bounds = _source == ViewCropExtentSource.ActiveViewElements
                ? CollectBoundsFromActiveViewElements(view)
                : CollectBoundsFromAllModelElements(view);

            if (bounds == null || !bounds.HasData)
            {
                reason = _source == ViewCropExtentSource.ActiveViewElements
                    ? "No valid visible model elements found in this view."
                    : "No valid model elements found for this view orientation.";
                return ViewCropResultState.Skipped;
            }

            bounds.Inflate(Math.Max(0, _settings.MarginInternal));
            bounds.EnsureMinimumSpan(5.0 * Constants.MM_TO_FEET);

            if (!TryApplyCrop(view, bounds, out reason))
                return ViewCropResultState.Failed;

            return ViewCropResultState.Updated;
        }

        private bool TryEnsureEditableCrop(View view, out string reason)
        {
            reason = string.Empty;

            if (view == null || !view.IsValidObject)
            {
                reason = "View is not valid.";
                return false;
            }

            if (view.CropBox == null)
            {
                reason = "This view does not expose an editable crop box.";
                return false;
            }

            Parameter scopeBoxParam = view.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
            if (scopeBoxParam != null && scopeBoxParam.HasValue)
            {
                ElementId scopeBoxId = scopeBoxParam.AsElementId();
                if (HasValidElementId(scopeBoxId))
                {
                    reason = "Scope box is assigned. Remove scope box control before running this tool.";
                    return false;
                }
            }

            Parameter cropActiveParam = view.get_Parameter(BuiltInParameter.VIEWER_CROP_REGION);
            ElementId templateId = view.ViewTemplateId;
            if (HasValidElementId(templateId) && cropActiveParam != null && cropActiveParam.IsReadOnly)
            {
                reason = "View template controls crop settings for this view.";
                return false;
            }

            if (!view.CropBoxActive)
            {
                try
                {
                    view.CropBoxActive = true;
                }
                catch (Exception ex)
                {
                    reason = $"Crop View is disabled and cannot be enabled: {ex.Message}";
                    return false;
                }
            }

            return true;
        }

        private ViewCropGeometryProjectionHelper.PlaneBounds CollectBoundsFromActiveViewElements(View view)
        {
            Transform modelToView = ViewCropGeometryProjectionHelper.GetModelToViewTransform(view);
            var bounds = new ViewCropGeometryProjectionHelper.PlaneBounds();
            bool hasData = false;

            Dictionary<int, bool> hiddenCategoryCache = _settings.IgnoreHiddenCategories
                ? new Dictionary<int, bool>()
                : null;

            IEnumerable<Element> elements = new FilteredElementCollector(_doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element element in elements)
            {
                if (!ShouldUseElement(element, view, hiddenCategoryCache))
                    continue;

                BoundingBoxXYZ bbox = null;
                try
                {
                    bbox = element.get_BoundingBox(view);
                }
                catch
                {
                    continue;
                }

                if (!IsValidBoundingBox(bbox))
                    continue;

                if (ViewCropGeometryProjectionHelper.TryIncludeBoundingBox(bbox, modelToView, bounds))
                    hasData = true;
            }

            return hasData ? bounds : null;
        }

        private ViewCropGeometryProjectionHelper.PlaneBounds CollectBoundsFromAllModelElements(View view)
        {
            if (_globalCandidates.Count == 0)
                return null;

            Transform modelToView = ViewCropGeometryProjectionHelper.GetModelToViewTransform(view);
            var bounds = new ViewCropGeometryProjectionHelper.PlaneBounds();
            bool hasData = false;

            Dictionary<int, bool> hiddenCategoryCache = _settings.IgnoreHiddenCategories
                ? new Dictionary<int, bool>()
                : null;

            foreach (GlobalExtentCandidate candidate in _globalCandidates)
            {
                if (_settings.IgnoreHiddenCategories
                    && IsCategoryHidden(view, candidate.CategoryId, hiddenCategoryCache))
                {
                    continue;
                }

                XYZ[] corners = candidate.Corners;
                for (int i = 0; i < corners.Length; i++)
                {
                    XYZ local;
                    try
                    {
                        local = modelToView.OfPoint(corners[i]);
                    }
                    catch
                    {
                        continue;
                    }

                    if (!IsFinite(local))
                        continue;

                    bounds.Include(local.X, local.Y);
                    hasData = true;
                }
            }

            return hasData ? bounds : null;
        }

        private List<GlobalExtentCandidate> CollectAllModelCandidates()
        {
            var candidates = new List<GlobalExtentCandidate>();

            IEnumerable<Element> elements = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent()
                .ToElements();

            foreach (Element element in elements)
            {
                if (!ShouldUseElement(element, null, null))
                    continue;

                BoundingBoxXYZ bbox;
                try
                {
                    bbox = element.get_BoundingBox(null);
                }
                catch
                {
                    continue;
                }

                if (!IsValidBoundingBox(bbox))
                    continue;

                XYZ[] corners = ViewCropGeometryProjectionHelper.GetBoundingBoxCorners(bbox);
                if (corners.Length == 0)
                    continue;

                Category category = element.Category;
                if (category == null || category.Id == null)
                    continue;

                int categoryId = category.Id.IntegerValue;
                candidates.Add(new GlobalExtentCandidate(categoryId, corners));
            }

            return candidates;
        }

        private bool ShouldUseElement(Element element, View view, Dictionary<int, bool> hiddenCategoryCache)
        {
            if (element == null || !element.IsValidObject)
                return false;

            if (element.ViewSpecific)
                return false;

            if (!_settings.IncludeDatums && element is DatumPlane)
                return false;

            if (element is SketchPlane || element is ImportInstance || element is Viewport)
                return false;

            Category category = element.Category;
            if (category == null || category.Id == null)
                return false;

            if (element is RevitLinkInstance)
            {
                if (!_settings.IncludeRevitLinks)
                    return false;
            }
            else if (category.CategoryType != CategoryType.Model)
            {
                return false;
            }

            int categoryId = category.Id.IntegerValue;
            if (!_settings.IncludeDatums && IsDatumCategory(categoryId))
                return false;

            if (_settings.IgnoreHiddenCategories && view != null)
            {
                if (IsCategoryHidden(view, categoryId, hiddenCategoryCache))
                    return false;

                try
                {
                    if (element.IsHidden(view))
                        return false;
                }
                catch
                {
                    // If Revit cannot evaluate element-level hide state, keep the element.
                }
            }

            return true;
        }

        private bool IsCategoryHidden(View view, int categoryId, Dictionary<int, bool> cache)
        {
            if (!_settings.IgnoreHiddenCategories || view == null || cache == null)
                return false;

            if (cache.TryGetValue(categoryId, out bool hidden))
                return hidden;

            hidden = false;
            try
            {
                ElementId categoryElementId = new ElementId(categoryId);
                if (view.CanCategoryBeHidden(categoryElementId))
                    hidden = view.GetCategoryHidden(categoryElementId);
            }
            catch
            {
                hidden = false;
            }

            cache[categoryId] = hidden;
            return hidden;
        }

        private bool TryApplyCrop(View view, ViewCropGeometryProjectionHelper.PlaneBounds bounds, out string reason)
        {
            reason = string.Empty;
            BoundingBoxXYZ currentCrop = view.CropBox;
            if (currentCrop == null)
            {
                reason = "View crop box is unavailable.";
                return false;
            }

            if (_settings.RectangularCropOnly)
            {
                if (TrySetRectangularCrop(view, currentCrop, bounds, out string shapeError))
                    return true;

                if (TrySetCropBox(view, currentCrop, bounds, out string boxFallbackError))
                {
                    reason = "Rectangular crop fallback used CropBox update.";
                    return true;
                }

                reason = CombineErrors(shapeError, boxFallbackError);
                return false;
            }

            if (TrySetCropBox(view, currentCrop, bounds, out string boxError))
                return true;

            string shapeFallbackError = string.Empty;
            if (RectangularFallbackAllowed && TrySetRectangularCrop(view, currentCrop, bounds, out shapeFallbackError))
            {
                reason = "Applied rectangular fallback because CropBox update failed.";
                return true;
            }

            reason = CombineErrors(boxError, shapeFallbackError);
            return false;
        }

        private static bool TrySetCropBox(
            View view,
            BoundingBoxXYZ currentCrop,
            ViewCropGeometryProjectionHelper.PlaneBounds bounds,
            out string error)
        {
            error = string.Empty;

            try
            {
                var newCrop = new BoundingBoxXYZ
                {
                    Transform = currentCrop.Transform,
                    Min = new XYZ(bounds.MinX, bounds.MinY, currentCrop.Min.Z),
                    Max = new XYZ(bounds.MaxX, bounds.MaxY, currentCrop.Max.Z)
                };

                view.CropBox = newCrop;
                return true;
            }
            catch (Exception ex)
            {
                error = $"CropBox update failed: {ex.Message}";
                return false;
            }
        }

        private static bool TrySetRectangularCrop(
            View view,
            BoundingBoxXYZ currentCrop,
            ViewCropGeometryProjectionHelper.PlaneBounds bounds,
            out string error)
        {
            error = string.Empty;

            try
            {
                ViewCropRegionShapeManager manager = view.GetCropRegionShapeManager();
                if (manager == null)
                {
                    error = "Crop region shape manager is not available.";
                    return false;
                }

                CurveLoop loop = ViewCropGeometryProjectionHelper.BuildRectangularCropLoop(currentCrop, bounds);

                bool isShapeValid = true;
                try
                {
                    isShapeValid = manager.IsCropRegionShapeValid(loop);
                }
                catch
                {
                    // If Revit does not expose validation, attempt SetCropShape directly.
                    isShapeValid = true;
                }

                if (!isShapeValid)
                {
                    error = "Computed rectangular crop shape is invalid.";
                    return false;
                }

                manager.SetCropShape(loop);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Crop shape update failed: {ex.Message}";
                return false;
            }
        }

        private static bool IsDatumCategory(int categoryId)
        {
            return categoryId == (int)BuiltInCategory.OST_Levels
                || categoryId == (int)BuiltInCategory.OST_Grids
                || categoryId == (int)BuiltInCategory.OST_CLines;
        }

        private static bool IsValidBoundingBox(BoundingBoxXYZ bbox)
        {
            if (bbox == null || bbox.Min == null || bbox.Max == null)
                return false;

            XYZ min = bbox.Min;
            XYZ max = bbox.Max;
            if (!IsFinite(min) || !IsFinite(max))
                return false;

            double dx = Math.Abs(max.X - min.X);
            double dy = Math.Abs(max.Y - min.Y);
            double dz = Math.Abs(max.Z - min.Z);

            return dx > Constants.ZERO_LENGTH_TOLERANCE
                || dy > Constants.ZERO_LENGTH_TOLERANCE
                || dz > Constants.ZERO_LENGTH_TOLERANCE;
        }

        private static bool IsFinite(XYZ point)
        {
            return point != null
                && !double.IsNaN(point.X)
                && !double.IsNaN(point.Y)
                && !double.IsNaN(point.Z)
                && !double.IsInfinity(point.X)
                && !double.IsInfinity(point.Y)
                && !double.IsInfinity(point.Z);
        }

        private static string CombineErrors(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
                return string.IsNullOrWhiteSpace(second) ? "Crop update failed." : second;

            if (string.IsNullOrWhiteSpace(second))
                return first;

            return $"{first} | {second}";
        }

        private static bool HasValidElementId(ElementId id)
        {
            return id != null && id.IntegerValue != ElementId.InvalidElementId.IntegerValue;
        }
    }
}
