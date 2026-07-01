#region Metadata
/*
 * Tool Name     : Apply Graphics
 * File Name     : GraphicsOverrideMemoryService.cs
 * Purpose       : Persists and restores the last-used Apply Graphics UI settings between tool launches.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-05-09
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Newtonsoft.Json
 *
 * Input         : Serializable Apply Graphics memory state.
 * Output        : Last-used settings JSON in the user's application data folder.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Memory persistence is best effort and never blocks or fails the Revit command.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Added persistent memory for Graphics Tool settings.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
