#region Metadata
/*
 * Tool Name     : Dimensioning (shared)
 * File Name     : DimensionSettingsService.cs
 * Purpose       : Loads and saves the Automatic Dimension and Auto MEP Dimension settings as JSON in
 *                 the user's AppData folder.
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
 * Dependencies  : Newtonsoft.Json, AJTools.Models.Dimensioning
 *
 * Input         : Settings objects from the settings windows.
 * Output        : JSON settings files under %APPDATA%\AJ Tools\Dimensioning\.
 *
 * Notes         :
 * - Settings are user-level and never written to the Revit model - no transaction is involved.
 * - Every read and write is guarded: a missing, corrupt or unreadable file falls back to defaults
 *   rather than blocking the tool. Normalize() runs on both load and save.
 * - Follows the MepOpeningSettingsService pattern (JSON under %APPDATA%\AJ Tools\<Tool>\).
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;
using AJTools.Models.Dimensioning;
using Newtonsoft.Json;

namespace AJTools.Services.Dimensioning
{
    /// <summary>
    /// Shared AppData path and JSON plumbing for the dimension settings stores.
    /// </summary>
    internal static class DimensionSettingsPaths
    {
        private const string RootFolderName = "AJ Tools";
        private const string ToolFolderName = "Dimensioning";

        internal static string GetSettingsFilePath(string fileName)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, RootFolderName, ToolFolderName, fileName);
        }

        /// <summary>
        /// Reads and deserializes a settings file. Returns default(T) (null) on any failure so the
        /// caller can substitute its own defaults.
        /// </summary>
        internal static T ReadOrNull<T>(string fileName) where T : class
        {
            try
            {
                string path = GetSettingsFilePath(fileName);
                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Serializes and writes a settings file. Never throws.</summary>
        internal static bool Write<T>(string fileName, T settings, out string errorMessage) where T : class
        {
            errorMessage = string.Empty;

            if (settings == null)
            {
                errorMessage = "No settings to save.";
                return false;
            }

            try
            {
                string path = GetSettingsFilePath(fileName);
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(folder))
                    Directory.CreateDirectory(folder);

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// Load/save for the Automatic Dimension (grids / levels) settings.
    /// </summary>
    internal static class AutoDatumDimensionSettingsService
    {
        private const string SettingsFileName = "auto-datum-dimension.json";

        public static AutoDatumDimensionSettings Load()
        {
            AutoDatumDimensionSettings settings =
                DimensionSettingsPaths.ReadOrNull<AutoDatumDimensionSettings>(SettingsFileName);

            if (settings == null)
                return AutoDatumDimensionSettings.CreateDefault();

            settings.Normalize();
            return settings;
        }

        public static bool Save(AutoDatumDimensionSettings settings, out string errorMessage)
        {
            settings?.Normalize();
            return DimensionSettingsPaths.Write(SettingsFileName, settings, out errorMessage);
        }

        public static string GetSettingsFilePath()
        {
            return DimensionSettingsPaths.GetSettingsFilePath(SettingsFileName);
        }
    }

    /// <summary>
    /// Load/save for the Auto MEP Dimension settings.
    /// </summary>
    internal static class MepDimensionSettingsService
    {
        private const string SettingsFileName = "auto-mep-dimension.json";

        public static MepDimensionSettings Load()
        {
            MepDimensionSettings settings =
                DimensionSettingsPaths.ReadOrNull<MepDimensionSettings>(SettingsFileName);

            if (settings == null)
                return MepDimensionSettings.CreateDefault();

            settings.Normalize();
            return settings;
        }

        public static bool Save(MepDimensionSettings settings, out string errorMessage)
        {
            settings?.Normalize();
            return DimensionSettingsPaths.Write(SettingsFileName, settings, out errorMessage);
        }

        public static string GetSettingsFilePath()
        {
            return DimensionSettingsPaths.GetSettingsFilePath(SettingsFileName);
        }
    }
}
