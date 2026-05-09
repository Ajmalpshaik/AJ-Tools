// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Persists last-used Apply Graphics settings between tool launches.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-05-09
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Newtonsoft.Json
// Input        : Serializable Apply Graphics memory state.
// Output       : Last-used settings JSON in the user's application data folder.
// Notes        : Memory persistence is best effort and does not block the Revit command.
// Changelog    : v1.4.4 - Added persistent memory for Graphics Tool settings.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using System.IO;
using AJTools.Models.GraphicsTools;
using Newtonsoft.Json;

namespace AJTools.Services.GraphicsTools
{
    /// <summary>
    /// Saves and loads last-used Graphics Tool UI settings.
    /// </summary>
    internal static class GraphicsOverrideMemoryService
    {
        private const int CurrentVersion = 1;
        private const string CompanyFolderName = "AJ Tools";
        private const string ToolFolderName = "GraphicsTools";
        private const string FileName = "graphics_override_memory.json";

        public static GraphicsOverrideMemoryState Load()
        {
            try
            {
                string path = GetMemoryFilePath();
                if (!File.Exists(path))
                {
                    return null;
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                GraphicsOverrideMemoryState state =
                    JsonConvert.DeserializeObject<GraphicsOverrideMemoryState>(json);
                if (state == null || state.Version > CurrentVersion)
                {
                    return null;
                }

                EnsureCollections(state);
                return state;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(GraphicsOverrideMemoryState state)
        {
            if (state == null)
            {
                return;
            }

            try
            {
                state.Version = CurrentVersion;
                EnsureCollections(state);

                string path = GetMemoryFilePath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Last-used memory must never block the Revit command.
            }
        }

        private static string GetMemoryFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, CompanyFolderName, ToolFolderName, FileName);
        }

        private static void EnsureCollections(GraphicsOverrideMemoryState state)
        {
            if (state.SelectedCategoryIntegerIds == null)
            {
                state.SelectedCategoryIntegerIds = new System.Collections.Generic.List<int>();
            }

            if (state.SelectedCategoryNames == null)
            {
                state.SelectedCategoryNames = new System.Collections.Generic.List<string>();
            }
        }
    }
}
