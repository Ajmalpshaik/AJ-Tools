// Tool Name: Pin Elements UI
// Description: Code-behind for grouped pin/unpin selection dialog.
// Author: Ajmal P.S.
// Version: 1.3.0
// Last Updated: 2026-07-13
// Revit Version: 2020

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using AJTools.Models.PinTools;
using AJTools.Services.PinTools;

namespace AJTools.UI
{
    /// <summary>
    /// Interaction logic for PinElementsWindow.xaml.
    /// </summary>
    public partial class PinElementsWindow : Window
    {
        private readonly Document _doc;
        private readonly View _activeView;
        private readonly bool _isSheetContext;
        private readonly ObservableCollection<PinCategoryItem> _sheetItems = new ObservableCollection<PinCategoryItem>();
        private readonly ObservableCollection<PinCategoryItem> _modelItems = new ObservableCollection<PinCategoryItem>();

        public PinElementsWindow(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _activeView = _doc.ActiveView;
            _isSheetContext = PinElementsService.IsSheetContext(_activeView);

            InitializeComponent();

            SheetCategoryListBox.ItemsSource = _sheetItems;
            ModelCategoryListBox.ItemsSource = _modelItems;

            if (ActiveSheetOnlyRadioButton != null)
                ActiveSheetOnlyRadioButton.Checked += OnSheetScopeChanged;

            if (AllSheetsRadioButton != null)
                AllSheetsRadioButton.Checked += OnSheetScopeChanged;

            LoadItems();
            UpdateUiState();
        }

        internal bool HasExecutedOperation { get; private set; }

        private IEnumerable<PinCategoryItem> ActiveItems => _isSheetContext ? _sheetItems : _modelItems;

        private bool IncludeAllSheets =>
            _isSheetContext
            && AllSheetsRadioButton != null
            && AllSheetsRadioButton.IsChecked == true;

        private string SheetScopeLabel => IncludeAllSheets ? "All Sheets" : "Active Sheet Only";

        private void LoadItems()
        {
            if (_isSheetContext)
            {
                ContextText.Text = "Context: Active view is a sheet.";
                ModelItemsGroupBox.Visibility = System.Windows.Visibility.Collapsed;
                SheetScopePanel.Visibility = System.Windows.Visibility.Visible;
                LoadSheetItems(preserveSelection: false);
                return;
            }

            ContextText.Text = "Context: Active view is not a sheet. Model groups from the project.";
            SheetItemsGroupBox.Visibility = System.Windows.Visibility.Collapsed;
            SheetScopePanel.Visibility = System.Windows.Visibility.Collapsed;
            LoadModelItems();
        }

        private void LoadSheetItems(bool preserveSelection)
        {
            var previousSelection = preserveSelection
                ? _sheetItems.ToDictionary(item => item.Group, item => item.IsChecked)
                : null;

            UnwireItemEvents(_sheetItems);
            _sheetItems.Clear();

            foreach (PinTargetDefinition definition in PinElementsService.GetSheetTargetDefinitions())
            {
                int candidateCount = PinElementsService.CountCandidates(
                    _doc,
                    _activeView,
                    definition.Group,
                    IncludeAllSheets);

                bool isChecked = definition.DefaultSelected;
                if (previousSelection != null && previousSelection.TryGetValue(definition.Group, out bool wasChecked))
                    isChecked = wasChecked;

                var item = new PinCategoryItem(
                    definition.Group,
                    definition.Name,
                    definition.Description,
                    candidateCount,
                    isChecked);

                item.PropertyChanged += OnItemPropertyChanged;
                _sheetItems.Add(item);
            }
        }

        private void LoadModelItems()
        {
            UnwireItemEvents(_modelItems);
            _modelItems.Clear();

            foreach (PinTargetDefinition definition in PinElementsService.GetModelTargetDefinitions())
            {
                int candidateCount = PinElementsService.CountCandidates(_doc, _activeView, definition.Group);
                var item = new PinCategoryItem(
                    definition.Group,
                    definition.Name,
                    definition.Description,
                    candidateCount,
                    definition.DefaultSelected);

                item.PropertyChanged += OnItemPropertyChanged;
                _modelItems.Add(item);
            }
        }

        private void UnwireItemEvents(IEnumerable<PinCategoryItem> items)
        {
            if (items == null)
                return;

            foreach (PinCategoryItem item in items)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        private void OnSheetScopeChanged(object sender, RoutedEventArgs e)
        {
            if (!_isSheetContext || !IsInitialized)
                return;

            LoadSheetItems(preserveSelection: true);
            UpdateUiState();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PinCategoryItem.IsChecked))
                UpdateUiState();
        }

