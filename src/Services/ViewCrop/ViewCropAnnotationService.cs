#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropAnnotationService.cs
 * Purpose       : Enables annotation crop and applies equal offsets based on the view crop.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-11
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active Revit document, supported plan views, annotation crop offset.
 * Output        : Annotation crop enabled on each view with equal offsets (left/right/top/bottom).
 *
 * Notes         :
 * - Single TransactionGroup per batch + one Transaction per view => single undo step.
 * - ElementId validity check uses shared ElementIdHelper.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Refactor/audit pass: shared ElementIdHelper, metadata, version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;
using AJTools.Utils;

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
            if (ElementIdHelper.IsValid(templateId) && cropActiveParam != null && cropActiveParam.IsReadOnly)
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

    }
}
