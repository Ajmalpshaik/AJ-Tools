#region Metadata
/*
 * Tool Name     : Arrange Tags - Settings (Window)
 * File Name     : ArrangeTagsSettingsWindow.xaml.cs
 * Purpose       : WPF settings window for the default vertical spacing (mm on paper) used by Rearrange Tags.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-27
 * Last Updated  : 2026-07-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF)
 *
 * Dependencies  : System.Windows (WPF), AJTools.UI.ModernStyles
 *
 * Input         : Current spacing (mm) and the active view scale (optional, for the live explanation).
 * Output        : SpacingMm - the validated value, read by the calling command after DialogResult == true.
 *
 * Notes         :
 * - Pure UI. No Revit API calls, no model access, no transaction - the command owns saving.
 * - Modal window: shown with ShowDialog() from inside IExternalCommand.Execute.
 * - Validation is live and inline; the window never closes on a bad value and never shows a popup.
 * - Accepts both "12.5" and "12,5" so a comma-decimal Windows locale cannot silently misread the value.
 *
 * Changelog     :
 * v1.0.0 (2026-07-27) - Initial release. Replaces the old WinForms spacing prompt.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace AJTools.UI.TagArrange
{
    /// <summary>
    /// Interaction logic for ArrangeTagsSettingsWindow.xaml
    /// </summary>
    public partial class ArrangeTagsSettingsWindow : Window
    {
        private const double MinSpacingMm = 0.1;
        private const double MaxSpacingMm = 250.0;

        private readonly double _defaultSpacingMm;
        private readonly int _viewScale;

        /// <summary>
        /// The validated spacing (mm on paper) chosen by the user. Only meaningful when DialogResult is true.
        /// </summary>
        public double SpacingMm { get; private set; }

        /// <summary>
        /// Creates the settings window.
        /// </summary>
        /// <param name="currentSpacingMm">Spacing currently stored in settings.</param>
        /// <param name="defaultSpacingMm">Factory default, offered by the Reset button.</param>
        /// <param name="viewScale">Active view scale denominator (1:x). Pass 0 when unknown.</param>
        public ArrangeTagsSettingsWindow(double currentSpacingMm, double defaultSpacingMm, int viewScale)
        {
            InitializeComponent();

            _defaultSpacingMm = defaultSpacingMm > 0 ? defaultSpacingMm : 12.0;
            _viewScale = viewScale > 0 ? viewScale : 0;

            ResetButton.Content = $"Reset to default ({Format(_defaultSpacingMm)} mm)";
            ResetButton.ToolTip = $"Put the spacing back to {Format(_defaultSpacingMm)} mm.";

            double start = currentSpacingMm > 0 ? currentSpacingMm : _defaultSpacingMm;
            SpacingBox.Text = Format(start);

            Loaded += (s, e) =>
            {
                SpacingBox.Focus();
                SpacingBox.SelectAll();
            };
        }

        private void OnSpacingChanged(object sender, TextChangedEventArgs e)
        {
            Validate(out _);
        }

        private void OnPresetClicked(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || button.Tag == null)
                return;

            SpacingBox.Text = Convert.ToString(button.Tag, CultureInfo.InvariantCulture);
            SpacingBox.Focus();
            SpacingBox.SelectAll();
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            SpacingBox.Text = Format(_defaultSpacingMm);
            SpacingBox.Focus();
            SpacingBox.SelectAll();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (!Validate(out double spacingMm))
            {
                SpacingBox.Focus();
                SpacingBox.SelectAll();
                return;
            }

            SpacingMm = spacingMm;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Validates the typed value, refreshes the inline message and the live explanation,
        /// and enables or disables Save. Never shows a popup.
        /// </summary>
        private bool Validate(out double spacingMm)
        {
            spacingMm = 0;

            string raw = SpacingBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(raw))
                return Reject("Enter a spacing value in mm.");

            if (!TryParseNumber(raw, out double parsed))
                return Reject("That is not a number. Type a value such as 12 or 12.5.");

            if (parsed <= 0)
                return Reject("Spacing must be greater than zero.");

            if (parsed < MinSpacingMm)
                return Reject($"Spacing is too small. Use at least {Format(MinSpacingMm)} mm.");

            if (parsed > MaxSpacingMm)
                return Reject($"Spacing is too large. Use {Format(MaxSpacingMm)} mm or less.");

            spacingMm = parsed;

            ErrorText.Text = string.Empty;
            SaveButton.IsEnabled = true;
            PreviewText.Text = BuildPreview(parsed);
            return true;
        }

        private bool Reject(string message)
        {
            ErrorText.Text = message;
            SaveButton.IsEnabled = false;
            PreviewText.Text = "Tags stack this far apart on the printed sheet.";
            return false;
        }

        private string BuildPreview(double spacingMm)
        {
            string sheet = $"Tags stack {Format(spacingMm)} mm apart on the printed sheet.";

            if (_viewScale <= 0)
                return sheet + " The gap in the model follows the scale of the view you run the tool in.";

            double modelMm = spacingMm * _viewScale;
            return sheet + $" In the current view (1:{_viewScale}) that is {Format(modelMm)} mm in the model.";
        }

        /// <summary>
        /// Parses a number using the Windows locale first, then the invariant format,
        /// so both "12.5" and "12,5" are read correctly on any regional setting.
        /// </summary>
        private static bool TryParseNumber(string raw, out double value)
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Format(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);
    }
}
