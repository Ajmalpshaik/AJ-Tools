// Tool Name: View Crop Options Window
// Description: Collects run scope and crop settings before processing views.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-08
// Revit Version: 2020

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
