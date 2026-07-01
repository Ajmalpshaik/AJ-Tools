#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropAnnotationCommandService.cs
 * Purpose       : Coordinates annotation crop UI, target view selection, execution, and reporting.
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
 * Dependencies  : Autodesk Revit API, WPF
 *
 * Input         : Active Revit document, active or selected target views (plan views), annotation crop offset.
 * Output        : Annotation crop enabled with the user-supplied equal offset on each supported view.
 *
 * Notes         :
 * - Tool scope: Active View OR a user-selected batch of plan views.
 * - Skips unsupported, template, scope-box-controlled, and view-template-locked views.
 * - Bulk-edit confirmation is shown when more than one view is targeted.
 * - Summary and error-log handling delegated to ViewCropReportPresenter.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Refactor/audit pass: bulk-edit confirmation, shared report presenter, metadata, version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.ViewCrop;
using AJTools.UI.ViewCrop;
using AJTools.Utils;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Handles UI flow, target view selection, processing invocation, and summary reporting
    /// for annotation crop operations.
    /// </summary>
    internal static class ViewCropAnnotationCommandService
    {
        internal static Result Execute(
            ExternalCommandData commandData,
            string commandTitle,
            ref string message)
        {
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

                var settingsTracker = new ViewCropAnnotationSettingsTracker(doc);
                var optionsWindow = new ViewCropAnnotationOptionsWindow(commandTitle, settingsTracker.LastSettings);
                new WindowInteropHelper(optionsWindow)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

                bool? optionsResult = optionsWindow.ShowDialog();
                if (optionsResult != true)
                    return Result.Cancelled;

                ViewCropAnnotationSettings settings = optionsWindow.SelectedSettings ?? new ViewCropAnnotationSettings();
                settingsTracker.Save(settings);

                IList<View> targetViews = ViewCropCommandService.ResolveTargetViews(
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

                if (!ViewCropCommandService.ConfirmBulkRun(commandTitle, targetViews.Count, "annotation crop"))
                    return Result.Cancelled;

                var service = new ViewCropAnnotationService(doc, settings);
                var batch = service.Process(targetViews, $"AJ Tools - {commandTitle}");

                try
                {
                    ViewCropReportPresenter.ShowSummaryIfNeeded(commandTitle, "Annotation crop", batch);
                }
                catch (Exception summaryEx)
                {
                    string summaryLog = ViewCropReportPresenter.TryWriteErrorLog("AnnotationCrop", commandTitle, null, summaryEx);
                    DialogHelper.ShowError(
                        commandTitle,
                        $"Processing finished, but summary dialog failed.\n{summaryEx.Message}\n\nLog: {summaryLog}");
                }

                return ViewCropCommandService.ToCommandResult(batch);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                string logPath = ViewCropReportPresenter.TryWriteErrorLog("AnnotationCrop", commandTitle, null, ex);
                DialogHelper.ShowError(
                    commandTitle,
                    $"{ex.Message}\n\nDetails log:\n{logPath}");
                return Result.Failed;
            }
        }
    }
}
