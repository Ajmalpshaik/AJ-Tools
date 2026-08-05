// Tool Name: Purge Unused Elements UI (View Templates / Filters / Groups)
// Description: Code-behind for scanning, filtering, and deleting selected unused elements. Generalizes the
//              same shape as PurgeUnplacedViewsWindow for the three modes handled by UnusedElementPurgeService.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-07-28
// Revit Version: 2020 - latest
// Dependencies: Autodesk.Revit.DB, System.Windows

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using AJTools.Models.Purge;
using AJTools.Services.Purge;
using AJTools.Utils;

namespace AJTools.UI.Purge
{
    public partial class PurgeUnusedElementsWindow : Window
    {
        private readonly Document _doc;
        private readonly UnusedElementPurgeMode _mode;
        private readonly string _dialogTitle;
        private readonly ObservableCollection<UnusedElementPurgeItem> _rows;
        private readonly ICollectionView _rowsView;

        public PurgeUnusedElementsWindow(Document doc, UnusedElementPurgeMode mode)
        {
            _doc = doc;
            _mode = mode;
            _dialogTitle = mode.GetToolTitle();
            _rows = new ObservableCollection<UnusedElementPurgeItem>();

            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);
            ConfigureForMode();

            _rowsView = CollectionViewSource.GetDefaultView(_rows);
            _rowsView.Filter = FilterRows;
            dgElements.ItemsSource = _rowsView;

            Loaded += OnWindowLoaded;
        }

