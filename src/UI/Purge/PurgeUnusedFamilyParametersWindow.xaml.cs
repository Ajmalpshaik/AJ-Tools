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

namespace AJTools.UI.Purge
{
    public partial class PurgeUnusedFamilyParametersWindow : Window
    {
        private const string DialogTitle = "Purge Unused Family Parameters";

        private readonly Document _doc;
        private readonly ObservableCollection<FamilyParameterPurgeItem> _rows;
        private readonly ICollectionView _rowsView;
        private IList<string> _limitations;

        public PurgeUnusedFamilyParametersWindow(Document doc)
        {
            _doc = doc;
            _rows = new ObservableCollection<FamilyParameterPurgeItem>();
            _limitations = new List<string>();

            InitializeComponent();

            _rowsView = CollectionViewSource.GetDefaultView(_rows);
            _rowsView.Filter = FilterRows;
            dgParameters.ItemsSource = _rowsView;

            Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnWindowLoaded;
            ScanFamilyParameters();
        }

        private bool FilterRows(object obj)
        {
            var row = obj as FamilyParameterPurgeItem;
            if (row == null)
            {
                return false;
            }

            string searchText = txtSearch.Text != null ? txtSearch.Text.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                if ((row.ParameterName ?? string.Empty)
                    .IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            string selectedStatus = GetSelectedStatusTag();
            return MatchesStatus(row, selectedStatus);
        }

        private static bool MatchesStatus(FamilyParameterPurgeItem row, string statusTag)
        {
            switch (statusTag)
            {
                case "SafeToPurge":
                    return row.Status == ParameterPurgeStatus.SafeToPurge;
                case "PossiblyUnused":
                    return row.Status == ParameterPurgeStatus.PossiblyUnused;
                case "InUse":
                    return row.Status == ParameterPurgeStatus.InUse;
                case "CannotDelete":
                    return row.Status == ParameterPurgeStatus.CannotDelete;
                default:
                    return true;
            }
        }

        private string GetSelectedStatusTag()
        {
            var selected = cmbStatusFilter.SelectedItem as ComboBoxItem;
            return selected != null && selected.Tag != null
                ? selected.Tag.ToString()
                : "All";
        }

        private void OnScanClick(object sender, RoutedEventArgs e)
        {
            ScanFamilyParameters();
        }

        private void ScanFamilyParameters()
        {
            SetBusy(true);
            try
            {
                var scanner = new FamilyParameterScanService(_doc);
                FamilyParameterPurgeScanResult scanResult = scanner.Scan();

                _rows.Clear();
                foreach (FamilyParameterPurgeItem row in scanResult.Items)
                {
                    _rows.Add(row);
                }

                _limitations = scanResult.Limitations.ToList();
                _rowsView.Refresh();
                UpdateSummary();
                UpdateLimitations();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Scan failed:\n\n" + ex.Message,
                    DialogTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdateSummary()
        {
            txtTotalScanned.Text = _rows.Count.ToString();
            txtSafeCount.Text = _rows.Count(r => r.Status == ParameterPurgeStatus.SafeToPurge).ToString();
            txtPossibleCount.Text = _rows.Count(r => r.Status == ParameterPurgeStatus.PossiblyUnused).ToString();
            txtInUseCount.Text = _rows.Count(r => r.Status == ParameterPurgeStatus.InUse).ToString();
            txtCannotDeleteCount.Text = _rows.Count(r => r.Status == ParameterPurgeStatus.CannotDelete).ToString();
        }

        private void UpdateLimitations()
        {
            if (_limitations == null || _limitations.Count == 0)
            {
                txtLimitations.Text = "No additional limitations were reported.";
                return;
            }

            txtLimitations.Text = "- " + string.Join(Environment.NewLine + "- ", _limitations);
        }

        private void SetBusy(bool busy)
        {
            Mouse.OverrideCursor = busy ? Cursors.Wait : null;

            btnScan.IsEnabled = !busy;
            btnSelectSafeOnly.IsEnabled = !busy;
            btnUnselectAll.IsEnabled = !busy;
            btnDeleteSelected.IsEnabled = !busy;
            txtSearch.IsEnabled = !busy;
            cmbStatusFilter.IsEnabled = !busy;
            dgParameters.IsEnabled = !busy;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _rowsView.Refresh();
        }

        private void OnStatusFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            _rowsView.Refresh();
        }

        private void OnSelectSafeOnlyClick(object sender, RoutedEventArgs e)
        {
            foreach (FamilyParameterPurgeItem row in _rows)
            {
                row.IsSelected = row.CanSelectForDeletion &&
                                 row.Status == ParameterPurgeStatus.SafeToPurge;
            }
        }

        private void OnUnselectAllClick(object sender, RoutedEventArgs e)
        {
            foreach (FamilyParameterPurgeItem row in _rows)
            {
                row.IsSelected = false;
            }
        }

        private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
        {
            List<FamilyParameterPurgeItem> selected = _rows
                .Where(r => r.IsSelected && r.CanSelectForDeletion)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "No deletable parameters are selected.",
                    DialogTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string confirmationMessage =
                "Selected parameters: " + selected.Count + Environment.NewLine +
                "Safe to Purge: " + selected.Count(r => r.Status == ParameterPurgeStatus.SafeToPurge) + Environment.NewLine +
                "Possibly Unused: " + selected.Count(r => r.Status == ParameterPurgeStatus.PossiblyUnused) + Environment.NewLine + Environment.NewLine +
                "Deleting family parameters may affect formulas, dimensions, nested family associations, and family behavior." + Environment.NewLine +
                "Continue?";

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                confirmationMessage,
                DialogTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            FamilyParameterPurgeDeleteResult deleteResult;
            SetBusy(true);
            try
            {
                var deleteService = new FamilyParameterDeleteService(_doc);
                deleteResult = deleteService.DeleteSelected(selected);
            }
            catch (Exception ex)
            {
                SetBusy(false);
                MessageBox.Show(
                    this,
                    "Delete failed:\n\n" + ex.Message,
                    DialogTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            finally
            {
                SetBusy(false);
            }

            ShowDeleteResult(deleteResult);
            ScanFamilyParameters();
        }

        private void ShowDeleteResult(FamilyParameterPurgeDeleteResult result)
        {
            if (result == null)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Delete completed.");
            sb.AppendLine();
            sb.AppendLine("Attempted: " + result.AttemptedCount);
            sb.AppendLine("Deleted: " + result.DeletedCount);
            sb.AppendLine("Failed: " + result.FailedCount);

            if (result.FailedCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Failure reasons:");

                foreach (FamilyParameterPurgeDeleteFailure failure in result.Failures.Take(12))
                {
                    sb.AppendLine("- " + failure.ParameterName + ": " + failure.Reason);
                }

                if (result.FailedCount > 12)
                {
                    sb.AppendLine("- +" + (result.FailedCount - 12) + " more.");
                }
            }

            MessageBox.Show(
                this,
                sb.ToString(),
                DialogTitle,
                MessageBoxButton.OK,
                result.FailedCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
