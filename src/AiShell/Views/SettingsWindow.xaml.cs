#region Metadata
/*
 * Tool Name     : C# Settings
 * File Name     : SettingsWindow.xaml.cs
 * Purpose       : Code-behind for the modal AI provider settings popup - closes the window after
 *                 the Save/Close buttons are clicked, and syncs the four PasswordBoxes (API keys)
 *                 with the ViewModel, since WPF's PasswordBox.Password cannot be data-bound directly.
 *                 All the actual field logic otherwise lives in the shared AiShellViewModel (this
 *                 window just borrows the pane's DataContext).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-07-18
 * Last Updated  : 2026-08-05
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
 * v1.2.0 (2026-08-05) - Fourth provider section (NVIDIA NIM): key PasswordBox + reveal toggle on the
 *                       same Tag-matched pattern as the other three, plus the model picker.
 *                       THE MODEL PICKER IS DELIBERATELY TWO CONTROLS, not one editable ComboBox.
 *                       NVIDIA's catalog is ~130 models, so a fixed list cannot cover it and Ajmal
 *                       asked to be able to paste any model id - but SoftComboBoxStyle (SoftUiStyles
 *                       v1.1.0) replaces the ComboBox ControlTemplate and that template has NO
 *                       PART_EditableTextBox, so IsEditable="True" would render a control with
 *                       nothing to type into. Adding the part to the shared dictionary was rejected:
 *                       it is merged by the docked pane, which AiShellPaneProvider builds during
 *                       Revit's OnStartup, so a fault there takes the whole add-in down (it did once,
 *                       v1.16.0). A shortlist ComboBox and a plain TextBox, both bound TwoWay to
 *                       NvidiaModel, give the same result with no shared-style change.
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
using AJTools.Utils;

namespace AJTools.AiShell.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);
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
                NvidiaKeyBox.Password = vm.NvidiaApiKeyInput;
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

        private void NvidiaKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AiShellViewModel vm) vm.NvidiaApiKeyInput = NvidiaKeyBox.Password;
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
                case "Nvidia":
                    maskedBox = NvidiaKeyBox;
                    revealBox = NvidiaKeyRevealBox;
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
