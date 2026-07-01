#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropReportPresenter.cs
 * Purpose       : Shared summary and error-log presentation for View Crop and Annotation Crop services.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-06-27
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : ViewCropBatchResult, tool title and label, exception (for log).
 * Output        : Summary TaskDialog, error log file path.
 *
 * Notes         :
 * - Replaces the two duplicated ShowBatchSummaryIfNeeded / TryWriteErrorLog methods that
 *   previously lived in ViewCropCommandService and ViewCropAnnotationCommandService.
 * - Summary is only shown when there are skipped or failed views (skill rule:
 *   no normal success popup).
 *
 * Changelog     :
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
        internal static void ShowSummaryIfNeeded(string title, string operationLabel, ViewCropBatchResult batch)
        {
            if (batch == null || (batch.SkippedCount == 0 && batch.FailedCount == 0))
                return;

            var dialog = new TaskDialog(title)
            {
                MainInstruction = $"{operationLabel} processing completed.",
                MainContent = batch.BuildMainSummary(),
                ExpandedContent = "Reason summary:\n"
                    + batch.BuildReasonSummary()
                    + "\n\nDetailed results:\n"
                    + batch.BuildDetailedLines(250),
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
