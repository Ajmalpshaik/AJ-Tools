using Newtonsoft.Json;
using System.IO;
using System.Reflection;

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

        private static string GetSettingsPath(string fileName)
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(folder, fileName);
        }

        public static RevisionCloudSettings Load()
        {
            try
            {
                string primaryPath = GetSettingsPath(SettingsFileName);
                if (File.Exists(primaryPath))
                {
                    string json = File.ReadAllText(primaryPath);
                    var settings = JsonConvert.DeserializeObject<RevisionCloudSettings>(json);
                    if (settings != null)
                        return settings;
                }

                string legacyPath = GetSettingsPath(LegacySettingsFileName);
                if (File.Exists(legacyPath))
                {
                    string json = File.ReadAllText(legacyPath);
                    var settings = JsonConvert.DeserializeObject<RevisionCloudSettings>(json);
                    if (settings != null)
                        return settings;
                }
            }
            catch
            {
                // Return defaults on any error.
            }
            return new RevisionCloudSettings();
        }

        public void Save()
        {
            try
            {
                string path = GetSettingsPath(SettingsFileName);
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Silently fail - settings are non-critical.
            }
        }

        public double OffsetDistanceFeet => OffsetDistanceMm * 0.00328084;
    }
}
