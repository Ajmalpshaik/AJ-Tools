#region Metadata
/*
 * Tool Name     : Dimensioning (shared)
 * File Name     : DimensionRunReport.cs
 * Purpose       : Counts what a dimension run created, skipped and failed, groups the reasons, and
 *                 builds the plain-language summary shown when the run finishes.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API (ElementId), AJTools.Utils (ElementIdHelper)
 *
 * Input         : Record* calls from the dimension services.
 * Output        : Counters plus BuildSummary() text. The SERVICE returns it; the COMMAND shows it -
 *                 shared code never raises its own dialog.
 *
 * Notes         :
 * - Replaces DuctReferenceDimensionReport, whose BuildSummary() was written but never called, so a
 *   batch run finished silently and skipped work was invisible. Reporting is on by default now.
 * - Skip reasons are aggregated by text, so a run over hundreds of elements produces a short list
 *   rather than one line per element.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release, replacing DuctReferenceDimensionReport.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.Dimensioning
{
    /// <summary>
    /// Accumulates the outcome of one dimension run and formats it for the user.
    /// </summary>
    internal sealed class DimensionRunReport
    {
        private const int MaxFailedItemsListed = 15;

        private readonly Dictionary<string, int> _skipReasons = new Dictionary<string, int>();
        private readonly List<FailedItem> _failedItems = new List<FailedItem>();
        private readonly List<string> _notes = new List<string>();

        internal DimensionRunReport(string title)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Dimension Report" : title;
        }

        internal string Title { get; }

        /// <summary>Dimension elements actually placed in the model.</summary>
        internal int DimensionsCreated { get; private set; }

        /// <summary>Elements (runs, grids, levels) covered by those dimensions.</summary>
        internal int ElementsCovered { get; private set; }

        /// <summary>Views processed, for the multi-view modes.</summary>
        internal int ViewsProcessed { get; private set; }

        /// <summary>Everything deliberately not dimensioned.</summary>
        internal int SkippedCount { get; private set; }

        /// <summary>Attempts Revit itself rejected.</summary>
        internal int FailedCount
        {
            get { return _failedItems.Count; }
        }

        /// <summary>True when the run did anything worth reporting.</summary>
        internal bool HasActivity
        {
            get { return DimensionsCreated > 0 || SkippedCount > 0 || _failedItems.Count > 0; }
        }

        /// <summary>
        /// True when something was left out. A run that dimensioned everything it was asked to needs no
        /// popup - the dimensions themselves are the result. Only unfinished work is worth interrupting for.
        /// </summary>
        internal bool HasUnfinishedWork
        {
            get { return SkippedCount > 0 || _failedItems.Count > 0; }
        }

        internal void RecordViewProcessed()
        {
            ViewsProcessed++;
        }

        internal void RecordCreated(int elementsCovered = 1)
        {
            DimensionsCreated++;
            if (elementsCovered > 0)
                ElementsCovered += elementsCovered;
        }

        internal void RecordSkipped(string reason)
        {
            SkippedCount++;
            AddReason(reason);
        }

        internal void RecordSkipped(string reason, int count)
        {
            if (count <= 0)
                return;

            SkippedCount += count;
            AddReason(reason, count);
        }

        internal void RecordFailed(ElementId elementId, string reason)
        {
            _failedItems.Add(new FailedItem(elementId, reason));
            AddReason(reason);
        }

        /// <summary>A free-text line added verbatim to the top of the summary (e.g. which links were read).</summary>
        internal void AddNote(string note)
        {
            if (!string.IsNullOrWhiteSpace(note) && !_notes.Contains(note))
                _notes.Add(note);
        }

        /// <summary>
        /// The plain-language summary. Kept short: totals, then reasons, then a capped list of
        /// individual failures with their element ids so they can be found in the model.
        /// </summary>
        internal string BuildSummary()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Dimensions created: " + DimensionsCreated);

            if (ElementsCovered > 0)
                sb.AppendLine("Elements dimensioned: " + ElementsCovered);

            if (ViewsProcessed > 1)
                sb.AppendLine("Views processed: " + ViewsProcessed);

            if (SkippedCount > 0)
                sb.AppendLine("Skipped: " + SkippedCount);

            if (_notes.Count > 0)
            {
                sb.AppendLine();
                foreach (string note in _notes)
                    sb.AppendLine(note);
            }

            if (_skipReasons.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Why things were skipped:");
                foreach (KeyValuePair<string, int> item in _skipReasons
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key))
                {
                    sb.AppendLine("- " + item.Key + ": " + item.Value);
                }
            }

            if (_failedItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Could not be dimensioned:");

                foreach (FailedItem item in _failedItems.Take(MaxFailedItemsListed))
                    sb.AppendLine("- Element " + ElementIdHelper.ToReportString(item.ElementId) + ": " + item.Reason);

                int remaining = _failedItems.Count - MaxFailedItemsListed;
                if (remaining > 0)
                    sb.AppendLine("- ... and " + remaining + " more.");
            }

            return sb.ToString().TrimEnd();
        }

        private void AddReason(string reason, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Unknown reason";

            if (_skipReasons.ContainsKey(reason))
                _skipReasons[reason] += count;
            else
                _skipReasons[reason] = count;
        }

        private sealed class FailedItem
        {
            internal FailedItem(ElementId elementId, string reason)
            {
                ElementId = elementId;
                Reason = string.IsNullOrWhiteSpace(reason) ? "Unknown reason" : reason;
            }

            internal ElementId ElementId { get; }
            internal string Reason { get; }
        }
    }
}
