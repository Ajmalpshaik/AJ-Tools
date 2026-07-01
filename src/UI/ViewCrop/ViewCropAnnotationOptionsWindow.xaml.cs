#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropAnnotationOptionsWindow.xaml.cs
 * Purpose       : Code-behind for the annotation-crop settings and run-scope dialog.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-11
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : WPF
 *
 * Input         : Initial ViewCropAnnotationSettings, tool title.
 * Output        : Edited ViewCropAnnotationSettings via SelectedSettings property; ApplyToActiveViewOnly flag.
 *
 * Notes         :
 * - Modal dialog - no Revit API calls in code-behind (skill rule).
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Metadata refresh and version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using AJTools.Models.ViewCrop;
using AJTools.Utils;

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
            ErrorText.Visibility = Visibility.Collapsed;

            string offsetText = (OffsetTextBox.Text ?? string.Empty).Trim();
            if (!double.TryParse(offsetText, NumberStyles.Float, CultureInfo.CurrentCulture, out double offsetMm))
            {
                ShowError("Offset must be a valid number.");
                return;
            }

            if (offsetMm < 0)
            {
                ShowError("Offset cannot be negative.");
                return;
            }

            SelectedSettings = new ViewCropAnnotationSettings
            {
                OffsetMm = offsetMm
            };

            DialogResult = true;
            Close();
        }

        private void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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

        private void UpdateRunButtonText()
        {
            if (RunButton == null || ActiveViewOnlyRadio == null)
                return;

            RunButton.Content = ActiveViewOnlyRadio.IsChecked == true ? "Run" : "Next";
        }
    }
}
