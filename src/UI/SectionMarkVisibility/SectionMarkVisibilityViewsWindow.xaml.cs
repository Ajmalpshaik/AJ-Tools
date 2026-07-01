#region Metadata
/*
 * Tool Name     : Section Mark Visibility
 * File Name     : SectionMarkVisibilityViewsWindow.xaml.cs
 * Purpose       : Code-behind for the multiple target-view selection dialog (grouped, searchable).
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
 * Input         : List of selectable plan views
 * Output        : User-selected view ids
 *
 * Notes         :
 * - Light code-behind only; no Revit model changes here.
 *
 * Changelog     :
 * v1.0.0 (2026-05-24) - Initial release.
 * v1.2.0 (2026-06-30) - Cleanup pass: removed dead constructor loop; metadata block.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using AJTools.Models.SectionMarkVisibility;

namespace AJTools.UI.SectionMarkVisibility
{
    /// <summary>
    /// Interaction logic for SectionMarkVisibilityViewsWindow.xaml
    /// </summary>
    public partial class SectionMarkVisibilityViewsWindow : Window
    {
        private readonly ICollectionView _collectionView;
        private readonly IList<SectionMarkVisibilityViewItem> _allItems;

        internal IList<ElementId> SelectedViewIds { get; private set; }

        internal SectionMarkVisibilityViewsWindow(IList<SectionMarkVisibilityViewItem> items)
        {
            InitializeComponent();

            _allItems = items ?? new List<SectionMarkVisibilityViewItem>();

            // Group and bind items in WPF ListBox
            _collectionView = CollectionViewSource.GetDefaultView(_allItems);
            _collectionView.GroupDescriptions.Add(new PropertyGroupDescription("GroupName"));
            _collectionView.Filter = FilterViewItems;
            ViewsListBox.ItemsSource = _collectionView;

            UpdateSelectionCount();
        }

        private bool FilterViewItems(object obj)
        {
            if (!(obj is SectionMarkVisibilityViewItem item))
                return false;

            string query = (SearchTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return true;

            // Search by View Name, Type Name, Sheet Number, or Sheet Name (case-insensitive)
            return (item.ViewName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.ViewTypeName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.SheetNumber ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.SheetName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            _collectionView?.Refresh();
            UpdateSelectionCount();
        }

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            var filtered = _collectionView.Cast<SectionMarkVisibilityViewItem>().ToList();
            foreach (var item in filtered)
            {
                if (item.CanSelect)
                {
                    item.IsSelected = true;
                }
            }
            ViewsListBox.Refresh(); // Force ListBox UI repaint
            UpdateSelectionCount();
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            var filtered = _collectionView.Cast<SectionMarkVisibilityViewItem>().ToList();
            foreach (var item in filtered)
            {
                if (item.CanSelect)
                {
                    item.IsSelected = false;
                }
            }
            ViewsListBox.Refresh(); // Force ListBox UI repaint
            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            if (SelectionCountText == null) return;

            int selected = _allItems.Count(i => i.IsSelected);
            int total = _allItems.Count(i => i.CanSelect);
            SelectionCountText.Text = $"Selected: {selected} / {total} supported views";
        }

        private void OnSelect(object sender, RoutedEventArgs e)
        {
            SelectionErrorText.Text = string.Empty;

            SelectedViewIds = _allItems
                .Where(i => i.IsSelected)
                .Select(i => i.ViewId)
                .ToList();

            if (SelectedViewIds.Count == 0)
            {
                SelectionErrorText.Text = "Please select at least one view to process.";
                return;
            }

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
    /// Helper extension to refresh ListBox visual containers when bindings are updated.
    /// </summary>
    internal static class ListBoxExtensions
    {
        public static void Refresh(this ItemsControl itemsControl)
        {
            if (itemsControl == null) return;
            itemsControl.Items.Refresh();
        }
    }
}
