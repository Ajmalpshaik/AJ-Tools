// Tool Name: Linked ID Viewer UI
// Description: WPF dialog showing picked element ID and model source with copy support.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Windows
using System;
using System.Windows;

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
    }
}
