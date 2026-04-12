// Tool Name: View Crop Target Views Window
// Description: Selects one or more views with grouping by sheet and search/filter support.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-08
// Revit Version: 2020

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;

namespace AJTools.UI.ViewCrop
{
    /// <summary>
    /// Interaction logic for ViewCropTargetViewsWindow.xaml
    /// </summary>
    public partial class ViewCropTargetViewsWindow : Window
    {
        private readonly List<ViewCropTargetViewItem> _items;
        private readonly ICollectionView _collectionView;

        internal ViewCropTargetViewsWindow(IEnumerable<ViewCropTargetViewItem> items)
        {
            InitializeComponent();

            _items = (items ?? Enumerable.Empty<ViewCropTargetViewItem>()).ToList();
            foreach (ViewCropTargetViewItem item in _items)
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }

            _collectionView = CollectionViewSource.GetDefaultView(_items);
            _collectionView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ViewCropTargetViewItem.GroupName)));
            _collectionView.Filter = FilterItem;

            ViewsListBox.ItemsSource = _collectionView;
            SelectedViewIds = new List<ElementId>();

            UpdateSelectionCount();
        }

        internal IList<ElementId> SelectedViewIds { get; private set; }

        private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _collectionView.Refresh();
            UpdateSelectionCount();
        }

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            foreach (ViewCropTargetViewItem item in _items)
            {
                if (item.CanSelect && FilterItem(item))
                    item.IsSelected = true;
            }

            UpdateSelectionCount();
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            foreach (ViewCropTargetViewItem item in _items)
            {
                item.IsSelected = false;
            }

            UpdateSelectionCount();
        }

        private void OnRun(object sender, RoutedEventArgs e)
        {
            List<ElementId> selected = _items
                .Where(i => i.CanSelect && i.IsSelected && i.ViewId != null)
                .GroupBy(i => i.ViewId.IntegerValue)
                .Select(g => g.First().ViewId)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Select at least one supported view.",
                    "View Crop",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SelectedViewIds = selected;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool FilterItem(object obj)
        {
            ViewCropTargetViewItem item = obj as ViewCropTargetViewItem;
            if (item == null)
                return false;

            string search = (SearchTextBox?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(search))
                return true;

            return Contains(item.ViewName, search)
                || Contains(item.ViewTypeName, search)
                || Contains(item.SheetNumber, search)
                || Contains(item.SheetName, search)
                || Contains(item.GroupName, search)
                || Contains(item.StatusText, search);
        }

        private static bool Contains(string source, string term)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(term))
                return false;

            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                source,
                term,
                CompareOptions.IgnoreCase) >= 0;
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewCropTargetViewItem.IsSelected))
            {
                UpdateSelectionCount();
            }
        }

        private void UpdateSelectionCount()
        {
            int selected = _items.Count(i => i.CanSelect && i.IsSelected);
            int supported = _items.Count(i => i.CanSelect);
            int visible = _collectionView.Cast<object>().Count();

            SelectionCountText.Text = $"Selected: {selected}  |  Supported views: {supported}  |  Visible rows: {visible}";
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (ViewCropTargetViewItem item in _items)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            base.OnClosed(e);
        }
    }
}
