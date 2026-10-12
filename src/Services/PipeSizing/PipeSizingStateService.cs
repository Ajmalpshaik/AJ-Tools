#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeSizingStateService.cs
 * Purpose       : Loads and saves last-used Pipe Sizing settings.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2026-07-01
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Newtonsoft.Json, AJTools.Models.PipeSizing
 *
 * Input         : PipeSizingState object.
 * Output        : JSON state file in user AppData.
 *
 * Notes         :
 * - Replaces the bundle-local state.json of Ajmal's earlier pipe sizing script with a per-user AppData file.
 * - JSON field names remain compatible with the state shape of Ajmal's earlier script.
 * - Save reports whether it worked (and why not), so the window can tell the user instead of
 *   silently losing their rows; it still never throws into the sizing workflow.
 *
 * Changelog     :
 * v1.0.1 (2026-10-12) - Save now returns a success flag plus the reason on failure (it used to swallow
 *                       every error silently, so a failed save lost the user's rows with no word).
 * v1.0.0 (2026-07-01) - Initial C# port for Pipe Sizing.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;
using AJTools.Models.PipeSizing;
using Newtonsoft.Json;

namespace AJTools.Services.PipeSizing
{
    internal static class PipeSizingStateService
    {
        private const string StateFileName = "state.json";

        public static PipeSizingState Load()
        {
            string path = GetStateFilePath();
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<PipeSizingState>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves the last-used settings. Returns false (with the reason in errorMessage) when the file
        /// could not be written. Never throws: state persistence must not interrupt the sizing workflow.
        /// </summary>
        public static bool Save(PipeSizingState state, out string errorMessage)
        {
            errorMessage = null;

            if (state == null)
            {
                return true;
            }

            try
            {
                string path = GetStateFilePath();
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static string GetStateFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "AJ Tools", "Pipe Sizing", StateFileName);
        }
    }
}
