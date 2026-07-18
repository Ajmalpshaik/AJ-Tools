// Tool Name: Arrange Tags - Settings
// Description: Persists default vertical spacing (mm) for Arrange Tags tools.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-07
// Revit Version: 2020
// Dependencies: System, System.IO

using System;
using System.Globalization;
using System.IO;

namespace AJTools.Utils
{
    /// <summary>
    /// Stores and retrieves tagging settings used by Arrange Tags workflows.
    /// </summary>
    internal static class TagArrangeSettings
    {
        internal const string ConfigKey = "tag_spacing_mm";
        internal const double DefaultTagSpacingMm = 12.0;

        private const string SettingsFileName = "Tagging.config";

        internal static double GetTagSpacingMm()
        {
            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path))
                    return DefaultTagSpacingMm;

                string raw = File.ReadAllText(path)?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                    return DefaultTagSpacingMm;

                // Expected format: tag_spacing_mm=<value>
                string[] parts = raw.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    return DefaultTagSpacingMm;

                if (!string.Equals(parts[0].Trim(), ConfigKey, StringComparison.OrdinalIgnoreCase))
                    return DefaultTagSpacingMm;

                if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double spacing))
                    return DefaultTagSpacingMm;

                if (spacing <= 0)
                    return DefaultTagSpacingMm;

                return spacing;
            }
            catch
            {
                return DefaultTagSpacingMm;
            }
        }

        internal static void SaveTagSpacingMm(double spacingMm)
        {
            if (spacingMm <= 0)
                return;

            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string value = spacingMm.ToString("0.###", CultureInfo.InvariantCulture);
                File.WriteAllText(path, $"{ConfigKey}={value}");
            }
            catch
            {
                // Ignore settings write failures.
            }
        }

        private static string GetConfigPath() => AppDataConfigStore.GetPath(SettingsFileName);
    }
}
