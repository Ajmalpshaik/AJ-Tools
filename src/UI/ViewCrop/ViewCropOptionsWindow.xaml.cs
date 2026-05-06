// ==================================================
// Tool Name    : View Crop
// Purpose      : Code-behind for View Crop settings and run scope dialog.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-08
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// Input        : Active Revit document, active or selected target views, and View Crop settings.
// Output       : Updated view crop or annotation crop settings for supported target views.
// Notes        : Skips unsupported, template, scope-box-controlled, and view-template-locked views.
// Changelog    : v1.0.1 - Standardized metadata after production cleanup.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using System.Globalization;
using System.Windows;
using AJTools.Models.ViewCrop;

namespace AJTools.UI.ViewCrop
{
    /// <summary>
    /// Interaction logic for ViewCropOptionsWindow.xaml
    /// </summary>
    public partial class ViewCropOptionsWindow : Window
    {
        internal ViewCropOptionsWindow(string toolTitle, ViewCropSettings initialSettings)
        {
            InitializeComponent();

            Title = string.IsNullOrWhiteSpace(toolTitle)
                ? "View Crop Settings"
                : toolTitle;

            ViewCropSettings settings = (initialSettings ?? new ViewCropSettings()).Clone();
            MarginTextBox.Text = settings.MarginMm.ToString("0.###", CultureInfo.CurrentCulture);
            IncludeLinksCheckBox.IsChecked = settings.IncludeRevitLinks;
            IgnoreHiddenCheckBox.IsChecked = settings.IgnoreHiddenCategories;
            RectangularOnlyCheckBox.IsChecked = settings.RectangularCropOnly;
            IncludeDatumsCheckBox.IsChecked = settings.IncludeDatums;

            SelectedSettings = settings;
            UpdateRunButtonText();
        }

        internal bool ApplyToActiveViewOnly => ActiveViewOnlyRadio.IsChecked == true;

        internal ViewCropSettings SelectedSettings { get; private set; }

        private void OnApplyScopeChanged(object sender, RoutedEventArgs e)
        {
            UpdateRunButtonText();
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

            SelectedSettings = new ViewCropSettings
            {
                MarginMm = marginMm,
                IncludeRevitLinks = IncludeLinksCheckBox.IsChecked == true,
                IgnoreHiddenCategories = IgnoreHiddenCheckBox.IsChecked == true,
                RectangularCropOnly = RectangularOnlyCheckBox.IsChecked == true,
                IncludeDatums = IncludeDatumsCheckBox.IsChecked == true
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
