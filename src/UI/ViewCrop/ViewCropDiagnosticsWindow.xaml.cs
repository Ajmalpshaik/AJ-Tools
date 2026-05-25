// ==================================================
// Tool Name    : View Crop - Diagnostics Window Code-Behind
// Purpose      : Manages copying diagnostics data to clipboard and window closing.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-24
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// ==================================================
using System;
using System.Windows;

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
                NotificationText.Text = "Debug Report successfully copied to Clipboard!";
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
    }
}
