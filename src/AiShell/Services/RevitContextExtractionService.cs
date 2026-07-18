#region Metadata
/*
 * Tool Name     : C#
 * File Name     : RevitContextExtractionService.cs
 * Purpose       : Reads a small, safe summary of the live Revit session (document, active view,
 *                 current selection with category/type/level) and hands it to the AI as context,
 *                 so generated code can reasonably assume what the user is looking at.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-01-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : None (reads the currently active UIDocument/selection)
 * Output        : Plain-text context summary string, capped at AiShellConstants.MaxContextPayloadChars
 *
 * Notes         :
 * - Every Revit API read is individually try/caught so one odd element or missing level never
 *   drops the whole context — it just omits that one detail.
 * - Per-element detail (type/level) is capped at AiShellConstants.MaxContextElementDetails so a
 *   huge selection doesn't blow up the AI request size; the category-count summary still covers
 *   the full selection either way.
 * - "No active document." and "No elements currently selected." are checked verbatim by
 *   AiShellViewModel.GenerateCodeAsync to decide whether to inject context — do not change
 *   this exact wording without updating that check too.
 *
 * Changelog     :
 * v1.0.0 (2026-01-01) - Initial release (selection + category counts only).
 * v1.1.0 (2026-07-01) - Added document title, active view info, per-element type/level detail,
 *                       payload size cap, and per-field exception handling.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using AJTools.AiShell.Helpers;

using AJTools.Utils;
namespace AJTools.AiShell.Services
{
    public class RevitContextExtractionService : IExternalEventHandler
    {
        private readonly ExternalEvent _externalEvent;
        private readonly object _lock = new object();
        private TaskCompletionSource<string> _tcs;

        public RevitContextExtractionService()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public Task<string> ExtractContextAsync()
        {
            lock (_lock)
            {
                _tcs = new TaskCompletionSource<string>();
            }

            _externalEvent.Raise();
            return _tcs.Task;
        }

        public void Execute(UIApplication app)
        {
            TaskCompletionSource<string> tcs;
            lock (_lock)
            {
                tcs = _tcs;
            }

            try
            {
                var uidoc = app.ActiveUIDocument;
                if (uidoc == null || uidoc.Document == null)
                {
                    tcs.TrySetResult("No active document.");
                    return;
                }

                var doc = uidoc.Document;
                var sb = new StringBuilder();

                AppendDocumentInfo(sb, doc);
                AppendActiveViewInfo(sb, doc);

                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 0)
                {
                    sb.AppendLine("No elements currently selected.");
                }
                else
                {
                    AppendSelectionInfo(sb, doc, selectedIds);
                }

                tcs.TrySetResult(TruncateToPayloadLimit(sb.ToString().Trim()));
            }
            catch (Exception ex)
            {
                tcs.TrySetResult($"Failed to extract context: {ex.Message}");
            }
        }

        private static void AppendDocumentInfo(StringBuilder sb, Document doc)
        {
            try
            {
                string title = string.IsNullOrWhiteSpace(doc.Title) ? "(untitled)" : doc.Title;
                sb.AppendLine($"Document: {title}{(doc.IsWorkshared ? " (workshared)" : string.Empty)}");
            }
            catch
            {
                // Document title is informational only — safe to skip if unavailable.
            }
        }

        private static void AppendActiveViewInfo(StringBuilder sb, Document doc)
        {
            try
            {
                var view = doc.ActiveView;
                if (view != null)
                {
                    sb.AppendLine($"Active View: \"{view.Name}\" ({view.ViewType}), Id {view.Id.IntValue()}");
                }
            }
            catch
            {
                // Active view can be unavailable in some document states — skip rather than fail.
            }
        }

        private static void AppendSelectionInfo(StringBuilder sb, Document doc, ICollection<ElementId> selectedIds)
        {
            var categoryCounts = new Dictionary<string, int>();
            var detailLines = new List<string>();

            foreach (var id in selectedIds)
            {
                Element elem;
                try
                {
                    elem = doc.GetElement(id);
                }
                catch
                {
                    continue;
                }

                if (elem == null) continue;

                string catName = elem.Category?.Name ?? "Uncategorized";
                categoryCounts[catName] = categoryCounts.TryGetValue(catName, out var existing) ? existing + 1 : 1;

                if (detailLines.Count < AiShellConstants.MaxContextElementDetails)
                {
                    string detail = BuildElementDetailLine(doc, elem, catName);
                    if (detail != null) detailLines.Add(detail);
                }
            }

            string summary = string.Join(", ", categoryCounts.Select(kv => $"{kv.Value} {kv.Key}"));
            string idList = string.Join(", ", selectedIds.Select(id => id.IntValue()));
            sb.AppendLine($"Selected Elements: {summary}. IDs: [{idList}]");

            if (detailLines.Count == 0) return;

            sb.AppendLine(selectedIds.Count > detailLines.Count
                ? $"Detail for the first {detailLines.Count} of {selectedIds.Count} selected elements:"
                : "Selected element detail:");

            foreach (var line in detailLines)
            {
                sb.AppendLine(line);
            }
        }

        private static string BuildElementDetailLine(Document doc, Element elem, string categoryName)
        {
            try
            {
                string typeName = null;
                var typeId = elem.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    typeName = (doc.GetElement(typeId))?.Name;
                }

                string levelName = GetLevelName(doc, elem);

                var parts = new List<string> { $"Id {elem.Id.IntValue()}: {categoryName}" };
                if (!string.IsNullOrWhiteSpace(typeName)) parts.Add($"Type: {typeName}");
                if (!string.IsNullOrWhiteSpace(levelName)) parts.Add($"Level: {levelName}");

                return "- " + string.Join(", ", parts);
            }
            catch
            {
                // A single odd element (e.g. an in-place family with unusual type binding) should
                // never stop the rest of the context from being built.
                return null;
            }
        }

        private static string GetLevelName(Document doc, Element elem)
        {
            try
            {
                var levelId = elem.LevelId;
                if (levelId != null && levelId != ElementId.InvalidElementId)
                {
                    return (doc.GetElement(levelId) as Level)?.Name;
                }
            }
            catch
            {
                // Not every element type exposes a meaningful LevelId — that's expected, not an error.
            }
            return null;
        }

        private static string TruncateToPayloadLimit(string context)
        {
            if (context.Length <= AiShellConstants.MaxContextPayloadChars) return context;
            return context.Substring(0, AiShellConstants.MaxContextPayloadChars) + "\n...(context truncated)";
        }

        public string GetName() => "AJ AI Context Extraction";
    }
}
