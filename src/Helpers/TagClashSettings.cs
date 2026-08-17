#region Metadata
/*
 * Tool Name     : Fix Tag Clash - Settings
 * File Name     : TagClashSettings.cs
 * Purpose       : Persists the shared tagging/clash settings to %APPDATA%\AJTools\TagClash.config.
 *                 Unlike SmartTagSettingsTracker / CreateTagsSettingsTracker (which keep their state
 *                 in a static field and therefore lose it when Revit closes), these settings survive
 *                 a restart - same file-backed approach as TagArrangeSettings, extended to a
 *                 multi-key key=value file so one store can carry every clash/tagging value.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-16
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System, System.IO, Autodesk Revit API (Color only), AJTools.Utils (AppDataConfigStore)
 *
 * Input         : %APPDATA%\AJTools\TagClash.config - one key=value per line.
 * Output        : Strongly-typed settings with safe defaults when the file is missing or damaged.
 *
 * Notes         :
 * - Every "mm" value here is millimetres ON THE PRINTED SHEET, not model millimetres. Multiply by the
 *   view scale to get model size - use PaperMmToFeet on TagViewGeometry, never a raw MM_TO_FEET.
 * - SkipVerticalRuns is deliberately a TAGGING setting, not a clash setting: Smart MEP Tags and
 *   Create Tags both read it so the two tools can never disagree about a vertical pipe again.
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// The colour used to mark tags the fix loop could not separate.
    /// </summary>
    public enum TagClashMarkColour
    {
        Red,
        Green,
        Orange,
        Blue,
        Magenta
    }

    /// <summary>
    /// A snapshot of the shared tagging / clash settings.
    /// </summary>
    public sealed class TagClashSettingsState
    {
        /// <summary>How many fix-and-recheck rounds to run before giving up. 1 - 50.</summary>
        public int FixPasses { get; set; }

        /// <summary>How far a tag may drift from where it started, in mm on the printed sheet.</summary>
        public double MaxDriftMm { get; set; }

        /// <summary>Colour applied to tags still clashing after the last pass.</summary>
        public TagClashMarkColour MarkColour { get; set; }

        /// <summary>Whether to colour the unfixed tags at all (they are always selected and reported).</summary>
        public bool MarkFailures { get; set; }

        /// <summary>Skip vertical ducts, pipes and cable trays when tagging. Read by the tagging tools.</summary>
        public bool SkipVerticalRuns { get; set; }

        /// <summary>Smart MEP Tags: shortest run worth tagging, mm. 0 = no minimum.</summary>
        public double SmartTagMinLengthMm { get; set; }

        /// <summary>Smart MEP Tags: whether the width/height minimums apply at all.</summary>
        public bool SmartTagFilterBySize { get; set; }

        /// <summary>Smart MEP Tags: minimum width, mm. 0 = no minimum. A round section uses its diameter.</summary>
        public double SmartTagMinWidthMm { get; set; }

        /// <summary>Smart MEP Tags: minimum height, mm. 0 = no minimum. A round section uses its diameter.</summary>
        public double SmartTagMinHeightMm { get; set; }

        /// <summary>
        /// Smart MEP Tags: comma-separated BuiltInCategory names whose tags get NO leader. Empty means
        /// every category keeps its leader, which is the default and the old behaviour.
        /// </summary>
        public string SmartTagNoLeaderCategories { get; set; }

        /// <summary>Overlap below this (mm on the sheet) is ignored - matches Smart MEP Tag's tuned 1.5mm.</summary>
        public double ClashToleranceMm { get; set; }

        /// <summary>Two tags closer than this (mm on the sheet) count as clashing - Smart MEP Tag used 5mm.</summary>
        public double MinGapMm { get; set; }

        /// <summary>
        /// When fixing, also try the other side of the host and sliding along it before moving
        /// up/down. Slower per tag, but it only ever runs on the few tags that actually clash.
        /// </summary>
        public bool FullSearch { get; set; }
    }

    /// <summary>
    /// Reads and writes the shared tagging / clash settings file.
    /// </summary>
    internal static class TagClashSettings
    {
        private const string SettingsFileName = "TagClash.config";

        internal const int DefaultFixPasses = 5;
        internal const double DefaultMaxDriftMm = 50.0;
        internal const bool DefaultMarkFailures = true;
        internal const bool DefaultSkipVerticalRuns = true;
        internal const double DefaultClashToleranceMm = 1.5;
        internal const double DefaultMinGapMm = 5.0;
        internal const bool DefaultFullSearch = true;
        internal const TagClashMarkColour DefaultMarkColour = TagClashMarkColour.Red;

        // These four mirror SmartTagSettingsTracker's own defaults and must stay in step with them.
        // MinHeight is 0, not 100, on purpose: the hardcoded filter these replaced only ever tested
        // WIDTH, so a default height minimum would silently stop shallow ducts being tagged the day
        // this shipped. See the tracker for the full reasoning.
        internal const double DefaultSmartTagMinLengthMm = 1000.0;
        internal const bool DefaultSmartTagFilterBySize = true;
        internal const double DefaultSmartTagMinWidthMm = 100.0;
        internal const double DefaultSmartTagMinHeightMm = 0.0;

        // LongRunConfirmThreshold moved to DialogHelper in v1.49.5, alongside the ConfirmLongRun call
        // that uses it. It started here because Fix Tag Clash needed it first, but it is a suite-wide
        // "warn me before Revit goes busy" threshold - seven tools now share it, including Center Room
        // Tags and L-Shape Leader, which have nothing to do with tag clash. Leaving it on this feature's
        // settings class made it read as clash-only.

        internal const int MinFixPasses = 1;
        internal const int MaxFixPasses = 50;
        internal const double MinDriftMm = 1.0;
        internal const double MaxDriftMmLimit = 500.0;

        private const string KeyFixPasses = "fix_passes";
        private const string KeyMaxDrift = "max_drift_mm";
        private const string KeyMarkColour = "mark_colour";
        private const string KeyMarkFailures = "mark_failures";
        private const string KeySkipVertical = "skip_vertical_runs";
        private const string KeyClashTolerance = "clash_tolerance_mm";
        private const string KeyMinGap = "min_gap_mm";
        private const string KeyFullSearch = "full_search";

        // Smart MEP Tag's own size rules live in this file for one reason: it is the only tagging
        // settings store that survives closing Revit. SmartTagSettingsTracker is a static in-memory
        // field, so anything kept there is gone next session - which for a drafting standard like
        // "shortest run worth tagging" would read as the tool forgetting. Same reasoning that put
        // SkipVerticalRuns here in v1.49.2. Categories and priorities stay in the tracker, because
        // those are genuinely per-project.
        private const string KeySmartTagMinLength = "smarttag_min_length_mm";
        private const string KeySmartTagFilterBySize = "smarttag_filter_by_size";
        private const string KeySmartTagMinWidth = "smarttag_min_width_mm";
        private const string KeySmartTagMinHeight = "smarttag_min_height_mm";

        // Stored as the EXCEPTIONS, not as one key per category: the default is "every category keeps
        // its leader", so an absent or empty entry means exactly that, and a category added to the
        // tool later inherits the right default without needing a migration.
        private const string KeySmartTagNoLeader = "smarttag_no_leader_categories";

        /// <summary>
        /// Returns the stored settings, falling back to defaults for anything missing or unreadable.
        /// Never throws - a damaged config file must not stop a tool from running.
        /// </summary>
        internal static TagClashSettingsState Load()
        {
            TagClashSettingsState state = CreateDefaults();

            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path))
                    return state;

                Dictionary<string, string> values = ReadKeyValues(path);

                state.FixPasses = ClampPasses(ReadInt(values, KeyFixPasses, DefaultFixPasses));
                state.MaxDriftMm = ClampDrift(ReadDouble(values, KeyMaxDrift, DefaultMaxDriftMm));
                state.MarkColour = ParseColour(ReadString(values, KeyMarkColour, null), DefaultMarkColour);
                state.MarkFailures = ReadBool(values, KeyMarkFailures, DefaultMarkFailures);
                state.SkipVerticalRuns = ReadBool(values, KeySkipVertical, DefaultSkipVerticalRuns);
                state.ClashToleranceMm = ClampPositive(
                    ReadDouble(values, KeyClashTolerance, DefaultClashToleranceMm), DefaultClashToleranceMm);
                state.MinGapMm = ClampPositive(
                    ReadDouble(values, KeyMinGap, DefaultMinGapMm), DefaultMinGapMm);
                state.FullSearch = ReadBool(values, KeyFullSearch, DefaultFullSearch);

                // 0 is a legitimate value for all three ("no minimum on this axis"), so these are
                // clamped at zero rather than falling back to the default when they read as 0.
                state.SmartTagMinLengthMm = Math.Max(
                    0.0, ReadDouble(values, KeySmartTagMinLength, DefaultSmartTagMinLengthMm));
                state.SmartTagFilterBySize = ReadBool(
                    values, KeySmartTagFilterBySize, DefaultSmartTagFilterBySize);
                state.SmartTagMinWidthMm = Math.Max(
                    0.0, ReadDouble(values, KeySmartTagMinWidth, DefaultSmartTagMinWidthMm));
                state.SmartTagMinHeightMm = Math.Max(
                    0.0, ReadDouble(values, KeySmartTagMinHeight, DefaultSmartTagMinHeightMm));
                state.SmartTagNoLeaderCategories = ReadString(values, KeySmartTagNoLeader, string.Empty);
            }
            catch (Exception)
            {
                return CreateDefaults();
            }

            return state;
        }

        /// <summary>
        /// Writes the settings. Returns false when the file could not be written, so the caller can
        /// tell the user instead of silently losing the change (the mistake TagArrangeSettings made
        /// before its own read-back check was added).
        /// </summary>
        internal static bool Save(TagClashSettingsState state)
        {
            if (state == null)
                return false;

            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                AppendLine(sb, KeyFixPasses, ClampPasses(state.FixPasses).ToString(CultureInfo.InvariantCulture));
                AppendLine(sb, KeyMaxDrift, FormatNumber(ClampDrift(state.MaxDriftMm)));
                AppendLine(sb, KeyMarkColour, state.MarkColour.ToString());
                AppendLine(sb, KeyMarkFailures, state.MarkFailures ? "1" : "0");
                AppendLine(sb, KeySkipVertical, state.SkipVerticalRuns ? "1" : "0");
                AppendLine(sb, KeySmartTagMinLength, FormatNumber(state.SmartTagMinLengthMm));
                AppendLine(sb, KeySmartTagFilterBySize, state.SmartTagFilterBySize ? "1" : "0");
                AppendLine(sb, KeySmartTagMinWidth, FormatNumber(state.SmartTagMinWidthMm));
                AppendLine(sb, KeySmartTagMinHeight, FormatNumber(state.SmartTagMinHeightMm));
                AppendLine(sb, KeySmartTagNoLeader, state.SmartTagNoLeaderCategories ?? string.Empty);
                AppendLine(sb, KeyClashTolerance, FormatNumber(ClampPositive(state.ClashToleranceMm, DefaultClashToleranceMm)));
                AppendLine(sb, KeyMinGap, FormatNumber(ClampPositive(state.MinGapMm, DefaultMinGapMm)));
                AppendLine(sb, KeyFullSearch, state.FullSearch ? "1" : "0");

                File.WriteAllText(path, sb.ToString());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True when the tagging tools should skip vertical ducts, pipes and cable trays.
        /// Single question, one answer, read by every tagging tool.
        /// </summary>
        internal static bool ShouldSkipVerticalRuns()
        {
            return Load().SkipVerticalRuns;
        }

        /// <summary>
        /// Stores the skip-vertical-runs rule on its own, without disturbing anything else in the file.
        /// Returns false when the value could not be written, so the caller can say so.
        /// </summary>
        /// <remarks>
        /// Lives here because THREE settings windows now touch this one file - Smart MEP Tag Settings
        /// and Create Tags Settings both show the tick box, and Fix Tag Clash Settings carries the value
        /// through untouched. Read-modify-write is what keeps them from overwriting each other, and it
        /// belongs in one place rather than being copied into each command. The write is confirmed by
        /// reading back, because a settings write that silently fails is worse than one that reports.
        /// </remarks>
        /// <summary>
        /// Saves Smart MEP Tags' size rules, leaving every other value in the file untouched.
        /// </summary>
        /// <remarks>
        /// Read-modify-write-and-verify, the same shape as TrySetSkipVerticalRuns: Save rewrites the
        /// WHOLE file, so loading first is what stops this from wiping the clash settings, and reading
        /// back is what turns a failed write into a reported failure instead of a silently lost
        /// setting. Three windows now write this file.
        /// </remarks>
        /// <returns>True when the values were stored and read back unchanged.</returns>
        internal static bool TrySetSmartTagSizeSettings(
            double minLengthMm,
            bool filterBySize,
            double minWidthMm,
            double minHeightMm,
            string noLeaderCategories)
        {
            TagClashSettingsState current = Load();

            current.SmartTagMinLengthMm = Math.Max(0.0, minLengthMm);
            current.SmartTagFilterBySize = filterBySize;
            current.SmartTagMinWidthMm = Math.Max(0.0, minWidthMm);
            current.SmartTagMinHeightMm = Math.Max(0.0, minHeightMm);
            current.SmartTagNoLeaderCategories = noLeaderCategories ?? string.Empty;

            if (!Save(current))
                return false;

            TagClashSettingsState verify = Load();

            return Math.Abs(verify.SmartTagMinLengthMm - current.SmartTagMinLengthMm) < 0.001
                && verify.SmartTagFilterBySize == current.SmartTagFilterBySize
                && Math.Abs(verify.SmartTagMinWidthMm - current.SmartTagMinWidthMm) < 0.001
                && Math.Abs(verify.SmartTagMinHeightMm - current.SmartTagMinHeightMm) < 0.001
                && string.Equals(
                    verify.SmartTagNoLeaderCategories ?? string.Empty,
                    current.SmartTagNoLeaderCategories ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TrySetSkipVerticalRuns(bool skipVerticalRuns)
        {
            TagClashSettingsState current = Load();
            if (current.SkipVerticalRuns == skipVerticalRuns)
                return true;

            current.SkipVerticalRuns = skipVerticalRuns;

            return Save(current) && Load().SkipVerticalRuns == skipVerticalRuns;
        }

        /// <summary>Maps the stored colour name to a Revit colour.</summary>
        internal static Color ToRevitColour(TagClashMarkColour colour)
        {
            switch (colour)
            {
                case TagClashMarkColour.Green:
                    return new Color(0, 176, 80);
                case TagClashMarkColour.Orange:
                    return new Color(255, 140, 0);
                case TagClashMarkColour.Blue:
                    return new Color(0, 112, 224);
                case TagClashMarkColour.Magenta:
                    return new Color(224, 0, 160);
                case TagClashMarkColour.Red:
                default:
                    return new Color(230, 0, 0);
            }
        }

        internal static TagClashSettingsState CreateDefaults()
        {
            return new TagClashSettingsState
            {
                FixPasses = DefaultFixPasses,
                MaxDriftMm = DefaultMaxDriftMm,
                MarkColour = DefaultMarkColour,
                MarkFailures = DefaultMarkFailures,
                SkipVerticalRuns = DefaultSkipVerticalRuns,
                ClashToleranceMm = DefaultClashToleranceMm,
                MinGapMm = DefaultMinGapMm,
                FullSearch = DefaultFullSearch,
                SmartTagMinLengthMm = DefaultSmartTagMinLengthMm,
                SmartTagFilterBySize = DefaultSmartTagFilterBySize,
                SmartTagMinWidthMm = DefaultSmartTagMinWidthMm,
                SmartTagMinHeightMm = DefaultSmartTagMinHeightMm,
                SmartTagNoLeaderCategories = string.Empty
            };
        }

        internal static int ClampPasses(int passes)
        {
            if (passes < MinFixPasses)
                return MinFixPasses;
            if (passes > MaxFixPasses)
                return MaxFixPasses;
            return passes;
        }

        internal static double ClampDrift(double driftMm)
        {
            if (driftMm < MinDriftMm)
                return MinDriftMm;
            if (driftMm > MaxDriftMmLimit)
                return MaxDriftMmLimit;
            return driftMm;
        }

        internal static TagClashMarkColour ParseColour(string text, TagClashMarkColour fallback)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            foreach (TagClashMarkColour candidate in (TagClashMarkColour[])Enum.GetValues(typeof(TagClashMarkColour)))
            {
                if (string.Equals(candidate.ToString(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return fallback;
        }

        private static double ClampPositive(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                return fallback;
            return value;
        }

        private static void AppendLine(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append('=').Append(value).Append(Environment.NewLine);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, string> ReadKeyValues(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawLine in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                string line = rawLine.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                string key = parts[0].Trim();
                if (key.Length == 0)
                    continue;

                values[key] = parts[1].Trim();
            }

            return values;
        }

        private static string ReadString(Dictionary<string, string> values, string key, string fallback)
        {
            string raw;
            if (values != null && values.TryGetValue(key, out raw) && !string.IsNullOrWhiteSpace(raw))
                return raw;
            return fallback;
        }

        private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
        {
            string raw = ReadString(values, key, null);
            int parsed;
            if (raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return fallback;
        }

        private static double ReadDouble(Dictionary<string, string> values, string key, double fallback)
        {
            string raw = ReadString(values, key, null);
            double parsed;
            if (raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return fallback;
        }

        private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string raw = ReadString(values, key, null);
            if (raw == null)
                return fallback;

            if (string.Equals(raw, "1", StringComparison.Ordinal)
                || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(raw, "0", StringComparison.Ordinal)
                || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fallback;
        }

        private static string GetConfigPath()
        {
            return AppDataConfigStore.GetPath(SettingsFileName);
        }
    }
}
