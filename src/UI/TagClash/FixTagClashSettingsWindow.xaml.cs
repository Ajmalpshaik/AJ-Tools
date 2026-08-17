#region Metadata
/*
 * Tool Name     : Fix Tag Clash - Settings (Window)
 * File Name     : FixTagClashSettingsWindow.xaml.cs
 * Purpose       : WPF settings window for the tag clash engine - rounds, drift limit, mark colour and
 *                 matching tolerances.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-16
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF)
 *
 * Dependencies  : System.Windows (WPF), AJTools.Utils (TagClashSettings), AJTools.UI.ModernStyles
 *
 * Input         : The current settings.
 * Output        : Result - the validated settings, read by the calling command when DialogResult is true.
 *
 * Notes         :
 * - Pure UI. No Revit API calls, no model access, no transaction - the command owns saving.
 * - Validation is live and inline; the window never closes on a bad value and never shows a popup.
 * - Accepts both "12.5" and "12,5" so a comma-decimal Windows locale cannot silently misread a value.
 * - The stored settings also carry "skip vertical runs", whose tick box lives on the Create Tags
 *   settings window because it is a tagging rule, not a clash rule. This window never shows it but
 *   MUST hand it back untouched - saving rewrites the whole file, so dropping it would reset a choice
 *   made elsewhere. Same reason "Reset to defaults" here deliberately leaves that one value alone.
 *
 * Changelog     :
 * v1.1.0 (2026-08-17) - Moved the "skip vertical runs" tick box out to the Create Tags settings window
 *                       where a tagging rule belongs; the value is still stored here and carried
 *                       through untouched. Ajmal's call.
 * v1.0.0 (2026-08-16) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Globalization;
using System.Windows;
using AJTools.Utils;

namespace AJTools.UI.TagClash
{
    /// <summary>
    /// Interaction logic for FixTagClashSettingsWindow.xaml
    /// </summary>
    public partial class FixTagClashSettingsWindow : Window
    {
        private const double MinToleranceMm = 0.01;
        private const double MaxToleranceMm = 50.0;
        private const double MinGapMm = 0.01;
        private const double MaxGapMm = 100.0;

        private bool _loaded;

        /// <summary>
        /// Carried through untouched. "Skip vertical runs" is a TAGGING rule and its tick box lives on
        /// the Create Tags settings window, but the value is stored in the same file as these settings
        /// (the only tagging store that survives closing Revit). Since saving rewrites the whole file,
        /// this window must hand the value back exactly as it found it - otherwise saving a clash
        /// setting would quietly reset a choice made in Create Tags.
        /// </summary>
        private bool _skipVerticalRuns;

        /// <summary>
        /// The validated settings. Only meaningful when DialogResult is true.
        /// </summary>
        public TagClashSettingsState Result { get; private set; }

        public FixTagClashSettingsWindow(TagClashSettingsState current)
        {
            InitializeComponent();
            LoadFrom(current ?? TagClashSettings.CreateDefaults());
            _loaded = true;
            Validate();
        }

        private void LoadFrom(TagClashSettingsState state)
        {
            PassesBox.Text = state.FixPasses.ToString(CultureInfo.CurrentCulture);
            DriftBox.Text = FormatNumber(state.MaxDriftMm);
            ToleranceBox.Text = FormatNumber(state.ClashToleranceMm);
            GapBox.Text = FormatNumber(state.MinGapMm);

            MarkFailuresBox.IsChecked = state.MarkFailures;
            FullSearchBox.IsChecked = state.FullSearch;

            // Not shown here - see the field's note. Preserved so Save cannot reset it.
            _skipVerticalRuns = state.SkipVerticalRuns;

            ColourRed.IsChecked = state.MarkColour == TagClashMarkColour.Red;
            ColourGreen.IsChecked = state.MarkColour == TagClashMarkColour.Green;
            ColourOrange.IsChecked = state.MarkColour == TagClashMarkColour.Orange;
            ColourBlue.IsChecked = state.MarkColour == TagClashMarkColour.Blue;
            ColourMagenta.IsChecked = state.MarkColour == TagClashMarkColour.Magenta;

            if (state.MarkColour != TagClashMarkColour.Red
                && state.MarkColour != TagClashMarkColour.Green
                && state.MarkColour != TagClashMarkColour.Orange
                && state.MarkColour != TagClashMarkColour.Blue
                && state.MarkColour != TagClashMarkColour.Magenta)
            {
                ColourRed.IsChecked = true;
            }

            // The advanced boxes stay hidden unless they are actually holding something other than the
            // defaults - a non-default value tucked out of sight is how a setting gets forgotten and
            // then blamed on the tool.
            bool advancedIsNonDefault =
                Math.Abs(state.ClashToleranceMm - TagClashSettings.DefaultClashToleranceMm) > 0.0005
                || Math.Abs(state.MinGapMm - TagClashSettings.DefaultMinGapMm) > 0.0005;

            ShowAdvancedBox.IsChecked = advancedIsNonDefault;
            ApplyAdvancedVisibility();
        }

        private void OnAdvancedToggled(object sender, RoutedEventArgs e)
        {
            ApplyAdvancedVisibility();
        }

        private void ApplyAdvancedVisibility()
        {
            if (AdvancedPanel == null || ShowAdvancedBox == null)
                return;

            AdvancedPanel.Visibility = ShowAdvancedBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private TagClashMarkColour ReadColour()
        {
            if (ColourGreen.IsChecked == true)
                return TagClashMarkColour.Green;
            if (ColourOrange.IsChecked == true)
                return TagClashMarkColour.Orange;
            if (ColourBlue.IsChecked == true)
                return TagClashMarkColour.Blue;
            if (ColourMagenta.IsChecked == true)
                return TagClashMarkColour.Magenta;

            return TagClashMarkColour.Red;
        }

        /// <summary>
        /// Checks every field, shows the first problem inline, and enables Save only when all are good.
        /// </summary>
        private bool Validate()
        {
            if (!_loaded)
                return false;

            string error = null;

            int passes;
            if (!TryReadInt(PassesBox.Text, out passes)
                || passes < TagClashSettings.MinFixPasses
                || passes > TagClashSettings.MaxFixPasses)
            {
                error = string.Format(
                    "Rounds must be a whole number between {0} and {1}.",
                    TagClashSettings.MinFixPasses, TagClashSettings.MaxFixPasses);
            }

            double drift;
            if (error == null
                && (!TryReadDouble(DriftBox.Text, out drift)
                    || drift < TagClashSettings.MinDriftMm
                    || drift > TagClashSettings.MaxDriftMmLimit))
            {
                error = string.Format(
                    "How far a tag may move must be between {0:F0}mm and {1:F0}mm.",
                    TagClashSettings.MinDriftMm, TagClashSettings.MaxDriftMmLimit);
            }

            double tolerance;
            if (error == null
                && (!TryReadDouble(ToleranceBox.Text, out tolerance)
                    || tolerance < MinToleranceMm
                    || tolerance > MaxToleranceMm))
            {
                error = string.Format(
                    "Overlap must be between {0}mm and {1:F0}mm.", MinToleranceMm, MaxToleranceMm);
            }

            double gap;
            if (error == null
                && (!TryReadDouble(GapBox.Text, out gap)
                    || gap < MinGapMm
                    || gap > MaxGapMm))
            {
                error = string.Format(
                    "Gap must be between {0}mm and {1:F0}mm.", MinGapMm, MaxGapMm);
            }

            ErrorText.Text = error ?? string.Empty;
            SaveButton.IsEnabled = error == null;
            return error == null;
        }

        private void OnValueChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Validate();
        }

        private void OnPassPreset(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button == null || button.Tag == null)
                return;

            PassesBox.Text = button.Tag.ToString();
            Validate();
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            // "Reset to defaults" resets THIS window's settings only. The skip-vertical-runs value
            // belongs to Create Tags and is deliberately carried across the reset.
            bool keepSkipVerticalRuns = _skipVerticalRuns;

            LoadFrom(TagClashSettings.CreateDefaults());

            _skipVerticalRuns = keepSkipVerticalRuns;
            Validate();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (!Validate())
                return;

            int passes;
            double drift;
            double tolerance;
            double gap;

            TryReadInt(PassesBox.Text, out passes);
            TryReadDouble(DriftBox.Text, out drift);
            TryReadDouble(ToleranceBox.Text, out tolerance);
            TryReadDouble(GapBox.Text, out gap);

            // Saving REWRITES THE WHOLE FILE, so every value this window does not own must be carried
            // through or it is wiped. SkipVerticalRuns was already carried for exactly this reason
            // (v1.49.2); the five Smart MEP Tag values added in v1.49.7 were NOT, so opening this
            // window and pressing Save silently reset that tool's size rules and leader choices.
            // Re-read them here rather than trusting a snapshot taken when this window opened - the
            // other settings window may have been used in between.
            TagClashSettingsState stored = TagClashSettings.Load();

            Result = new TagClashSettingsState
            {
                FixPasses = passes,
                MaxDriftMm = drift,
                ClashToleranceMm = tolerance,
                MinGapMm = gap,
                MarkColour = ReadColour(),
                MarkFailures = MarkFailuresBox.IsChecked == true,
                SkipVerticalRuns = _skipVerticalRuns,
                FullSearch = FullSearchBox.IsChecked == true,

                // Not shown on this window - owned by Smart MEP Tag Settings, passed straight through.
                SmartTagMinLengthMm = stored.SmartTagMinLengthMm,
                SmartTagFilterBySize = stored.SmartTagFilterBySize,
                SmartTagMinWidthMm = stored.SmartTagMinWidthMm,
                SmartTagMinHeightMm = stored.SmartTagMinHeightMm,
                SmartTagNoLeaderCategories = stored.SmartTagNoLeaderCategories
            };

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private static bool TryReadInt(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return int.TryParse(
                text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
                || int.TryParse(
                    text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Reads a number, accepting either a dot or a comma decimal separator so a comma-decimal
        /// Windows locale cannot silently misread the value.
        /// </summary>
        private static bool TryReadDouble(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string trimmed = text.Trim();

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            return double.TryParse(
                trimmed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