        public bool OperationWasRun { get; private set; }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnWindowLoaded;
            ScanElements();
        }

        private bool FilterRows(object obj)
        {
            var row = obj as UnusedElementPurgeItem;
            if (row == null)
            {
                return false;
            }

            string searchText = txtSearch.Text != null ? txtSearch.Text.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(searchText) &&
                (row.ElementName ?? string.Empty).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (_mode == UnusedElementPurgeMode.Groups)
            {
                string kindTag = GetSelectedTag(cmbKindFilter);
                if (!string.Equals(kindTag, "All", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(row.ElementKind, kindTag, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return MatchesStatus(row, GetSelectedTag(cmbStatusFilter));
        }

        private static bool MatchesStatus(UnusedElementPurgeItem row, string statusTag)
        {
            switch (statusTag)
            {
                case "SafeToPurge":
                    return row.Status == UnusedElementPurgeStatus.SafeToPurge;
                case "Skipped":
                    return row.Status == UnusedElementPurgeStatus.Skipped;
                case "CannotDelete":
                    return row.Status == UnusedElementPurgeStatus.CannotDelete;
                default:
                    return true;
            }
        }

        private static string GetSelectedTag(ComboBox comboBox)
        {
            var selected = comboBox != null ? comboBox.SelectedItem as ComboBoxItem : null;
            return selected != null && selected.Tag != null
                ? selected.Tag.ToString()
                : "All";
        }

        private void OnScanClick(object sender, RoutedEventArgs e)
        {
            ScanElements();
        }

        private void ScanElements()
        {
            SetBusy(true);

            // The scan trial-deletes every candidate inside a rolled-back transaction, so on a busy model
            // it can sit for several seconds. The work stays on Revit's UI thread exactly as before -
            // this only makes the window repaint part-way through it. See ProgressReporter's header.
            var progress = new ProgressReporter(ScanProgressBar, ProgressText);

            try
            {
                ClearRows();

                var service = new UnusedElementPurgeService(_doc, _mode);

                progress.Begin(1, "Collecting elements...");
                IList<UnusedElementPurgeItem> scanResult = service.Scan(delegate(int done, int total)
                {
                    progress.Report(done, total, "Checking element " + done + " of " + total + "...");
                });

                foreach (UnusedElementPurgeItem row in scanResult)
                {
                    row.PropertyChanged += OnRowPropertyChanged;
                    _rows.Add(row);
                }

                _rowsView.Refresh();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Scan failed:\n\n" + ex.Message,
                    _dialogTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // In the finally so the row disappears even if the scan threw.
                progress.End();
                SetBusy(false);
            }
        }

        private void ClearRows()
        {
            foreach (UnusedElementPurgeItem row in _rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }

            _rows.Clear();
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UnusedElementPurgeItem.IsSelected))
            {
                UpdateSummary();
            }
        }

        private void UpdateSummary()
        {
            txtFoundCount.Text = _rows.Count.ToString();
            txtTargetKindCount.Text = _rows.Count.ToString();
            txtSafeCount.Text = _rows.Count(r => r.Status == UnusedElementPurgeStatus.SafeToPurge).ToString();
            txtBlockedCount.Text = _rows.Count(r => r.Status != UnusedElementPurgeStatus.SafeToPurge).ToString();
            txtSelectedCount.Text = _rows.Count(r => r.IsSelected && r.CanSelectForDeletion).ToString();
        }

        private void SetBusy(bool busy)
        {
            Mouse.OverrideCursor = busy ? Cursors.Wait : null;

            btnScan.IsEnabled = !busy;
            btnSelectSafeOnly.IsEnabled = !busy;
            btnUnselectAll.IsEnabled = !busy;
            btnDeleteSelected.IsEnabled = !busy;
            txtSearch.IsEnabled = !busy;
            cmbKindFilter.IsEnabled = !busy;
            cmbStatusFilter.IsEnabled = !busy;
            dgElements.IsEnabled = !busy;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_rowsView != null)
            {
                _rowsView.Refresh();
            }
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_rowsView != null)
            {
                _rowsView.Refresh();
            }
        }

        private void OnSelectSafeOnlyClick(object sender, RoutedEventArgs e)
        {
            foreach (UnusedElementPurgeItem row in _rows)
            {
                row.IsSelected = row.CanSelectForDeletion;
            }
        }

        private void OnUnselectAllClick(object sender, RoutedEventArgs e)
        {
            foreach (UnusedElementPurgeItem row in _rows)
            {
                row.IsSelected = false;
            }
        }

        private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
        {
            List<UnusedElementPurgeItem> selected = _rows
                .Where(r => r.IsSelected && r.CanSelectForDeletion)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "No safe unused " + _mode.GetElementKindPlural().ToLowerInvariant() + " are selected.",
                    _dialogTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                BuildConfirmationMessage(selected, _mode),
                _dialogTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            UnusedElementPurgeResult deleteResult;
            SetBusy(true);
            try
            {
                var service = new UnusedElementPurgeService(_doc, _mode);
                deleteResult = service.DeleteSelected(selected, _rows.Count);
                OperationWasRun = true;
            }
            catch (Exception ex)
            {
                SetBusy(false);
                MessageBox.Show(
                    this,
                    "Delete failed:\n\n" + ex.Message,
                    _dialogTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            finally
            {
                SetBusy(false);
            }

            ShowPurgeReport(deleteResult);
            ScanElements();
        }

        private static string BuildConfirmationMessage(
            ICollection<UnusedElementPurgeItem> selected,
            UnusedElementPurgeMode mode)
        {
            return
                "Selected " + mode.GetElementKindPlural().ToLowerInvariant() + ": " + selected.Count + Environment.NewLine + Environment.NewLine +
                "This permanently deletes the selected elements." + Environment.NewLine + Environment.NewLine +
                "Continue?";
        }

        private void ShowPurgeReport(UnusedElementPurgeResult result)
        {
            if (result == null)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Purge report");
            sb.AppendLine();
            sb.AppendLine("Found: " + result.FoundCount);
            sb.AppendLine("Attempted: " + result.AttemptedCount);
            sb.AppendLine("Purged: " + result.DeletedCount);
            sb.AppendLine("Skipped: " + result.SkippedCount);
            sb.AppendLine("Failed: " + result.FailedCount);

            AppendIssues(sb, "Skipped reasons:", result.Skipped);
            AppendIssues(sb, "Failure reasons:", result.Failures);

            MessageBox.Show(
                this,
                sb.ToString(),
                _dialogTitle + " Report",
                MessageBoxButton.OK,
                result.FailedCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private static void AppendIssues(StringBuilder sb, string heading, IList<UnusedElementPurgeIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine(heading);

            foreach (UnusedElementPurgeIssue issue in issues.Take(12))
            {
                sb.AppendLine("- " + issue.ElementName + " [" + issue.ElementIdValue + "]: " + issue.Reason);
            }

            if (issues.Count > 12)
            {
                sb.AppendLine("- +" + (issues.Count - 12) + " more.");
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ConfigureForMode()
        {
            Title = _dialogTitle + " - AJ Tools";
            txtWindowTitle.Text = _dialogTitle;
            txtWindowDescription.Text = _mode.GetDescription();
            txtTargetKindLabel.Text = _mode.GetElementKindPlural();

            if (_mode == UnusedElementPurgeMode.Groups)
            {
                lblKindFilter.Visibility = System.Windows.Visibility.Visible;
                cmbKindFilter.Visibility = System.Windows.Visibility.Visible;
                cmbKindFilter.Items.Clear();
                cmbKindFilter.Items.Add(new ComboBoxItem { Content = "All", Tag = "All", IsSelected = true });
                cmbKindFilter.Items.Add(new ComboBoxItem { Content = "Model Groups", Tag = "Model Group" });
                cmbKindFilter.Items.Add(new ComboBoxItem { Content = "Detail Groups", Tag = "Detail Group" });
            }
            else
            {
                lblKindFilter.Visibility = System.Windows.Visibility.Collapsed;
                cmbKindFilter.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
    }
}
