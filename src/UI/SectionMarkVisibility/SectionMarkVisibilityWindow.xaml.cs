#region Metadata
/*
 * Tool Name     : Section Mark Visibility
 * File Name     : SectionMarkVisibilityWindow.xaml.cs
 * Purpose       : Code-behind for the main options dialog (run scope, sheet-number rows, modes).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-05-24
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, WPF
 *
 * Input         : Previously saved settings
 * Output        : Selected settings; settings persisted to disk
 *
 * Notes         :
 * - Light code-behind only; no Revit model changes here.
 *
 * Changelog     :
 * v1.0.0 (2026-05-24) - Initial release.
 * v1.2.0 (2026-06-30) - Cleanup pass: metadata block; aligned with service refactor.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AJTools.Models.SectionMarkVisibility;
using AJTools.Utils;

namespace AJTools.UI.SectionMarkVisibility
{
    /// <summary>
    /// Interaction logic for SectionMarkVisibilityWindow.xaml
    /// </summary>
    public partial class SectionMarkVisibilityWindow : Window
    {
        private readonly ObservableCollection<SheetRowItem> _sheetRows;

        internal SectionMarkVisibilitySettings SelectedSettings { get; private set; }

        internal SectionMarkVisibilityWindow(SectionMarkVisibilitySettings initialSettings)
        {
            InitializeComponent();

            SectionMarkVisibilitySettings settings = (initialSettings ?? new SectionMarkVisibilitySettings()).Clone();
            SelectedSettings = settings;

            // Restore Run Scope radio buttons
            if (settings.ApplyToActiveViewOnly)
            {
                ActiveViewOnlyRadio.IsChecked = true;
            }
            else
            {
                MultipleViewsRadio.IsChecked = true;
            }

            // Restore Sheet Numbers into dynamic collection
            _sheetRows = new ObservableCollection<SheetRowItem>();
            if (settings.SheetNumbers != null && settings.SheetNumbers.Count > 0)
            {
                foreach (string num in settings.SheetNumbers)
                {
                    _sheetRows.Add(new SheetRowItem { SheetNumber = num });
                }
            }

            // The UI must initially contain at least one Sheet Number input row
            if (_sheetRows.Count == 0)
            {
                _sheetRows.Add(new SheetRowItem { SheetNumber = string.Empty });
            }

            // Bind to ItemsControl
            SheetNumbersItemsControl.ItemsSource = _sheetRows;

            UpdateActionButtonsText();
        }

        private void OnAddRow(object sender, RoutedEventArgs e)
        {
            _sheetRows.Add(new SheetRowItem { SheetNumber = string.Empty });
        }

        private void OnRemoveRow(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is SheetRowItem item)
            {
                _sheetRows.Remove(item);

                // Ensure there is always at least one row in the UI
                if (_sheetRows.Count == 0)
                {
                    _sheetRows.Add(new SheetRowItem { SheetNumber = string.Empty });
                }
            }
        }

        private void OnRunScopeChanged(object sender, RoutedEventArgs e)
        {
            UpdateActionButtonsText();
        }

        private void UpdateActionButtonsText()
        {
            if (ApplyButton == null || KeepPlacedButton == null || UnhideAllButton == null || ActiveViewOnlyRadio == null)
                return;

            bool activeOnly = ActiveViewOnlyRadio.IsChecked == true;
            ApplyButton.Content = activeOnly ? "Apply Filter" : "Next";
            KeepPlacedButton.Content = activeOnly ? "Keep All Placed" : "Next (Keep Placed)";
            UnhideAllButton.Content = activeOnly ? "Unhide All" : "Next (Unhide All)";
        }

        private void OnApplyFilter(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            // Extract, clean, and validate sheet numbers
            var sheets = _sheetRows
                .Select(r => (r.SheetNumber ?? string.Empty).Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (sheets.Count == 0)
            {
                ErrorText.Text = "Please enter at least one Sheet Number for filtering.";
                return;
            }

            SelectedSettings.ApplyToActiveViewOnly = ActiveViewOnlyRadio.IsChecked == true;
            SelectedSettings.KeepAllPlacedSections = false;
            SelectedSettings.UnhideAllSections = false;
            SelectedSettings.SheetNumbers = sheets;

            // Save settings persistent memory
            SectionMarkVisibilityConfigStore.Save(SelectedSettings);

            DialogResult = true;
            Close();
        }

        private void OnKeepAllPlaced(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            SelectedSettings.ApplyToActiveViewOnly = ActiveViewOnlyRadio.IsChecked == true;
            SelectedSettings.KeepAllPlacedSections = true;
            SelectedSettings.UnhideAllSections = false;
            SelectedSettings.SheetNumbers = new List<string>();

            SectionMarkVisibilityConfigStore.Save(SelectedSettings);

            DialogResult = true;
            Close();
        }

        private void OnUnhideAll(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            SelectedSettings.ApplyToActiveViewOnly = ActiveViewOnlyRadio.IsChecked == true;
            SelectedSettings.KeepAllPlacedSections = false;
            SelectedSettings.UnhideAllSections = true;
            SelectedSettings.SheetNumbers = new List<string>();

            SectionMarkVisibilityConfigStore.Save(SelectedSettings);

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Represents a single sheet number filter row inside the WPF ItemsControl.
    /// </summary>
    public sealed class SheetRowItem : INotifyPropertyChanged
    {
        private string _sheetNumber;
        public string SheetNumber
        {
            get => _sheetNumber;
            set
            {
                if (_sheetNumber != value)
                {
                    _sheetNumber = value;
                    OnPropertyChanged(nameof(SheetNumber));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
