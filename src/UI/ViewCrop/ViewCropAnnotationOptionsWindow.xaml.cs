// Tool Name: View Crop Annotation Options Window
// Description: Collects run scope and annotation crop offset settings before processing views.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-11
// Revit Version: 2020

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
