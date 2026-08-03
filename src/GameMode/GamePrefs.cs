#region Metadata
/*
 * Tool Name     : AJ Game Mode (preferences)
 * File Name     : GamePrefs.cs
 * Purpose       : Small persistent game preferences. Currently one: Professional Mode - the gun is
 *                 never shown (Ajmal: presentable in front of a manager / in meetings) while every
 *                 tool keeps working; effects start from the bottom of the view like a laser
 *                 pointer. Saved to %AppData%\AJTools\ajgame-prefs.txt so it survives sessions.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-29
 * Last Updated  : 2026-07-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Changelog     :
 * v1.0.0 (2026-07-29) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;

namespace AJTools.Services.GameMode
{
    /// <summary>Persistent Game Mode preferences (best-effort file IO, never throws).</summary>
    internal static class GamePrefs
    {
        public static bool LoadProfessionalMode()
        {
            try
            {
                string file = GetFilePath();
                if (System.IO.File.Exists(file))
                {
                    foreach (string line in System.IO.File.ReadAllLines(file))
                    {
                        if (line.Trim().Equals("ProfessionalMode=true", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        public static void SaveProfessionalMode(bool on)
        {
            try
            {
                string file = GetFilePath();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
                System.IO.File.WriteAllLines(file, new[] { "ProfessionalMode=" + (on ? "true" : "false") });
            }
            catch (Exception)
            {
            }
        }

        private static string GetFilePath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AJTools",
                "ajgame-prefs.txt");
        }
    }
}
