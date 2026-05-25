// ==================================================
// Tool Name    : Section Mark Visibility
// Purpose      : Persists Section Mark Visibility settings on disk.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-24
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : System, System.IO
// ==================================================

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
        private const string SettingsFolderName = "AJTools";
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

                        case "keepallplacedsections":
                            if (bool.TryParse(val, out bool keepPlaced))
                                settings.KeepAllPlacedSections = keepPlaced;
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
                    writer.WriteLine($"KeepAllPlacedSections={settings.KeepAllPlacedSections}");
                    writer.WriteLine($"SheetNumbers={sheetNumbersCsv}");
                }
            }
            catch
            {
                // Ignore settings write failures.
            }
        }

        private static string GetConfigPath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, SettingsFolderName, SettingsFileName);
        }
    }
}
