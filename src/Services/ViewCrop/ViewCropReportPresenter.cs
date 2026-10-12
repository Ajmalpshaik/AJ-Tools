#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropReportPresenter.cs
 * Purpose       : Shared summary and error-log presentation for View Crop and Annotation Crop services.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.1
 *
 * Created Date  : 2026-06-27
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : ViewCropBatchResult (plus the integrated annotation crop's result, when it ran),
 *                 tool title and label, exception (for log).
 * Output        : Summary TaskDialog, error log file path.
 *
 * Notes         :
 * - Replaces the two duplicated ShowBatchSummaryIfNeeded / TryWriteErrorLog methods that
 *   previously lived in ViewCropCommandService and ViewCropAnnotationCommandService.
 * - Summary is only shown when there are skipped or failed views - in the crop OR in the
 *   integrated annotation crop (skill rule: no normal success popup, and no silent failure).
 *
 * Changelog     :
 * v1.1.1 (2026-10-12) - Optional annotation-crop result: its skipped/failed views now open the summary
 *                       and are listed in it. Before, View Crop discarded that result unreported.
 * v1.1.0 (2026-06-27) - Initial release. Extracted shared summary + log logic.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.IO;
using Autodesk.Revit.UI;
using AJTools.Models.ViewCrop;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Centralizes the post-run summary dialog and the error-log writer for View Crop tools.
    /// </summary>
    internal static class ViewCropReportPresenter
    {
        /// <param name="annotationBatch">
        /// The integrated annotation crop's result when it ran after the crop, otherwise null. Its skipped
        /// or failed views also open the summary, since nothing on screen shows that they were left out.
        /// </param>
        internal static void ShowSummaryIfNeeded(
            string title,
            string operationLabel,
            ViewCropBatchResult batch,
            ViewCropBatchResult annotationBatch = null)
        {
            if (batch == null)
                return;

            bool hasAnnotationResults = annotationBatch != null && annotationBatch.TotalSelected > 0;
            bool annotationLeftSomethingOut = hasAnnotationResults
                && (annotationBatch.SkippedCount > 0 || annotationBatch.FailedCount > 0);

            if (batch.SkippedCount == 0 && batch.FailedCount == 0 && !annotationLeftSomethingOut)
                return;

            string mainContent = batch.BuildMainSummary();
            string expandedContent = "Reason summary:\n"
                + batch.BuildReasonSummary()
                + "\n\nDetailed results:\n"
                + batch.BuildDetailedLines(250);

            if (hasAnnotationResults)
            {
                mainContent += "\n\nAnnotation crop (on the views cropped above):\n"
                    + $"Updated successfully: {annotationBatch.UpdatedCount}\n"
                    + $"Skipped: {annotationBatch.SkippedCount}\n"
                    + $"Failed: {annotationBatch.FailedCount}";

                expandedContent += "\n\nAnnotation crop reason summary:\n"
                    + annotationBatch.BuildReasonSummary()
                    + "\n\nAnnotation crop detailed results:\n"
                    + annotationBatch.BuildDetailedLines(250);
            }

            var dialog = new TaskDialog(title)
            {
                MainInstruction = $"{operationLabel} processing completed.",
                MainContent = mainContent,
                ExpandedContent = expandedContent,
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            dialog.Show();
        }

        internal static string TryWriteErrorLog(string logPrefix, string commandTitle, string sourceLabel, Exception ex)
        {
            try
            {
                string fileName = $"AJTools_{logPrefix}_Error_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                string path = Path.Combine(Path.GetTempPath(), fileName);
                var body = new System.Text.StringBuilder();
                body.AppendLine($"Command: {commandTitle}");
                if (!string.IsNullOrWhiteSpace(sourceLabel))
                    body.AppendLine($"Source: {sourceLabel}");
                body.AppendLine($"Timestamp: {DateTime.Now:O}");
                body.AppendLine();
                body.Append(ex);

                File.WriteAllText(path, body.ToString());
                return path;
            }
            catch
            {
                return "Could not write log file.";
            }
        }
    }
}
