// ==================================================
// Tool Name    : View Crop
// Purpose      : Coordinates View Crop command UI, target view selection, execution, and reporting.
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
using System.IO;
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
            ViewCropExtentSource source,
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

                var service = new ViewCropExtentsService(doc, settings, source);
                var batch = service.Process(targetViews, $"AJ Tools - {commandTitle}");

                try
                {
                    ShowBatchSummaryIfNeeded(commandTitle, batch);
                }
                catch (Exception summaryEx)
                {
                    string summaryLog = TryWriteErrorLog(commandTitle, source, summaryEx);
                    DialogHelper.ShowError(
                        commandTitle,
                        $"Processing finished, but summary dialog failed.\n{summaryEx.Message}\n\nLog: {summaryLog}");
                }

                return ToCommandResult(batch);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                string logPath = TryWriteErrorLog(commandTitle, source, ex);
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
                .GroupBy(v => v.Id.IntegerValue)
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

        private static void ShowBatchSummaryIfNeeded(string title, ViewCropBatchResult batch)
        {
            if (batch == null || (batch.SkippedCount == 0 && batch.FailedCount == 0))
                return;

            var dialog = new TaskDialog(title)
            {
                MainInstruction = "View crop processing completed.",
                MainContent = batch.BuildMainSummary(),
                ExpandedContent = "Reason summary:\n"
                    + batch.BuildReasonSummary()
                    + "\n\nDetailed results:\n"
                    + batch.BuildDetailedLines(250),
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            dialog.Show();
        }

        private static string TryWriteErrorLog(string commandTitle, ViewCropExtentSource source, Exception ex)
        {
            try
            {
                string fileName = $"AJTools_ViewCrop_Error_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                string path = Path.Combine(Path.GetTempPath(), fileName);
                string body =
                    $"Command: {commandTitle}{Environment.NewLine}" +
                    $"Source: {source}{Environment.NewLine}" +
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
