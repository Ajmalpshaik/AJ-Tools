// Tool Name: Linked ID Viewer UI
// Description: WPF dialog showing picked element ID and model source with copy support.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Windows
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AJTools.UI
{
    public partial class LinkedIdViewerWindow : Window
    {
        public LinkedIdViewerWindow(string elementId, string modelSource)
        {
            InitializeComponent();
            ElementIdBox.Text = elementId ?? string.Empty;
            ModelSourceBox.Text = modelSource ?? "Current Model";
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            ApplyTextWidthSizing();
        }

        private void OnCopyId(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ElementIdBox.Text))
                {
                    Clipboard.SetText(ElementIdBox.Text);
                }
            }
            catch (Exception)
            {
                // Ignore clipboard errors and keep the window open
            }
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyTextWidthSizing()
        {
            double targetWidth = Math.Max(
                MeasureTextWidth(ElementIdBox),
                MeasureTextWidth(ModelSourceBox));

            double minWidth = Math.Max(280, Math.Min(targetWidth, 640));
            ElementIdBox.MinWidth = minWidth;
            ModelSourceBox.MinWidth = minWidth;
        }

        private double MeasureTextWidth(TextBox box)
        {
            string text = box.Text ?? string.Empty;
            var typeface = new Typeface(box.FontFamily, box.FontStyle, box.FontWeight, box.FontStretch);
            var dpi = VisualTreeHelper.GetDpi(this);

            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                box.FontSize,
                Brushes.Black,
                dpi.PixelsPerDip);

            double chrome = box.Padding.Left + box.Padding.Right +
                            box.BorderThickness.Left + box.BorderThickness.Right;

            return formatted.Width + chrome;
        }
    }
}
