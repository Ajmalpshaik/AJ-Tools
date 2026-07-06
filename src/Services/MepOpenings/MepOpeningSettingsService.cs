#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningSettingsService.cs
 * Purpose       : Loads and saves Opening settings in the user's AppData folder.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-03
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Newtonsoft.Json, AJTools.Models.MepOpenings
 *
 * Input         : MepOpeningSettings object.
 * Output        : JSON settings file in user AppData.
 *
 * Notes         :
 * - Settings are user-level and do not write to the Revit model.
 * - Invalid or missing settings fall back to safe defaults.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;
using AJTools.Models.MepOpenings;
using Newtonsoft.Json;

namespace AJTools.Services.MepOpenings
{
    internal static class MepOpeningSettingsService
    {
        private const string SettingsFileName = "settings.json";

        public static MepOpeningSettings Load()
        {
            string path = GetSettingsFilePath();
            if (!File.Exists(path))
            {
                return MepOpeningSettings.CreateDefault();
            }

            try
            {
                string json = File.ReadAllText(path);
                var settings = JsonConvert.DeserializeObject<MepOpeningSettings>(json);
                if (settings == null)
                {
                    return MepOpeningSettings.CreateDefault();
                }

                settings.Normalize();
                return settings;
            }
            catch
            {
                return MepOpeningSettings.CreateDefault();
            }
        }

        public static bool Save(MepOpeningSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (settings == null)
            {
                errorMessage = "No settings to save.";
                return false;
            }

            try
            {
                settings.Normalize();
                string path = GetSettingsFilePath();
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

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

        public static string GetSettingsFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "AJ Tools", "Opening", SettingsFileName);
        }
    }
}
