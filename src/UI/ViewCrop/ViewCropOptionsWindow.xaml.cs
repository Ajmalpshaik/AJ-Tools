#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropOptionsWindow.xaml.cs
 * Purpose       : Code-behind for the View Crop settings and run-scope dialog.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-06-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : WPF
 *
 * Input         : Initial ViewCropSettings, tool title.
 * Output        : Edited ViewCropSettings via SelectedSettings property; ApplyToActiveViewOnly flag.
 *
 * Notes         :
 * - Modal dialog - no Revit API calls in code-behind (skill rule).
 * - Annotation offset controls enable/disable based on the ApplyAnnotationCrop checkbox.
 *
 * Changelog     :
 * v1.2.0 (2026-06-28) - Added Crop Mode radio buttons (visible/all-model); wired to ViewCropSettings.ExtentSource.
 * v1.1.0 (2026-06-27) - Metadata refresh and version coverage notes.
 * v1.0.2 (2026-05-24) - Integrated presets logic and toggle annotation offsets.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AJTools.Models.ViewCrop;
using AJTools.Utils;

namespace AJTools.UI.ViewCrop
{
    /// <summary>
    /// Interaction logic for ViewCropOptionsWindow.xaml
    /// </summary>
    public partial class ViewCropOptionsWindow : Window
    {
        private bool _isCustomPresetSelection = false;

        internal ViewCropOptionsWindow(string toolTitle, ViewCropSettings initialSettings)
        {
            InitializeComponent();

            Title = string.IsNullOrWhiteSpace(toolTitle)
                ? "View Crop Settings"
                : toolTitle;

            // Populate presets dropdown
            PresetComboBox.Items.Add("Custom (Type your own)");
            PresetComboBox.Items.Add("Tight Crop (50 mm)");
            PresetComboBox.Items.Add("Standard Plan (300 mm)");
            PresetComboBox.Items.Add("Spacious Plan (1000 mm)");
            PresetComboBox.Items.Add("Site / Masterplan (3000 mm)");

            ViewCropSettings settings = (initialSettings ?? new ViewCropSettings()).Clone();
            MarginTextBox.Text = settings.MarginMm.ToString("0.###", CultureInfo.CurrentCulture);

            // Select matching preset if available
            SelectMatchingPreset(settings.MarginMm);

            ModeAllModelRadio.IsChecked = settings.ExtentSource == ViewCropExtentSource.AllModelElements;
            ModeVisibleRadio.IsChecked = settings.ExtentSource == ViewCropExtentSource.ActiveViewElements;

            IncludeLinksCheckBox.IsChecked = settings.IncludeRevitLinks;
            IncludeCoordinationModelsCheckBox.IsChecked = settings.IncludeCoordinationModels;
            IncludeDatumsCheckBox.IsChecked = settings.IncludeDatums;
            IgnoreHiddenCheckBox.IsChecked = settings.IgnoreHiddenCategories;

            // Annotation Integration
            ApplyAnnotationCheckBox.IsChecked = settings.ApplyAnnotationCrop;
            AnnotationOffsetTextBox.Text = settings.AnnotationOffsetMm.ToString("0.###", CultureInfo.CurrentCulture);
            UpdateAnnotationControlsState();

            SelectedSettings = settings;
            UpdateRunButtonText();
        }

        internal bool ApplyToActiveViewOnly => ActiveViewOnlyRadio.IsChecked == true;

        internal ViewCropSettings SelectedSettings { get; private set; }

        private void OnApplyScopeChanged(object sender, RoutedEventArgs e)
        {
            UpdateRunButtonText();
        }

        private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MarginTextBox == null) return;

