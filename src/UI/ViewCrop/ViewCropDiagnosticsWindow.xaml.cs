#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropDiagnosticsWindow.xaml.cs
 * Purpose       : Code-behind for the View Crop diagnostics window. Displays per-view boundary detail and supports clipboard copy.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-05-24
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : WPF
 *
 * Input         : Diagnostic report string.
 * Output        : Modal window; clipboard copy on demand.
 *
 * Notes         :
 * - Modal dialog - no Revit API calls in code-behind (skill rule).
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Metadata refresh; plain-language copy wording (dropped developer "Debug" term).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Windows;
using System.Windows.Input;
using AJTools.Utils;

namespace AJTools.UI.ViewCrop
{
    /// <summary>
    /// Interaction logic for ViewCropDiagnosticsWindow.xaml
    /// </summary>
    public partial class ViewCropDiagnosticsWindow : Window
    {
        public ViewCropDiagnosticsWindow(string diagnosticReport)
        {
            InitializeComponent();
            DiagnosticsTextBox.Text = string.IsNullOrWhiteSpace(diagnosticReport)
                ? "No diagnostic data was generated."
                : diagnosticReport;
        }

        private void OnCopyReport(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(DiagnosticsTextBox.Text);
                NotificationText.Text = "Report copied to clipboard.";
                NotificationText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                NotificationText.Text = $"Failed to copy: {ex.Message}";
                NotificationText.Visibility = Visibility.Visible;
            }
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
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
            Close();
        }
    }
}
