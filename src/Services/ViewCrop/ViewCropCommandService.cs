#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropCommandService.cs
 * Purpose       : Coordinates View Crop UI flow, target view selection, processing, and reporting.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-06-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, WPF
 *
 * Input         : Active Revit document, active or selected target views (Floor/Ceiling/Engineering/Area plans), View Crop settings.
 * Output        : Updated view crop settings for supported target views; batch summary + optional diagnostics window.
 *
 * Notes         :
 * - Tool scope: Active View OR a user-selected batch of plan views.
 * - Supported view types only: Floor Plan, Ceiling Plan, Engineering Plan, Area Plan.
 * - Skips unsupported, template, scope-box-controlled, and view-template-locked views.
 * - Bulk-edit confirmation is shown when more than one view is targeted (skill safety gate).
 * - Summary and error-log handling delegated to ViewCropReportPresenter.
 *
 * Changelog     :
 * v1.2.0 (2026-06-28) - Merged two commands into one: ExtentSource now read from ViewCropSettings (user sets mode in the dialog).
 * v1.1.0 (2026-06-27) - Refactor/audit pass: bulk-edit confirmation, shared report presenter, ElementId helper, metadata, version coverage notes.
 * v1.0.2 (2026-05-24) - Premium UI redesign with presets and integrated annotation crop.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.ViewCrop;
