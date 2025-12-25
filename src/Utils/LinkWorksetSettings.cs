// Tool Name: Set Link Workset - Settings
// Description: Persists last-used workset name for the Set Link Workset tool.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-23
// Revit Version: 2020
// Dependencies: System, System.IO

using System;
using System.IO;

namespace AJTools.Utils
{
    /// <summary>
    /// Stores and retrieves the last-used workset name for link workset assignment.
    /// </summary>
    internal static class LinkWorksetSettings
    {
        private const string DefaultWorksetName = "Linked Models";
        private const string SettingsFolderName = "AJTools";
        private const string SettingsFileName = "SetLinkWorkset.config";

        internal static string GetLastWorksetName()
        {
            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path))
                    return DefaultWorksetName;

                string text = File.ReadAllText(path);
                if (string.IsNullOrEmpty(text))
                    return DefaultWorksetName;

                return text.TrimEnd('\r', '\n');
            }
            catch
            {
                return DefaultWorksetName;
            }
        }

        internal static void SaveLastWorksetName(string worksetName)
        {
            if (string.IsNullOrEmpty(worksetName))
                return;

            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, worksetName);
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
