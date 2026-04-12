// Tool Name: View Crop Batch Result
// Description: Result models and summary helpers for batch crop processing.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-08
// Revit Version: 2020

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace AJTools.Models.ViewCrop
{
    /// <summary>
    /// Holds the processing result for a single view.
    /// </summary>
    internal sealed class ViewCropViewResult
    {
        internal ViewCropViewResult(ElementId viewId, string viewName, string viewTypeName)
        {
            ViewId = viewId;
            ViewName = viewName ?? string.Empty;
            ViewTypeName = viewTypeName ?? string.Empty;
            State = ViewCropResultState.Skipped;
            Reason = string.Empty;
        }

        internal ElementId ViewId { get; }

        internal string ViewName { get; }

        internal string ViewTypeName { get; }

        internal ViewCropResultState State { get; private set; }

        internal string Reason { get; private set; }

        internal void MarkUpdated(string reason = null)
        {
            State = ViewCropResultState.Updated;
            Reason = reason ?? string.Empty;
        }

        internal void MarkSkipped(string reason)
        {
            State = ViewCropResultState.Skipped;
            Reason = string.IsNullOrWhiteSpace(reason) ? "Skipped." : reason.Trim();
        }

        internal void MarkFailed(string reason)
        {
            State = ViewCropResultState.Failed;
            Reason = string.IsNullOrWhiteSpace(reason) ? "Failed." : reason.Trim();
        }
    }

    /// <summary>
    /// Aggregates per-view outcomes and builds summary strings.
    /// </summary>
    internal sealed class ViewCropBatchResult
    {
        private readonly List<ViewCropViewResult> _items = new List<ViewCropViewResult>();

        internal IReadOnlyList<ViewCropViewResult> Items => _items;

        internal int TotalSelected => _items.Count;

        internal int UpdatedCount => _items.Count(i => i.State == ViewCropResultState.Updated);

        internal int SkippedCount => _items.Count(i => i.State == ViewCropResultState.Skipped);

        internal int FailedCount => _items.Count(i => i.State == ViewCropResultState.Failed);

        internal void Add(ViewCropViewResult item)
        {
            if (item != null)
                _items.Add(item);
        }

        internal string BuildMainSummary()
        {
            return $"Total selected: {TotalSelected}\n"
                 + $"Updated successfully: {UpdatedCount}\n"
                 + $"Skipped: {SkippedCount}\n"
                 + $"Failed: {FailedCount}";
        }

        internal string BuildReasonSummary()
        {
            var grouped = _items
                .Where(i => i.State != ViewCropResultState.Updated && !string.IsNullOrWhiteSpace(i.Reason))
                .GroupBy(i => i.Reason)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .ToList();

            if (grouped.Count == 0)
                return "No skip/failure reasons.";

            var sb = new StringBuilder();
            foreach (var group in grouped)
            {
                sb.AppendLine($"{group.Count()} - {group.Key}");
            }

            return sb.ToString().TrimEnd();
        }

        internal string BuildDetailedLines(int maxLines)
        {
            if (_items.Count == 0)
                return "No views processed.";

            int limit = maxLines <= 0 ? _items.Count : maxLines;
            var sb = new StringBuilder();
            int count = 0;
            foreach (ViewCropViewResult item in _items)
            {
                if (count >= limit)
                    break;

                string state = item.State.ToString();
                string line = string.IsNullOrWhiteSpace(item.Reason)
                    ? $"{state}: {item.ViewName} ({item.ViewTypeName})"
                    : $"{state}: {item.ViewName} ({item.ViewTypeName}) - {item.Reason}";

                sb.AppendLine(line);
                count++;
            }

            if (_items.Count > limit)
            {
                sb.Append($"... {_items.Count - limit} more result(s). ");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
