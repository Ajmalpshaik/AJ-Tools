// ==================================================
// Tool Name    : View Crop
// Purpose      : Code-behind for View Crop settings and run scope dialog.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.2
// Created      : 2026-04-08
// Last Updated : 2026-05-24
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// Input        : Active Revit document, active or selected target views, and View Crop settings.
// Output       : Updated view crop or annotation crop settings for supported target views.
// Notes        : Skips unsupported, template, scope-box-controlled, and view-template-locked views.
// Changelog    : v1.0.2 - Integrated presets logic and toggle annotation offsets.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AJTools.Models.ViewCrop;

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

            IncludeLinksCheckBox.IsChecked = settings.IncludeRevitLinks;
            IgnoreHiddenCheckBox.IsChecked = settings.IgnoreHiddenCategories;
            RectangularOnlyCheckBox.IsChecked = settings.RectangularCropOnly;
            IncludeDatumsCheckBox.IsChecked = settings.IncludeDatums;
            ShowDiagnosticsCheckBox.IsChecked = settings.ShowDiagnostics;
            IncludeCoordinationModelsCheckBox.IsChecked = settings.IncludeCoordinationModels;

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

        private void OnRun(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            string marginText = (MarginTextBox.Text ?? string.Empty).Trim();
            if (!double.TryParse(marginText, NumberStyles.Float, CultureInfo.CurrentCulture, out double marginMm))
            {
                ErrorText.Text = "Margin must be a valid number.";
                return;
            }

            if (marginMm < 0)
            {
                ErrorText.Text = "Margin cannot be negative.";
                return;
            }

            double annotationOffset = 100.0;
            if (ApplyAnnotationCheckBox.IsChecked == true)
            {
                string offsetText = (AnnotationOffsetTextBox.Text ?? string.Empty).Trim();
                if (!double.TryParse(offsetText, NumberStyles.Float, CultureInfo.CurrentCulture, out annotationOffset))
                {
                    ErrorText.Text = "Annotation offset must be a valid number.";
                    return;
                }

                if (annotationOffset < 0)
                {
                    ErrorText.Text = "Annotation offset cannot be negative.";
                    return;
                }
            }

            SelectedSettings = new ViewCropSettings
            {
                MarginMm = marginMm,
                IncludeRevitLinks = IncludeLinksCheckBox.IsChecked == true,
                IgnoreHiddenCategories = IgnoreHiddenCheckBox.IsChecked == true,
                RectangularCropOnly = RectangularOnlyCheckBox.IsChecked == true,
                IncludeDatums = IncludeDatumsCheckBox.IsChecked == true,
                ApplyAnnotationCrop = ApplyAnnotationCheckBox.IsChecked == true,
                AnnotationOffsetMm = annotationOffset,
                ShowDiagnostics = ShowDiagnosticsCheckBox.IsChecked == true,
                IncludeCoordinationModels = IncludeCoordinationModelsCheckBox.IsChecked == true
            };

            DialogResult = true;
            Close();
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
    }
}
