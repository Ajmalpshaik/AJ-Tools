using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AJTools.Models.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Lightweight session telemetry for Smart MEP Tag runs.
    /// Stores counters in memory and appends one-line records to a temp log file.
    /// </summary>
    internal static class SmartTagTelemetryTracker
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<TagSkipReason, int> SessionSkipTotals = new Dictionary<TagSkipReason, int>();
        private static readonly string TelemetryLogPath =
            Path.Combine(Path.GetTempPath(), "AJTools_SmartTag_Telemetry.log");

        private static int _sessionRuns;
        private static int _sessionAnalysed;
        private static int _sessionTagged;

        internal static void RecordRun(
            PreFlightResult preflight,
            SmartTagSettingsState settingsState,
            IList<TagPlacementResult> results,
            int candidatesAfterFilter,
            int candidatesAfterTagTypeResolution)
        {
            if (preflight == null || results == null)
                return;

            int analysed = results.Count;
            int tagged = results.Count(r => r != null && r.Success);
            Dictionary<TagSkipReason, int> skipCounts = BuildSkipCounts(results);
            string skipText = FormatSkipCounts(skipCounts);
            string offsetsText = BuildOffsetSummary(settingsState);
            string viewName = preflight.ActiveView != null ? preflight.ActiveView.Name : "Unknown";

            string line = string.Format(
                "{0:u} | view=\"{1}\" | type={2} | scale=1:{3} | analysed={4} | candidates(filter)={5} | candidates(tagType)={6} | tagged={7} | skips={8} | offsets(mm)={9}",
                DateTime.UtcNow,
                viewName.Replace("\"", "'"),
                preflight.ViewType,
                preflight.ViewScale,
                analysed,
                candidatesAfterFilter,
                candidatesAfterTagTypeResolution,
                tagged,
                skipText,
                offsetsText);

            lock (Sync)
            {
                _sessionRuns++;
                _sessionAnalysed += analysed;
                _sessionTagged += tagged;

                foreach (KeyValuePair<TagSkipReason, int> kvp in skipCounts)
                {
                    int existing;
                    SessionSkipTotals.TryGetValue(kvp.Key, out existing);
                    SessionSkipTotals[kvp.Key] = existing + kvp.Value;
                }

                AppendLineSafe(line);
            }
        }

        internal static void RecordDiagnostic(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            lock (Sync)
            {
                AppendLineSafe(string.Format("{0:u} | diagnostic | {1}", DateTime.UtcNow, message));
            }
        }

        internal static string GetSessionSummary()
        {
            lock (Sync)
            {
                if (_sessionRuns <= 0)
                    return "No session telemetry available yet.";

                double successRate = _sessionAnalysed > 0
                    ? (_sessionTagged * 100.0 / _sessionAnalysed)
                    : 0;

                return string.Format(
                    "Runs: {0}\nAnalysed: {1}\nTagged: {2} ({3:F1}%)\nSkip Totals: {4}",
                    _sessionRuns,
                    _sessionAnalysed,
                    _sessionTagged,
                    successRate,
                    FormatSkipCounts(SessionSkipTotals));
            }
        }

        private static Dictionary<TagSkipReason, int> BuildSkipCounts(IList<TagPlacementResult> results)
        {
            var counts = new Dictionary<TagSkipReason, int>();
            if (results == null)
                return counts;

            foreach (TagPlacementResult result in results)
            {
                if (result == null || result.Success || result.SkipReason == TagSkipReason.None)
                    continue;

                int existing;
                counts.TryGetValue(result.SkipReason, out existing);
                counts[result.SkipReason] = existing + 1;
            }

            return counts;
        }

        private static string FormatSkipCounts(IDictionary<TagSkipReason, int> counts)
        {
            if (counts == null || counts.Count == 0)
                return "none";

            return string.Join(", ",
                counts
                    .OrderBy(kvp => kvp.Key.ToString(), StringComparer.Ordinal)
                    .Select(kvp => string.Format("{0}:{1}", kvp.Key, kvp.Value)));
        }

        private static string BuildOffsetSummary(SmartTagSettingsState settingsState)
        {
            if (settingsState == null)
                return "default";

            var parts = new List<string>();
            foreach (var category in SmartTagSettingsTracker.SupportedCategories)
            {
                if (!SmartTagSettingsTracker.IsCategoryEnabled(settingsState, category))
                    continue;

                double offsetInternal = SmartTagSettingsTracker.ResolveOffsetInternal(settingsState, category);
                double offsetMm = offsetInternal / Constants.MM_TO_FEET;
                parts.Add(string.Format("{0}:{1:F0}", SmartTagSettingsTracker.GetCategoryLabel(category), offsetMm));
            }

            if (parts.Count == 0)
                return "none-enabled";

            return string.Join("; ", parts);
        }

        private static void AppendLineSafe(string line)
        {
            try
            {
                File.AppendAllText(TelemetryLogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Telemetry must never block tool execution.
            }
        }
    }
}
