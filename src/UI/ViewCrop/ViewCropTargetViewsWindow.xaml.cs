#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropTargetViewsWindow.xaml.cs
 * Purpose       : Code-behind for the target-view picker (filter, group by sheet, validate selection).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API (ElementId), WPF
 *
 * Input         : Collection of ViewCropTargetViewItem rows.
 * Output        : SelectedViewIds list (de-duplicated by ElementId numeric value).
 *
 * Notes         :
 * - Modal dialog - no Revit API calls beyond reading ElementIds (skill rule).
 * - Search filter is culture-aware and case-insensitive.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Refactor/audit pass: ElementIdHelper for de-dup, metadata, version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;
using AJTools.Utils;

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
            SelectionErrorText.Text = string.Empty;
            _collectionView.Refresh();
            UpdateSelectionCount();
        }

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            SelectionErrorText.Text = string.Empty;
            foreach (ViewCropTargetViewItem item in _items)
            {
                if (item.CanSelect && FilterItem(item))
                    item.IsSelected = true;
            }

            UpdateSelectionCount();
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            SelectionErrorText.Text = string.Empty;
            foreach (ViewCropTargetViewItem item in _items)
            {
                item.IsSelected = false;
            }

            UpdateSelectionCount();
        }

        private void OnRun(object sender, RoutedEventArgs e)
        {
            SelectionErrorText.Text = string.Empty;

            List<ElementId> selected = _items
                .Where(i => i.CanSelect && i.IsSelected && i.ViewId != null)
                .GroupBy(i => ElementIdHelper.GetIntegerValue(i.ViewId))
                .Select(g => g.First().ViewId)
                .ToList();

            if (selected.Count == 0)
            {
                SelectionErrorText.Text = "Select at least one supported view.";
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
            int selected = _items.Count(i => i.IsSelected);
            int visible = _collectionView.Cast<object>().Count();

            SelectionCountText.Text = $"Selected: {selected}  |  Showing: {visible} of {_items.Count} views";
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
