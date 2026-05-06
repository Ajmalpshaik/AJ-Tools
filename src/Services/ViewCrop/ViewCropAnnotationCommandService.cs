// Tool Name: View Crop Annotation Command Service
// Description: Shared command flow for annotation crop tools.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-11
// Revit Version: 2020

using System;
using System.Collections.Generic;
using System.IO;
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

                var service = new ViewCropAnnotationService(doc, settings);
                var batch = service.Process(targetViews, $"AJ Tools - {commandTitle}");

                try
                {
                    ShowBatchSummaryIfNeeded(commandTitle, batch);
                }
                catch (Exception summaryEx)
                {
                    string summaryLog = TryWriteErrorLog(commandTitle, summaryEx);
                    DialogHelper.ShowError(
                        commandTitle,
                        $"Processing finished, but summary dialog failed.\n{summaryEx.Message}\n\nLog: {summaryLog}");
                }

                return ViewCropCommandService.ToCommandResult(batch);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                string logPath = TryWriteErrorLog(commandTitle, ex);
                DialogHelper.ShowError(
                    commandTitle,
                    $"{ex.Message}\n\nDetails log:\n{logPath}");
                return Result.Failed;
            }
        }

        private static void ShowBatchSummaryIfNeeded(string title, ViewCropBatchResult batch)
        {
            if (batch == null || (batch.SkippedCount == 0 && batch.FailedCount == 0))
                return;

            var dialog = new TaskDialog(title)
            {
                MainInstruction = "Annotation crop processing completed.",
                MainContent = batch.BuildMainSummary(),
                ExpandedContent = "Reason summary:\n"
                    + batch.BuildReasonSummary()
                    + "\n\nDetailed results:\n"
                    + batch.BuildDetailedLines(250),
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            dialog.Show();
        }

        private static string TryWriteErrorLog(string commandTitle, Exception ex)
        {
            try
            {
                string fileName = $"AJTools_AnnotationCrop_Error_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                string path = Path.Combine(Path.GetTempPath(), fileName);
                string body =
                    $"Command: {commandTitle}{Environment.NewLine}" +
                    $"Timestamp: {DateTime.Now:O}{Environment.NewLine}{Environment.NewLine}" +
                    ex;
                File.WriteAllText(path, body);
                return path;
            }
            catch
            {
                return "Could not write log file.";
            }
        }
    }
}
