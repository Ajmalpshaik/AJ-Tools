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
    /// Lightweight diagnostics for Smart MEP Tag runs: appends one line per run (and the DEBUG
    /// build's geometry self-check result) to a log file in the user's temp folder.
    /// </summary>
    /// <remarks>
    /// Nothing in AJ Tools reads this file - it is there for whoever investigates a bad run - so it
    /// is capped rather than allowed to grow for ever: past about 1 MB the current file is kept as
    /// one ".old" copy and a fresh one is started, so at most about 2 MB sits in the temp folder.
    /// The in-session totals that used to be printed as "SESSION TELEMETRY" at the bottom of the
    /// user's report were developer output, and went when that block was removed from the report.
    /// </remarks>
    internal static class SmartTagTelemetryTracker
    {
        private static readonly object Sync = new object();
        private static readonly string TelemetryLogPath =
            Path.Combine(Path.GetTempPath(), "AJTools_SmartTag_Telemetry.log");
        private static readonly string PreviousTelemetryLogPath =
            Path.Combine(Path.GetTempPath(), "AJTools_SmartTag_Telemetry.old.log");

        // About 3,000 runs at the current line length.
        private const long MaxLogBytes = 1024 * 1024;

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
                RotateIfTooLarge();
                File.AppendAllText(TelemetryLogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Telemetry must never block tool execution.
            }
        }

        /// <summary>
        /// Keeps the log bounded: once it passes MaxLogBytes it becomes the single ".old" copy
        /// (replacing any earlier one) and the next line starts a new file.
        /// </summary>
        private static void RotateIfTooLarge()
        {
            var current = new FileInfo(TelemetryLogPath);
            if (!current.Exists || current.Length < MaxLogBytes)
                return;

            if (File.Exists(PreviousTelemetryLogPath))
                File.Delete(PreviousTelemetryLogPath);

            File.Move(TelemetryLogPath, PreviousTelemetryLogPath);
        }
    }
}
