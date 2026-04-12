// Tool Name: View Crop Annotation Service
// Description: Enables and offsets annotation crop based on the active crop box.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-11
// Revit Version: 2020

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Applies annotation crop activation and offset changes for a set of views.
    /// </summary>
    internal sealed class ViewCropAnnotationService
    {
        private readonly Document _doc;
        private readonly ViewCropAnnotationSettings _settings;

        internal ViewCropAnnotationService(Document doc, ViewCropAnnotationSettings settings)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _settings = (settings ?? new ViewCropAnnotationSettings()).Clone();
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
                        using (var tx = new Transaction(_doc, transactionName))
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

            ViewCropRegionShapeManager manager;
            try
            {
                manager = view.GetCropRegionShapeManager();
            }
            catch (Exception ex)
            {
                reason = $"Could not access crop region manager: {ex.Message}";
                return ViewCropResultState.Failed;
            }

            if (manager == null)
            {
                reason = "Crop region manager is not available for this view.";
                return ViewCropResultState.Skipped;
            }

            if (!manager.CanHaveAnnotationCrop)
            {
                reason = "Annotation crop is not available for this view type.";
                return ViewCropResultState.Skipped;
            }

            Parameter annotationCropParam = view.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
            if (annotationCropParam == null)
            {
                reason = "This view does not expose an annotation crop parameter.";
                return ViewCropResultState.Skipped;
            }

            if (annotationCropParam.IsReadOnly)
            {
                reason = "View template controls annotation crop settings for this view.";
                return ViewCropResultState.Skipped;
            }

            try
            {
                if (annotationCropParam.AsInteger() != 1)
                    annotationCropParam.Set(1);
            }
            catch (Exception ex)
            {
                reason = $"Could not enable annotation crop: {ex.Message}";
                return ViewCropResultState.Failed;
            }

            double offset = Math.Max(0, _settings.OffsetInternal);
            try
            {
                manager.LeftAnnotationCropOffset = offset;
                manager.RightAnnotationCropOffset = offset;
                manager.TopAnnotationCropOffset = offset;
                manager.BottomAnnotationCropOffset = offset;
            }
            catch (Exception ex)
            {
                reason = $"Could not set annotation crop offsets: {ex.Message}";
                return ViewCropResultState.Failed;
            }

            reason = $"Annotation crop enabled with {Math.Max(0, _settings.OffsetMm):0.###} mm offset.";
            return ViewCropResultState.Updated;
        }

        private static bool TryEnsureEditableCrop(View view, out string reason)
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

        private static bool HasValidElementId(ElementId id)
        {
            return id != null && id.IntegerValue != ElementId.InvalidElementId.IntegerValue;
        }
    }
}
