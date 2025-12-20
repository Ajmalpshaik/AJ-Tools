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
using System.Windows.Threading;

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
            // Auto-size once to content, then allow manual resizing.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SizeToContent = SizeToContent.Manual;
                Width = ActualWidth;
                Height = ActualHeight;
            }), DispatcherPriority.Background);
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

            double minWidth = Math.Max(200, targetWidth);
            ElementIdBox.MinWidth = minWidth;
            ModelSourceBox.MinWidth = minWidth;
            if (InfoText != null)
            {
                InfoText.MaxWidth = minWidth;
            }
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
