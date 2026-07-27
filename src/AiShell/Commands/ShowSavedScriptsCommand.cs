#region Metadata
/*
 * Tool Name     : C#
 * File Name     : ShowSavedScriptsCommand.cs
 * Purpose       : Standalone "Saved Scripts" ribbon button - opens a window listing every .cs file
 *                 actually sitting in the configured Scripts Folder (AiShellConfig.ScriptsFolderPath),
 *                 with Pin/Run per row. Moved out of the C# pane's "Saved Scripts History" expander
 *                 per Ajmal's request, to sit alongside "Run Pinned" as its own always-present tool
 *                 that works whether or not the C# pane is open.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.1
 *
 * Created Date  : 2026-07-21
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : AiShellConfig, RoslynService, GeneratedCodeSafetyValidator, SavedScriptsWindow
 *
 * Input         : Ribbon button click (no arguments - reads AiShellConfig.ScriptsFolderPath).
 * Output        : Modal window; "▶ Run" inside it runs the selected script against the active
 *                 document the same way RunPinnedScriptCommand does.
 *
 * Notes         :
 * - Same reasoning as RunPinnedScriptCommand: this is an IExternalCommand invoked directly by
 *   Revit's own command dispatch (already on the API thread), so no ExternalEvent is needed, and it
 *   deliberately does not depend on the WPF ViewModel layer (AiShellViewModel) - it must work even
 *   if the C# pane was never opened this session.
 * - The saved-script-header parsing (Prompt:/Provider: comment lines) is kept as its own small copy
 *   here rather than shared with AiShellViewModel or RunPinnedScriptCommand, matching that file's
 *   own precedent.
 *
 * Changelog     :
 * v1.2.0 (2026-07-21) - Reverted v1.1.0 below - Saved Scripts now only ever runs from the Run Pinned
 *                       split button's dropdown and never becomes its default face (RibbonManager.cs
 *                       v1.12.0, IsSynchronizedWithCurrentItem = false), so Execute() no longer needs
 *                       to set CurrentButton itself.
 * v1.1.0 (2026-07-21) - Now sets the ribbon split button's CurrentButton to itself as the first thing
 *                       Execute() does (App.RunPinnedSplitButton / SavedScriptsButton) - Saved Scripts
 *                       moved into the dropdown of the Run Pinned split button (RibbonManager.cs
 *                       v1.11.0) instead of being its own top-level button.
 * v1.0.0 (2026-07-21) - Initial release.
 * v1.2.1 (2026-07-28) - Audit: window is now owned by the Revit main window (WindowInteropHelper), so it cannot drop behind Revit.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.AiShell.Configuration;
using AJTools.AiShell.Models;
using AJTools.AiShell.Views;

namespace AJTools.AiShell.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowSavedScriptsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var config = AiShellConfig.Load();
            var window = new SavedScriptsWindow(config, commandData);
            new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            window.ShowDialog();
            return Result.Succeeded;
        }

        /// <summary>Scans the scripts folder fresh every call - the folder can change on disk between
        /// window opens (scripts saved from the C# pane, copied in manually, deleted, etc.), so this
        /// never trusts a cached list.</summary>
        public static System.Collections.Generic.List<HistoryItem> ScanFolder(string folderPath)
        {
            var results = new System.Collections.Generic.List<HistoryItem>();
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return results;
            }

            foreach (var file in Directory.GetFiles(folderPath, "*.cs"))
            {
                var rawContent = File.ReadAllText(file);
                var (prompt, provider) = ParseSavedScriptHeader(rawContent);

                results.Add(new HistoryItem
                {
                    FilePath = file,
                    Prompt = prompt,
                    Provider = provider,
                    DateCreated = File.GetCreationTime(file)
                });
            }

            results.Sort((a, b) => b.DateCreated.CompareTo(a.DateCreated));
            return results;
        }

        private static (string Prompt, string Provider) ParseSavedScriptHeader(string rawContent)
        {
            string prompt = string.Empty;
            string provider = string.Empty;
            string remaining = rawContent ?? string.Empty;

            remaining = ConsumeHeaderLine(remaining, "// Prompt: ", out prompt);
            remaining = ConsumeHeaderLine(remaining, "// Provider: ", out provider);

            return (prompt, provider);
        }

        private static string ConsumeHeaderLine(string content, string prefix, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrEmpty(content) || !content.StartsWith(prefix))
            {
                return content;
            }

            int lineEnd = content.IndexOf('\n');
            string line = lineEnd < 0 ? content : content.Substring(0, lineEnd);
            value = line.Substring(prefix.Length).TrimEnd('\r');
            return lineEnd < 0 ? string.Empty : content.Substring(lineEnd + 1);
        }
    }
}