        private void OnSelectAllClick(object sender, RoutedEventArgs e)
        {
            foreach (PinCategoryItem item in ActiveItems)
                item.IsChecked = true;

            UpdateUiState();
        }

        private void OnClearAllClick(object sender, RoutedEventArgs e)
        {
            foreach (PinCategoryItem item in ActiveItems)
                item.IsChecked = false;

            UpdateUiState();
        }

        private void OnPinClick(object sender, RoutedEventArgs e)
        {
            ExecuteOperation(pinState: true);
        }

        private void OnUnpinClick(object sender, RoutedEventArgs e)
        {
            ExecuteOperation(pinState: false);
        }

        private void ExecuteOperation(bool pinState)
        {
            IList<PinTargetGroup> selectedGroups = ActiveItems
                .Where(i => i.IsChecked)
                .Select(i => i.Group)
                .ToList();

            if (selectedGroups.Count == 0)
            {
                UpdateUiState("Select at least one group.");
                return;
            }

            PinOperationSummary summary = PinElementsService.ApplyPinState(
                _doc,
                _activeView,
                selectedGroups,
                pinState,
                IncludeAllSheets);

            string operationWord = pinState ? "Pinned" : "Unpinned";
            string stateWord = pinState ? "already pinned" : "already unpinned";
            string scopePrefix = _isSheetContext ? SheetScopeLabel + " | " : string.Empty;

            string status = summary.TargetedCount == 0
                ? scopePrefix + "No elements found in the selected groups."
                : string.Format(
                    "{0}{1}: {2}/{3} updated | Unchanged ({4}): {5} | Skipped: {6}",
                    scopePrefix,
                    operationWord,
                    summary.UpdatedCount,
                    summary.TargetedCount,
                    stateWord,
                    summary.UnchangedCount,
                    summary.SkippedCount);

            HasExecutedOperation = true;
            UpdateUiState(status);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = HasExecutedOperation;
            Close();
        }

        /// <summary>
        /// The category lists sit inside the window's own outer ScrollViewer (so the two group boxes
        /// combined can scroll once they exceed MaxHeight), but ModernListBox's control template has
        /// its own internal ScrollViewer too. WPF's ScrollViewer always marks a MouseWheel event as
        /// handled once it processes it, even when there's nothing to scroll inside - so without this,
        /// the outer scrollbar could only be dragged by hand, never scrolled with the mouse wheel while
        /// hovering over a list. Re-raising the event sourced at the ListBox lets it bubble past the
        /// ListBox (skipping its own template's ScrollViewer) up to the window's outer ScrollViewer.
        /// </summary>
        private void OnListBoxPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || !(sender is ListBox listBox))
                return;

            var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent
            };
            listBox.RaiseEvent(forwarded);
            e.Handled = true;
        }

        private void UpdateUiState(string statusOverride = null)
        {
            List<PinCategoryItem> active = ActiveItems.ToList();
            int selectedGroupCount = active.Count(i => i.IsChecked);
            int candidateCount = active.Where(i => i.IsChecked).Sum(i => i.CandidateCount);

            if (_isSheetContext)
            {
                SelectionSummaryText.Text = string.Format(
                    "Scope: {0} | Selected groups: {1}/{2} | Candidate elements: {3}",
                    SheetScopeLabel,
                    selectedGroupCount,
                    active.Count,
                    candidateCount);
            }
            else
            {
                SelectionSummaryText.Text = string.Format(
                    "Selected groups: {0}/{1} | Candidate elements: {2}",
                    selectedGroupCount,
                    active.Count,
                    candidateCount);
            }

            PinButton.IsEnabled = selectedGroupCount > 0;
            UnpinButton.IsEnabled = selectedGroupCount > 0;
            SelectAllButton.IsEnabled = active.Count > 0;
            ClearAllButton.IsEnabled = active.Count > 0;

            if (!string.IsNullOrWhiteSpace(statusOverride))
            {
                StatusText.Text = statusOverride;
                return;
            }

            if (_isSheetContext)
            {
                StatusText.Text = selectedGroupCount > 0
                    ? string.Format("{0} mode ready.", SheetScopeLabel)
                    : "Select one or more groups.";
                return;
            }

            StatusText.Text = selectedGroupCount > 0
                ? "Ready to apply Pin or Unpin."
                : "Select one or more groups.";
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (PinCategoryItem item in _sheetItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            foreach (PinCategoryItem item in _modelItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            if (ActiveSheetOnlyRadioButton != null)
                ActiveSheetOnlyRadioButton.Checked -= OnSheetScopeChanged;

            if (AllSheetsRadioButton != null)
                AllSheetsRadioButton.Checked -= OnSheetScopeChanged;

            base.OnClosed(e);
        }
    }
}
