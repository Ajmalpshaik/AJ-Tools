#region Metadata
/*
 * Tool Name     : C# Settings
 * File Name     : SettingsWindow.xaml.cs
 * Purpose       : Code-behind for the modal AI provider settings popup - closes the window after
 *                 the Save/Close buttons are clicked, and syncs the three PasswordBoxes (API keys)
 *                 with the ViewModel, since WPF's PasswordBox.Password cannot be data-bound directly.
 *                 All the actual field logic otherwise lives in the shared AiShellViewModel (this
 *                 window just borrows the pane's DataContext).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-07-18
 * Last Updated  : 2026-07-21
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in / WPF
 *
 * Dependencies  : AiShellViewModel (shared DataContext, not owned by this window)
 *
 * Input         : Save/Close button clicks, PasswordBox edits, "show key" toggle clicks.
 * Output        : Closes the window; the actual settings save is SaveSettingsCommand's job.
 *
 * Notes         :
 * - No Revit API access at all (pure local config via AiShellConfig) - safe to ShowDialog() directly
 *   from AiShellView's code-behind with no ExternalEvent involved.
 * - API keys are shown behind PasswordBox by default (masked) - Ajmal asked whether the key could
 *   leak; the key itself was already safe (DPAPI-encrypted at rest, sent only in an HTTPS header,
 *   never logged - see ajtools-conventions-log.md 2026-07-21), but the Settings UI previously showed
 *   it in a plain TextBox, visible on screen/screen-share. A one-way "👁 show" button reveals it on
 *   demand via a read-only TextBox bound to the same *ApiKeyInput property, instead of leaving it
 *   permanently in the clear.
 *
 * Changelog     :
 * v1.1.0 (2026-07-21) - Masked the three API key fields (PasswordBox instead of TextBox) with a
 *                       per-field show/hide toggle.
 * v1.0.0 (2026-07-18) - Initial release: Settings moved out of the docked pane's inline collapsible
 *                       panel into this standalone popup, per Ajmal's request.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Windows;
using System.Windows.Controls;
using AJTools.AiShell.ViewModels;

namespace AJTools.AiShell.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += SettingsWindow_Loaded;
        }

        /// <summary>PasswordBox.Password isn't a dependency property, so the initial value (already
        /// decrypted into the ViewModel at construction) has to be pushed in manually here rather
        /// than via XAML binding.</summary>
        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AiShellViewModel vm)
            {
                GeminiKeyBox.Password = vm.GeminiApiKeyInput;
                OpenAiKeyBox.Password = vm.OpenAiApiKeyInput;
                AnthropicKeyBox.Password = vm.AnthropicApiKeyInput;
            }
        }

        private void GeminiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AiShellViewModel vm) vm.GeminiApiKeyInput = GeminiKeyBox.Password;
        }

        private void OpenAiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AiShellViewModel vm) vm.OpenAiApiKeyInput = OpenAiKeyBox.Password;
        }

        private void AnthropicKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AiShellViewModel vm) vm.AnthropicApiKeyInput = AnthropicKeyBox.Password;
        }

        /// <summary>Swaps the masked PasswordBox for a read-only plain-text reveal of the same
        /// *ApiKeyInput value, for whichever provider's key row the button sits in (matched by Tag).</summary>
        private void ShowKeyToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button)) return;

            PasswordBox maskedBox;
            TextBox revealBox;

            switch (button.Tag as string)
            {
                case "Gemini":
                    maskedBox = GeminiKeyBox;
                    revealBox = GeminiKeyRevealBox;
                    break;
                case "OpenAi":
                    maskedBox = OpenAiKeyBox;
                    revealBox = OpenAiKeyRevealBox;
                    break;
                case "Anthropic":
                    maskedBox = AnthropicKeyBox;
                    revealBox = AnthropicKeyRevealBox;
                    break;
                default:
                    return;
            }

            bool nowRevealing = revealBox.Visibility != Visibility.Visible;
            maskedBox.Visibility = nowRevealing ? Visibility.Collapsed : Visibility.Visible;
            revealBox.Visibility = nowRevealing ? Visibility.Visible : Visibility.Collapsed;
            button.Content = nowRevealing ? "🙈" : "👁";
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
