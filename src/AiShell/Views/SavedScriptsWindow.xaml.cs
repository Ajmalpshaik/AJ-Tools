#region Metadata
/*
 * Tool Name     : C# Saved Scripts
 * File Name     : SavedScriptsWindow.xaml.cs
 * Purpose       : Code-behind for the standalone "Saved Scripts" window - lists every .cs file in
 *                 the configured Scripts Folder (scanned fresh from disk each time, not a cached
 *                 in-memory history), with a "📌 Pin" and "▶ Run" button per row.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-21
 * Last Updated  : 2026-07-21
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in / WPF
 *
 * Dependencies  : AiShellConfig, ShowSavedScriptsCommand.ScanFolder, RunPinnedScriptCommand.RunScriptFile
 *
 * Input         : Browse/Refresh/Close button clicks, per-row Pin/Run clicks.
 * Output        : Updates AiShellConfig (folder path, pinned script); runs a script against the
 *                 active document via the same path RunPinnedScriptCommand uses.
 *
 * Notes         :
 * - Deliberately plain code-behind, not an MVVM ViewModel - this window is opened directly from a
 *   ribbon command (ShowSavedScriptsCommand), independent of the C# pane/AiShellViewModel, matching
 *   RunPinnedScriptCommand's own "must not depend on the WPF ViewModel layer" reasoning.
 * - "▶ Run" keeps the window open afterward (TaskDialog reports the result) so more than one script
 *   can be tried in a row without reopening the tool.
 *
 * Changelog     :
 * v1.0.0 (2026-07-21) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.IO;
using System.Windows;
using Autodesk.Revit.UI;
using AJTools.AiShell.Commands;
using AJTools.AiShell.Configuration;
using AJTools.AiShell.Models;
using AJTools.Utils;

namespace AJTools.AiShell.Views
{
    public partial class SavedScriptsWindow : Window
    {
        private readonly AiShellConfig _config;
        private readonly ExternalCommandData _commandData;

        public SavedScriptsWindow(AiShellConfig config, ExternalCommandData commandData)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);
            _config = config;
            _commandData = commandData;
            RefreshList();
        }

        private void RefreshList()
        {
            FolderPathBox.Text = _config.ScriptsFolderPath;

            var items = ShowSavedScriptsCommand.ScanFolder(_config.ScriptsFolderPath);
            ScriptsListBox.ItemsSource = items;
            EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            UpdatePinnedStatusText();
        }

        private void UpdatePinnedStatusText()
        {
            PinnedStatusText.Text = string.IsNullOrWhiteSpace(_config.PinnedScriptPath)
                ? "No script pinned to the ribbon yet."
                : $"📌 Pinned to ribbon: {Path.GetFileNameWithoutExtension(_config.PinnedScriptPath)}";
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.SelectedPath = _config.ScriptsFolderPath;
                dialog.Description = "Select a folder to save and load your scripts.";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _config.ScriptsFolderPath = dialog.SelectedPath;
                    _config.Save();
                    RefreshList();
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void PinScript_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.DataContext is HistoryItem item)) return;

            _config.PinnedScriptPath = item.FilePath;
            _config.Save();
            UpdatePinnedStatusText();
        }

        private void RunScript_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.DataContext is HistoryItem item)) return;
            if (!File.Exists(item.FilePath))
            {
                TaskDialog.Show("AJ AI: Saved Scripts", "This script file no longer exists. Click Refresh.");
                return;
            }

            string message = string.Empty;
            var result = RunPinnedScriptCommand.RunScriptFile(_commandData, item.FilePath, ref message);

            if (result == Result.Failed && !string.IsNullOrWhiteSpace(message))
            {
                TaskDialog.Show("AJ AI: Script Failed", message);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