using AJTools.UI.ViewCrop;
using AJTools.Utils;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Handles UI flow, target view selection, processing invocation, and summary reporting.
    /// </summary>
    internal static class ViewCropCommandService
    {
        internal static Result Execute(
            ExternalCommandData commandData,
            string commandTitle,
            ref string message)
        {
            string extentSourceLabel = "(not yet set)";
            try
            {
                UIDocument uidoc = commandData.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError(commandTitle, message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                {
                    DialogHelper.ShowError(commandTitle, message);
                    return Result.Cancelled;
                }

                View activeView = doc.ActiveView;
                if (activeView == null)
                {
                    DialogHelper.ShowError(commandTitle, "No active view found.");
                    return Result.Cancelled;
                }

                var settingsTracker = new ViewCropSettingsTracker(doc);
                var optionsWindow = new ViewCropOptionsWindow(commandTitle, settingsTracker.LastSettings);
                new WindowInteropHelper(optionsWindow)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

                bool? optionsResult = optionsWindow.ShowDialog();
                if (optionsResult != true)
                    return Result.Cancelled;

                ViewCropSettings settings = optionsWindow.SelectedSettings ?? new ViewCropSettings();
                extentSourceLabel = settings.ExtentSource.ToString();
                settingsTracker.Save(settings);

                IList<View> targetViews = ResolveTargetViews(
                    commandData,
                    doc,
                    activeView,
                    optionsWindow.ApplyToActiveViewOnly,
                    out string resolveError);

                if (!string.IsNullOrWhiteSpace(resolveError))
                {
                    DialogHelper.ShowError(commandTitle, resolveError);
                    message = resolveError;
                    return Result.Cancelled;
                }

                if (targetViews == null || targetViews.Count == 0)
                    return Result.Cancelled;

                if (!ConfirmBulkRun(commandTitle, targetViews.Count, "view crop"))
                    return Result.Cancelled;

                var service = new ViewCropExtentsService(doc, settings, settings.ExtentSource);
                var batch = service.Process(targetViews, $"AJ Tools - {commandTitle}");

                if (settings.ApplyAnnotationCrop && batch.UpdatedCount > 0)
                {
                    RunIntegratedAnnotationCrop(doc, batch, settings, commandTitle);
                }

                try
                {
                    ViewCropReportPresenter.ShowSummaryIfNeeded(commandTitle, "View crop", batch);
                }
                catch (Exception summaryEx)
                {
                    string summaryLog = ViewCropReportPresenter.TryWriteErrorLog("ViewCrop", commandTitle, extentSourceLabel, summaryEx);
                    DialogHelper.ShowError(
                        commandTitle,
                        $"Processing finished, but summary dialog failed.\n{summaryEx.Message}\n\nLog: {summaryLog}");
                }

                if (settings.ShowDiagnostics && batch.UpdatedCount > 0)
                {
                    ShowDiagnosticsIfAny(commandData, commandTitle, batch);
                }

                return ToCommandResult(batch);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                string logPath = ViewCropReportPresenter.TryWriteErrorLog("ViewCrop", commandTitle, extentSourceLabel, ex);
                DialogHelper.ShowError(
                    commandTitle,
                    $"{ex.Message}\n\nDetails log:\n{logPath}");
                return Result.Failed;
            }
        }

        internal static IList<View> ResolveTargetViews(
            ExternalCommandData commandData,
            Document doc,
            View activeView,
            bool applyToActiveOnly,
            out string error)
        {
            error = string.Empty;

            if (applyToActiveOnly)
            {
                if (!ViewCropViewSupport.TryValidateType(activeView, out string reason))
                {
                    error = $"Unsupported active view. {reason}";
                    return new List<View>();
                }

                return new List<View> { activeView };
            }

            IList<ViewCropTargetViewItem> items = ViewCropTargetViewCollector.Collect(doc, activeView.Id);
            var selectionWindow = new ViewCropTargetViewsWindow(items);
            new WindowInteropHelper(selectionWindow)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            bool? selectionResult = selectionWindow.ShowDialog();
            if (selectionResult != true)
            {
                return null;
            }

            IList<ElementId> selectedIds = selectionWindow.SelectedViewIds ?? new List<ElementId>();
            var views = new List<View>();
            foreach (ElementId id in selectedIds)
            {
                View view = doc.GetElement(id) as View;
                if (view != null)
                    views.Add(view);
            }

            views = views
                .GroupBy(v => ElementIdHelper.GetIntegerValue(v.Id))
                .Select(g => g.First())
                .ToList();

            if (views.Count == 0)
            {
                error = "No views selected.";
            }

            return views;
        }

        internal static Result ToCommandResult(ViewCropBatchResult batch)
        {
            if (batch == null)
                return Result.Cancelled;

            if (batch.UpdatedCount > 0)
                return Result.Succeeded;

            return batch.FailedCount > 0 ? Result.Failed : Result.Cancelled;
        }

        /// <summary>
        /// Skill safety-gate: confirm before changing crop on more than one view.
        /// Single-view runs (active view only) need no extra confirmation - the user already
        /// explicitly picked that view.
        /// </summary>
        internal static bool ConfirmBulkRun(string commandTitle, int viewCount, string operationLabel)
        {
            if (viewCount <= 1)
                return true;

            string message =
                $"This will modify the {operationLabel} on {viewCount} views.\n" +
                "This change can be undone with a single Ctrl+Z. Continue?";

            return DialogHelper.ShowYesNo(commandTitle, message);
        }

        private static void RunIntegratedAnnotationCrop(
            Document doc,
            ViewCropBatchResult batch,
            ViewCropSettings settings,
            string commandTitle)
        {
            try
            {
                var successfullyCropped = new List<View>();
                foreach (var result in batch.Items)
                {
                    if (result.State == ViewCropResultState.Updated)
                    {
                        View v = doc.GetElement(result.ViewId) as View;
                        if (v != null)
                            successfullyCropped.Add(v);
                    }
                }

                if (successfullyCropped.Count == 0)
                    return;

                var annotationSettings = new ViewCropAnnotationSettings
                {
                    OffsetMm = settings.AnnotationOffsetMm
                };
                var annotationService = new ViewCropAnnotationService(doc, annotationSettings);
                annotationService.Process(successfullyCropped, "AJ Tools - Integrated Annotation Crop");
            }
            catch (Exception annotationEx)
            {
                string annotationLog = ViewCropReportPresenter.TryWriteErrorLog(
                    "ViewCrop_AnnotationFallback",
                    commandTitle + " (Annotation Fallback)",
                    settings.ExtentSource.ToString(),
                    annotationEx);
                DialogHelper.ShowError(
                    commandTitle,
                    $"View cropping succeeded, but integrated annotation cropping failed.\n{annotationEx.Message}\n\nLog: {annotationLog}");
            }
        }

        private static void ShowDiagnosticsIfAny(
            ExternalCommandData commandData,
            string commandTitle,
            ViewCropBatchResult batch)
        {
            var sbDiag = new System.Text.StringBuilder();
            foreach (var item in batch.Items)
            {
                if (item.State == ViewCropResultState.Updated && !string.IsNullOrWhiteSpace(item.DiagnosticReport))
                {
                    sbDiag.AppendLine($"### VIEW: {item.ViewName} ({item.ViewTypeName}) ###");
                    sbDiag.AppendLine(item.DiagnosticReport);
                    sbDiag.AppendLine();
                    sbDiag.AppendLine();
                }
            }

            string diagReport = sbDiag.ToString().Trim();
            if (string.IsNullOrWhiteSpace(diagReport))
                return;

            try
            {
                var diagWindow = new ViewCropDiagnosticsWindow(diagReport);
                new WindowInteropHelper(diagWindow)
                {
                    Owner = commandData.Application.MainWindowHandle
                };
                diagWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                // Diagnostics report is a "nice to have" - if the WPF window fails, fall back to clipboard.
                try
                {
                    System.Windows.Clipboard.SetText(diagReport);
                    DialogHelper.ShowInfo(
                        commandTitle + " - Diagnostics",
                        "Diagnostics report could not be shown in a window and was copied to the clipboard instead.");
                }
                catch
                {
                    DialogHelper.ShowError(
                        commandTitle + " - Diagnostics",
                        $"Diagnostics report could not be displayed.\n{ex.Message}");
                }
            }
        }
    }
}
