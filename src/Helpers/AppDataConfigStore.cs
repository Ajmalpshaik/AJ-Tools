// Tool Name: AppData Config Store
// Description: Shared %APPDATA%\AJTools\<file> path builder for the tool-specific config stores
//              (LinkWorksetSettings, SectionMarkVisibilityConfigStore, TagArrangeSettings,
//              ViewCropConfigStore), which previously each hand-rolled an identical GetConfigPath().
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-07-17
// Revit Version: 2020
// Dependencies: System, System.IO

using System;
using System.IO;

namespace AJTools.Utils
{
    internal static class AppDataConfigStore
    {
        private const string SettingsFolderName = "AJTools";

        /// <summary>
        /// Returns the full path for a config file under %APPDATA%\AJTools\.
        /// </summary>
        internal static string GetPath(string fileName)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, SettingsFolderName, fileName);
        }
    }
}
