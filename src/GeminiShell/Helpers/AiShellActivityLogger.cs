#region Metadata
/*
 * Tool Name     : AJ AI (Gemini Shell)
 * File Name     : AiShellActivityLogger.cs
 * Purpose       : Minimal local activity log for AJ AI runs — provider, script name, success,
 *                 retry count, and a truncated error/prompt summary. Never logs API keys or full
 *                 model/element data.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : Run metadata (provider, prompt preview, success flag, attempt count, script name, error)
 * Output        : Appends one line to %AppData%\AJTools\AiShell_Activity.log
 *
 * Notes         :
 * - Logging failures are swallowed — a broken log must never break the tool itself.
 * - Prompt/error text is truncated before writing so the log stays small and readable.
 *
 * Changelog     :
 * v1.0.0 (2026-07-01) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;

namespace AJTools.GeminiShell.Helpers
{
    public static class AiShellActivityLogger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AJTools",
            "AiShell_Activity.log"
        );

        private static readonly object FileLock = new object();

        public static void LogRun(string provider, string promptPreview, bool success, int attempts, string scriptName, string errorSummary)
        {
            try
            {
                lock (FileLock)
                {
                    var dir = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Provider={provider} Script={scriptName ?? "(unsaved)"} " +
                                  $"Success={success} Attempts={attempts} Prompt=\"{Truncate(promptPreview, 80)}\" Error=\"{Truncate(errorSummary, 200)}\"";

                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never break the tool.
            }
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}
