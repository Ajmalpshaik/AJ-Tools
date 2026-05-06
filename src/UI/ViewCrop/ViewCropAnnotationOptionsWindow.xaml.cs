// ==================================================
// Tool Name    : View Crop
// Purpose      : Code-behind for annotation crop settings and run scope dialog.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-11
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
using System.Globalization;
using System.Windows;
using AJTools.Models.ViewCrop;

namespace AJTools.UI.ViewCrop
{
    /// <summary>
    /// Interaction logic for ViewCropAnnotationOptionsWindow.xaml
    /// </summary>
    public partial class ViewCropAnnotationOptionsWindow : Window
    {
        internal ViewCropAnnotationOptionsWindow(string toolTitle, ViewCropAnnotationSettings initialSettings)
        {
            InitializeComponent();

            Title = string.IsNullOrWhiteSpace(toolTitle)
                ? "Annotation Crop Settings"
                : toolTitle;

            ViewCropAnnotationSettings settings = (initialSettings ?? new ViewCropAnnotationSettings()).Clone();
            OffsetTextBox.Text = settings.OffsetMm.ToString("0.###", CultureInfo.CurrentCulture);

            SelectedSettings = settings;
            UpdateRunButtonText();
        }

        internal bool ApplyToActiveViewOnly => ActiveViewOnlyRadio.IsChecked == true;

        internal ViewCropAnnotationSettings SelectedSettings { get; private set; }

        private void OnApplyScopeChanged(object sender, RoutedEventArgs e)
        {
            UpdateRunButtonText();
        }

        private void OnRun(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            string offsetText = (OffsetTextBox.Text ?? string.Empty).Trim();
            if (!double.TryParse(offsetText, NumberStyles.Float, CultureInfo.CurrentCulture, out double offsetMm))
            {
                ErrorText.Text = "Offset must be a valid number.";
                return;
            }

            if (offsetMm < 0)
            {
                ErrorText.Text = "Offset cannot be negative.";
                return;
            }

            SelectedSettings = new ViewCropAnnotationSettings
            {
                OffsetMm = offsetMm
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
