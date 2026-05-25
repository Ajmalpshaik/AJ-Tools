// Tool Name: View Crop - Settings Configuration Store
// Description: Persists all user settings for the View Crop and integrated Annotation Crop tool on disk.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-05-24
// Target: Revit 2020
// Dependencies: System, System.IO, System.Globalization

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
