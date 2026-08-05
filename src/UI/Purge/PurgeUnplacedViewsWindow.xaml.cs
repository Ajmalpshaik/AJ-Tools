// ==================================================
// Tool Name    : Purge Unplaced Views
// Purpose      : Convert Python shell purge workflow into AJ Tools C# Revit add-in.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-11
// Last Updated : 2026-05-11
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Converted from Interactive Python Shell script.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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
    public partial class PurgeUnplacedViewsWindow : Window
    {
        private readonly Document _doc;
        private readonly ElementId _activeViewId;
        private readonly UnplacedViewPurgeMode _mode;
        private readonly string _dialogTitle;
        private readonly ObservableCollection<UnplacedViewPurgeItem> _rows;
        private readonly ICollectionView _rowsView;

        public PurgeUnplacedViewsWindow(
            Document doc,
            ElementId activeViewId,
            UnplacedViewPurgeMode mode)
        {
            _doc = doc;
            _activeViewId = activeViewId ?? ElementId.InvalidElementId;
            _mode = mode;
            _dialogTitle = mode.GetToolTitle();
            _rows = new ObservableCollection<UnplacedViewPurgeItem>();

            InitializeComponent();

            // A scan/delete pumps the dispatcher to repaint its progress bar, and that pump runs a
            // real Win32 message loop - so the title-bar X and Esc DO reach this window mid-run
            // (measured, see ProgressReporter's header). Disabling the buttons does not cover those.
            // Subscribed before the motion helpers so this veto is already set when they run.
            Closing += RefuseCloseWhileBusy;

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);
            ConfigureForMode();

            _rowsView = CollectionViewSource.GetDefaultView(_rows);
            _rowsView.Filter = FilterRows;
            dgViews.ItemsSource = _rowsView;

            Loaded += OnWindowLoaded;
        }

        public bool OperationWasRun { get; private set; }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnWindowLoaded;
            ScanViews();
        }

        private bool FilterRows(object obj)
        {
            var row = obj as UnplacedViewPurgeItem;
            if (row == null)
            {
                return false;
            }

            string searchText = txtSearch.Text != null ? txtSearch.Text.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(searchText) &&
                (row.ViewName ?? string.Empty).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (!string.Equals(row.ViewKind, _mode.GetViewKind(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return MatchesStatus(row, GetSelectedTag(cmbStatusFilter));
        }

        private static bool MatchesStatus(UnplacedViewPurgeItem row, string statusTag)
        {
            switch (statusTag)
            {
                case "SafeToPurge":
                    return row.Status == UnplacedViewPurgeStatus.SafeToPurge;
                case "Skipped":
                    return row.Status == UnplacedViewPurgeStatus.Skipped;
                case "CannotDelete":
                    return row.Status == UnplacedViewPurgeStatus.CannotDelete;
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
            ScanViews();
        }

        private void ScanViews()
        {
            SetBusy(true);

            // Every candidate costs a trial delete inside a rolled-back transaction, so this can run for
            // several seconds. The work stays on Revit's UI thread exactly as before - this only makes
            // the window repaint part-way through it. See ProgressReporter's header.
            var progress = new ProgressReporter(ScanProgressBar, ProgressText);

            try
            {
                ClearRows();

                var service = new UnplacedViewPurgeService(_doc, _activeViewId, _mode);

                progress.Begin(1, "Collecting views...");
                IList<UnplacedViewPurgeItem> scanResult = service.Scan(delegate(int done, int total)
                {
                    progress.Report(done, total, "Checking view " + done + " of " + total + "...");
                });

                foreach (UnplacedViewPurgeItem row in scanResult)
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
            foreach (UnplacedViewPurgeItem row in _rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }

            _rows.Clear();
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UnplacedViewPurgeItem.IsSelected))
            {
                UpdateSummary();
            }
        }

        private void UpdateSummary()
        {
            txtFoundCount.Text = _rows.Count.ToString();
            txtTargetKindCount.Text = _rows.Count(r => string.Equals(r.ViewKind, _mode.GetViewKind(), StringComparison.OrdinalIgnoreCase)).ToString();
            txtSafeCount.Text = _rows.Count(r => r.Status == UnplacedViewPurgeStatus.SafeToPurge).ToString();
            txtBlockedCount.Text = _rows.Count(r => r.Status != UnplacedViewPurgeStatus.SafeToPurge).ToString();
            txtSelectedCount.Text = _rows.Count(r => r.IsSelected && r.CanSelectForDeletion).ToString();
        }

        /// <summary>True while a scan or delete is running on the UI thread.</summary>
        private bool _isBusy;

        private void RefuseCloseWhileBusy(object sender, CancelEventArgs e)
        {
            if (_isBusy)
            {
                e.Cancel = true;
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            Mouse.OverrideCursor = busy ? Cursors.Wait : null;

            btnScan.IsEnabled = !busy;
            btnSelectSafeOnly.IsEnabled = !busy;
            btnUnselectAll.IsEnabled = !busy;
            btnDeleteSelected.IsEnabled = !busy;
            txtSearch.IsEnabled = !busy;
            cmbKindFilter.IsEnabled = !busy;
            cmbStatusFilter.IsEnabled = !busy;
            dgViews.IsEnabled = !busy;
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
            foreach (UnplacedViewPurgeItem row in _rows)
            {
                row.IsSelected = row.CanSelectForDeletion;
            }
        }

        private void OnUnselectAllClick(object sender, RoutedEventArgs e)
        {
            foreach (UnplacedViewPurgeItem row in _rows)
            {
                row.IsSelected = false;
            }
        }

        private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
        {
            List<UnplacedViewPurgeItem> selected = _rows
                .Where(r => r.IsSelected && r.CanSelectForDeletion)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "No safe unplaced " + _mode.GetViewKindPlural().ToLowerInvariant() + " are selected.",
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

            UnplacedViewPurgeResult deleteResult;
            SetBusy(true);
            try
            {
                var service = new UnplacedViewPurgeService(_doc, _activeViewId, _mode);
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
            ScanViews();
        }

        private static string BuildConfirmationMessage(
            ICollection<UnplacedViewPurgeItem> selected,
            UnplacedViewPurgeMode mode)
        {
            return
                "Selected " + mode.GetViewKindPlural().ToLowerInvariant() + ": " + selected.Count + Environment.NewLine + Environment.NewLine +
                "This permanently deletes the selected views and any Revit-owned dependent view markers." + Environment.NewLine +
                "Linked model data and views placed on sheets are not included." + Environment.NewLine + Environment.NewLine +
                "Continue?";
        }

        private void ShowPurgeReport(UnplacedViewPurgeResult result)
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

        private static void AppendIssues(StringBuilder sb, string heading, IList<UnplacedViewPurgeIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine(heading);

            foreach (UnplacedViewPurgeIssue issue in issues.Take(12))
            {
                sb.AppendLine("- " + issue.ViewName + " [" + issue.ViewIdValue + "]: " + issue.Reason);
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
            txtTargetKindLabel.Text = _mode.GetViewKindPlural();
            lblKindFilter.Visibility = System.Windows.Visibility.Collapsed;
            cmbKindFilter.Visibility = System.Windows.Visibility.Collapsed;
        }
    }
}
