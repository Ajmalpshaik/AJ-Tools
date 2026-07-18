#region Metadata
/*
 * Tool Name     : Section Mark Visibility
 * File Name     : SectionMarkVisibilityConfigStore.cs
 * Purpose       : Persists/loads the user's run scope and sheet-number filter between sessions.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-05-24
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System, System.IO
 *
 * Input         : Settings object / on-disk config file
 * Output        : Persisted config file / restored settings
 *
 * Notes         :
 * - Only the run scope and sheet-number filter are persisted (these are restored in the UI).
 *   The mode buttons (Keep All Placed / Unhide All) are per-run actions and are not persisted.
 *
 * Changelog     :
 * v1.0.0 (2026-05-24) - Initial release.
 * v1.2.0 (2026-06-30) - Cleanup pass: persist only the values restored by the UI; metadata block.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AJTools.Models.SectionMarkVisibility;

namespace AJTools.Utils
{
    /// <summary>
    /// Handles persistent disk-based storage and retrieval of Section Mark Visibility settings.
    /// </summary>
    internal static class SectionMarkVisibilityConfigStore
    {
        private const string SettingsFileName = "SectionMarkVisibility.config";

        /// <summary>
        /// Loads the settings from disk. Returns defaults if file does not exist or errors occur.
        /// </summary>
        internal static SectionMarkVisibilitySettings Load()
        {
            var settings = new SectionMarkVisibilitySettings();
            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path))
                    return settings;

                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;

                    string key = parts[0].Trim().ToLowerInvariant();
                    string val = parts[1].Trim();

                    switch (key)
                    {
                        case "applytoactiveviewonly":
                            if (bool.TryParse(val, out bool activeOnly))
                                settings.ApplyToActiveViewOnly = activeOnly;
                            break;

                        case "sheetnumbers":
                            settings.SheetNumbers = val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                            break;
                    }
                }
            }
            catch
            {
                // Fall back silently to default settings on error.
            }
            return settings;
        }

        /// <summary>
        /// Saves the given settings to disk.
        /// </summary>
        internal static void Save(SectionMarkVisibilitySettings settings)
        {
            if (settings == null)
                return;

            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string sheetNumbersCsv = string.Join(",", settings.SheetNumbers.Select(s => s.Trim()));

                using (var writer = new StreamWriter(path, false))
                {
                    writer.WriteLine($"ApplyToActiveViewOnly={settings.ApplyToActiveViewOnly}");
                    writer.WriteLine($"SheetNumbers={sheetNumbersCsv}");
                }
            }
            catch
            {
                // Ignore settings write failures.
            }
        }

        private static string GetConfigPath() => AppDataConfigStore.GetPath(SettingsFileName);
    }
}