            _isCustomPresetSelection = true;
            try
            {
                int index = PresetComboBox.SelectedIndex;
                switch (index)
                {
                    case 1: // Tight
                        MarginTextBox.Text = "50";
                        break;
                    case 2: // Standard
                        MarginTextBox.Text = "300";
                        break;
                    case 3: // Spacious
                        MarginTextBox.Text = "1000";
                        break;
                    case 4: // Site
                        MarginTextBox.Text = "3000";
                        break;
                }
            }
            finally
            {
                _isCustomPresetSelection = false;
            }
        }

        private void OnMarginTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isCustomPresetSelection) return;

            string text = MarginTextBox.Text;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double margin))
            {
                SelectMatchingPreset(margin);
            }
            else
            {
                PresetComboBox.SelectedIndex = 0; // Custom
            }
        }

        private void SelectMatchingPreset(double margin)
        {
            if (Math.Abs(margin - 50) < 0.001)
                PresetComboBox.SelectedIndex = 1;
            else if (Math.Abs(margin - 300) < 0.001)
                PresetComboBox.SelectedIndex = 2;
            else if (Math.Abs(margin - 1000) < 0.001)
                PresetComboBox.SelectedIndex = 3;
            else if (Math.Abs(margin - 3000) < 0.001)
                PresetComboBox.SelectedIndex = 4;
            else
                PresetComboBox.SelectedIndex = 0; // Custom
        }

        private void OnApplyAnnotationToggled(object sender, RoutedEventArgs e)
        {
            UpdateAnnotationControlsState();
        }

        private void UpdateAnnotationControlsState()
        {
            if (AnnotationOffsetLabel == null || AnnotationOffsetTextBox == null || ApplyAnnotationCheckBox == null)
                return;

            bool enable = ApplyAnnotationCheckBox.IsChecked == true;
            AnnotationOffsetLabel.IsEnabled = enable;
            AnnotationOffsetTextBox.IsEnabled = enable;
        }

        private bool ValidateAndBuildSettings(bool showDiagnostics)
        {
            ErrorText.Text = string.Empty;
            ErrorText.Visibility = Visibility.Collapsed;

            string marginText = (MarginTextBox.Text ?? string.Empty).Trim();
            if (!double.TryParse(marginText, NumberStyles.Float, CultureInfo.CurrentCulture, out double marginMm))
            {
                ErrorText.Text = "Margin must be a valid number.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            if (marginMm < 0)
            {
                ErrorText.Text = "Margin cannot be negative.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            double annotationOffset = 100.0;
            if (ApplyAnnotationCheckBox.IsChecked == true)
            {
                string offsetText = (AnnotationOffsetTextBox.Text ?? string.Empty).Trim();
                if (!double.TryParse(offsetText, NumberStyles.Float, CultureInfo.CurrentCulture, out annotationOffset))
                {
                    ErrorText.Text = "Annotation offset must be a valid number.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                if (annotationOffset < 0)
                {
                    ErrorText.Text = "Annotation offset cannot be negative.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }
            }

            SelectedSettings = new ViewCropSettings
            {
                MarginMm = marginMm,
                IncludeRevitLinks = IncludeLinksCheckBox.IsChecked == true,
                IgnoreHiddenCategories = IgnoreHiddenCheckBox.IsChecked == true,
                RectangularCropOnly = true,
                IncludeDatums = IncludeDatumsCheckBox.IsChecked == true,
                ApplyAnnotationCrop = ApplyAnnotationCheckBox.IsChecked == true,
                AnnotationOffsetMm = annotationOffset,
                ShowDiagnostics = showDiagnostics,
                IncludeCoordinationModels = IncludeCoordinationModelsCheckBox.IsChecked == true,
                ExtentSource = ModeVisibleRadio.IsChecked == true
                    ? ViewCropExtentSource.ActiveViewElements
                    : ViewCropExtentSource.AllModelElements
            };

            return true;
        }

        private void OnRun(object sender, RoutedEventArgs e)
        {
            if (ValidateAndBuildSettings(false))
            {
                DialogResult = true;
                Close();
            }
        }

        private void OnDiagnosticsRun(object sender, RoutedEventArgs e)
        {
            if (ValidateAndBuildSettings(true))
            {
                DialogResult = true;
                Close();
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateRunButtonText()
        {
            if (RunButton == null || ActiveViewOnlyRadio == null)
                return;

            RunButton.Content = ActiveViewOnlyRadio.IsChecked == true ? "Run" : "Next";
        }

        private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
        {
            WindowChromeHelper.HandleTitleBarDrag(this, e);
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowChromeHelper.Minimize(this);
        }

        private void OnMaximizeClick(object sender, RoutedEventArgs e)
        {
            WindowChromeHelper.ToggleMaximize(this, RootBorder);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            OnCancel(sender, e);
        }
    }
}
