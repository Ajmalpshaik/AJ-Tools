#region Metadata
/*
 * Tool Name     : C# Settings
 * File Name     : SettingsWindow.xaml.cs
 * Purpose       : Code-behind for the modal AI provider settings popup - closes the window after
 *                 the Save/Close buttons are clicked. All the actual field logic lives in the shared
 *                 AiShellViewModel (this window just borrows the pane's DataContext).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-18
 * Last Updated  : 2026-07-18
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in / WPF
 *
 * Dependencies  : AiShellViewModel (shared DataContext, not owned by this window)
 *
 * Input         : Save/Close button clicks.
 * Output        : Closes the window; the actual settings save is SaveSettingsCommand's job.
 *
 * Notes         :
 * - No Revit API access at all (pure local config via AiShellConfig) - safe to ShowDialog() directly
 *   from AiShellView's code-behind with no ExternalEvent involved.
 *
 * Changelog     :
 * v1.0.0 (2026-07-18) - Initial release: Settings moved out of the docked pane's inline collapsible
 *                       panel into this standalone popup, per Ajmal's request.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Windows;

namespace AJTools.AiShell.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // SaveSettingsCommand (bound on the same button) does the actual save; this just
            // closes the popup once the click has been handled.
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
