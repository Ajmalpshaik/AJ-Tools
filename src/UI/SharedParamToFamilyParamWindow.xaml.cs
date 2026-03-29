// Tool Name: Shared Parameter to Family Parameter - UI
// Description: Window logic for selecting shared parameters to convert.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-26
// Revit Version: 2020
// Dependencies: System.Windows

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AJTools.Models;

namespace AJTools.UI
{
    public partial class SharedParamToFamilyParamWindow : Window
    {
        private readonly ObservableCollection<SharedParamToFamilyParamItem> _availableParameters;
        private readonly ObservableCollection<SharedParamToFamilyParamItem> _selectedParameters;

        internal SharedParamToFamilyParamWindow(IList<SharedParamToFamilyParamItem> sharedParameters)
        {
            InitializeComponent();

            _availableParameters = new ObservableCollection<SharedParamToFamilyParamItem>();
            _selectedParameters = new ObservableCollection<SharedParamToFamilyParamItem>();

            if (sharedParameters != null)
            {
                List<SharedParamToFamilyParamItem> ordered = sharedParameters
                    .Where(item => item != null)
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                for (int i = 0; i < ordered.Count; i++)
                {
                    _availableParameters.Add(ordered[i]);
                }
            }

            AvailableListBox.ItemsSource = _availableParameters;
            SelectedListBox.ItemsSource = _selectedParameters;
            UpdateUiState();
        }

        internal IList<SharedParamToFamilyParamItem> SelectedItems
        {
            get { return _selectedParameters.ToList(); }
        }

        private void OnAvailableItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MoveToSelected(AvailableListBox.SelectedItem as SharedParamToFamilyParamItem);
        }

        private void OnSelectedItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MoveToAvailable(SelectedListBox.SelectedItem as SharedParamToFamilyParamItem);
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            MoveToSelected(AvailableListBox.SelectedItem as SharedParamToFamilyParamItem);
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            MoveToAvailable(SelectedListBox.SelectedItem as SharedParamToFamilyParamItem);
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUiState();
        }

        private void MoveToSelected(SharedParamToFamilyParamItem item)
        {
            if (item == null || !_availableParameters.Contains(item))
            {
                return;
            }

            _availableParameters.Remove(item);
            InsertSorted(_selectedParameters, item);
            SelectedListBox.SelectedItem = item;
            UpdateUiState();
        }

        private void MoveToAvailable(SharedParamToFamilyParamItem item)
        {
            if (item == null || !_selectedParameters.Contains(item))
            {
                return;
            }

            _selectedParameters.Remove(item);
            InsertSorted(_availableParameters, item);
            AvailableListBox.SelectedItem = item;
            UpdateUiState();
        }

        private static void InsertSorted(
            ObservableCollection<SharedParamToFamilyParamItem> collection,
            SharedParamToFamilyParamItem item)
        {
            if (collection == null || item == null)
            {
                return;
            }

            for (int i = 0; i < collection.Count; i++)
            {
                var existing = collection[i];
                int compare = string.Compare(item.Name, existing?.Name, false, CultureInfo.CurrentCulture);
                if (compare < 0)
                {
                    collection.Insert(i, item);
                    return;
                }
            }

            collection.Add(item);
        }

        private void UpdateUiState()
        {
            AddButton.IsEnabled = AvailableListBox.SelectedItem != null;
            RemoveButton.IsEnabled = SelectedListBox.SelectedItem != null;

            StatusText.Text =
                $"Shared parameters: {_availableParameters.Count}   |   Selected for conversion: {_selectedParameters.Count}";
        }
    }
}
