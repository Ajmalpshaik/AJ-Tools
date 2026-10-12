// Tool Name: Set Link Workset - Settings
// Description: Persists last-used workset name for the Set Link Workset tool.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2026-10-12
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

        /// <summary>
        /// Saves the workset name for next time. Returns false, with the reason in
        /// <paramref name="error"/>, when it could not be written - the caller reports it rather than
        /// the failure passing silently.
        /// </summary>
        internal static bool SaveLastWorksetName(string worksetName, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(worksetName))
                return true;

            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, worksetName);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string GetConfigPath() => AppDataConfigStore.GetPath(SettingsFileName);
    }
}
