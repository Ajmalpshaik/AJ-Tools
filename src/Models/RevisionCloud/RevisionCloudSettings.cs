using System;
using System.IO;
using System.Reflection;
using AJTools.Utils;
using Newtonsoft.Json;

namespace AJTools.Models.RevisionCloud
{
    public class RevisionCloudSettings
    {
        /// <summary>
        /// Offset distance from element bounding box in mm.
        /// </summary>
        public double OffsetDistanceMm { get; set; } = 50.0;

        private const string SettingsFileName = "RevisionCloudByElementsSettings.json";
        private const string LegacySettingsFileName = "RevisionCloudByShapeSettings.json";

        /// <summary>
        /// %APPDATA%\AJTools\ - the same per-user folder the other tools keep their settings in. The
        /// setting used to sit next to the add-in DLL, and every deploy installs the add-in into a fresh
        /// folder, so the saved offset was lost on each update.
        /// </summary>
        private static string GetSettingsPath()
        {
            return AppDataConfigStore.GetPath(SettingsFileName);
        }

        /// <summary>
        /// The old location beside the add-in DLL. Only read to carry an existing value over once.
        /// </summary>
        private static string GetOldAddinFolderPath(string fileName)
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, fileName);
        }

        public static RevisionCloudSettings Load()
        {
            try
            {
                RevisionCloudSettings settings = TryRead(GetSettingsPath());
                if (settings != null)
                    return settings;

                // One-time carry-over: nothing usable in AppData yet, so take the value saved beside
                // the add-in DLL by an older version (current file name first, then the older name)
                // and write it to AppData so it survives the next update.
                RevisionCloudSettings migrated =
                    TryRead(GetOldAddinFolderPath(SettingsFileName)) ??
                    TryRead(GetOldAddinFolderPath(LegacySettingsFileName));
                if (migrated != null)
                {
                    migrated.Save(out _);
                    return migrated;
                }
            }
            catch
            {
                // Return defaults on any error.
            }

            return new RevisionCloudSettings();
        }

        /// <summary>
        /// Writes the settings to %APPDATA%\AJTools\. Returns false, with the reason, when the file
        /// could not be written, so the caller can say so instead of closing as if it had saved.
        /// </summary>
        public bool Save(out string error)
        {
            error = null;

            try
            {
                string path = GetSettingsPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public double OffsetDistanceFeet => OffsetDistanceMm * 0.00328084;

        private static RevisionCloudSettings TryRead(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return null;

                return JsonConvert.DeserializeObject<RevisionCloudSettings>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }
    }
}
