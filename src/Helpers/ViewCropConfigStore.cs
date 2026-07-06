#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropConfigStore.cs
 * Purpose       : Persists View Crop and integrated annotation settings on disk.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-05-24
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System.IO, System.Globalization
 *
 * Input         : ViewCropSettings (Save) or none (Load).
 * Output        : Loaded ViewCropSettings or persisted text file.
 *
 * Notes         :
 * - File location: %APPDATA%\AJTools\ViewCrop.config (key=value lines, InvariantCulture).
 * - Read and write failures are swallowed - settings revert to defaults rather than crashing the tool.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Metadata refresh to skill standard.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Globalization;
using System.IO;
using AJTools.Models.ViewCrop;

namespace AJTools.Utils
{
    /// <summary>
    /// Handles persistent disk-based storage and retrieval of View Crop and integrated Annotation settings.
    /// </summary>
    internal static class ViewCropConfigStore
    {
        private const string SettingsFolderName = "AJTools";
        private const string SettingsFileName = "ViewCrop.config";

        internal static ViewCropSettings Load()
        {
            var settings = new ViewCropSettings();
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
                        case "marginmm":
                            if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double margin))
                                settings.MarginMm = margin;
                            break;
                        case "includerevitlinks":
                            if (bool.TryParse(val, out bool links))
                                settings.IncludeRevitLinks = links;
                            break;
                        case "ignorehiddencategories":
                            if (bool.TryParse(val, out bool ignoreHidden))
                                settings.IgnoreHiddenCategories = ignoreHidden;
                            break;
                        case "rectangularcroponly":
                            if (bool.TryParse(val, out bool rectOnly))
                                settings.RectangularCropOnly = rectOnly;
                            break;
                        case "includedatums":
                            if (bool.TryParse(val, out bool includeDatums))
                                settings.IncludeDatums = includeDatums;
                            break;
                        case "applyannotationcrop":
                            if (bool.TryParse(val, out bool applyAnnotation))
                                settings.ApplyAnnotationCrop = applyAnnotation;
                            break;
                        case "annotationoffsetmm":
                            if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double offset))
                                settings.AnnotationOffsetMm = offset;
                            break;
                        case "showdiagnostics":
                            if (bool.TryParse(val, out bool showDiag))
                                settings.ShowDiagnostics = showDiag;
                            break;
                        case "includecoordinationmodels":
                            if (bool.TryParse(val, out bool includeCoord))
                                settings.IncludeCoordinationModels = includeCoord;
                            break;
                        case "extentsource":
                            if (Enum.TryParse(val, true, out ViewCropExtentSource extentSource)
                                && Enum.IsDefined(typeof(ViewCropExtentSource), extentSource))
                                settings.ExtentSource = extentSource;
                            break;
                    }
                }
            }
            catch
            {
                // Return default settings if reading fails.
            }
            return settings;
        }

        internal static void Save(ViewCropSettings settings)
        {
            if (settings == null)
                return;

            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var writer = new StreamWriter(path, false))
                {
                    writer.WriteLine($"MarginMm={settings.MarginMm.ToString("0.###", CultureInfo.InvariantCulture)}");
                    writer.WriteLine($"IncludeRevitLinks={settings.IncludeRevitLinks}");
                    writer.WriteLine($"IgnoreHiddenCategories={settings.IgnoreHiddenCategories}");
                    writer.WriteLine($"RectangularCropOnly={settings.RectangularCropOnly}");
                    writer.WriteLine($"IncludeDatums={settings.IncludeDatums}");
                    writer.WriteLine($"ApplyAnnotationCrop={settings.ApplyAnnotationCrop}");
                    writer.WriteLine($"AnnotationOffsetMm={settings.AnnotationOffsetMm.ToString("0.###", CultureInfo.InvariantCulture)}");
                    writer.WriteLine($"ShowDiagnostics={settings.ShowDiagnostics}");
                    writer.WriteLine($"IncludeCoordinationModels={settings.IncludeCoordinationModels}");
                    writer.WriteLine($"ExtentSource={settings.ExtentSource}");
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
