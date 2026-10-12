// Tool Name: Linked ID Viewer UI
// Description: WPF dialog showing picked element ID and model source with copy support.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2026-10-12
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Windows
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AJTools.Utils;

namespace AJTools.UI
{
    public partial class LinkedIdViewerWindow : Window
    {
        private readonly string _defaultInfoText;

        public LinkedIdViewerWindow(string elementId, string modelSource)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);
            ElementIdBox.Text = elementId ?? string.Empty;
            ModelSourceBox.Text = modelSource ?? "Current Model";
            _defaultInfoText = InfoText.Text;
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

                InfoText.Text = _defaultInfoText;
                InfoText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            }
            catch (Exception ex)
            {
                // Keep the window open, but say the copy did not happen - otherwise the user pastes
                // whatever was on the clipboard before. Usually another program holding the clipboard.
                InfoText.Text = "Could not copy to the clipboard: " + ex.Message +
                                " Try Copy again, or select the ID above and press Ctrl+C.";
                InfoText.SetResourceReference(TextBlock.ForegroundProperty, "AccentDanger");
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
