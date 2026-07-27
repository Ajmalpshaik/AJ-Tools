#region Metadata
/*
 * Tool Name     : Smart MEP Tagging - Settings (Window)
 * File Name     : SmartMepTagSettingsWindow.xaml.cs
 * Purpose       : WPF settings window - enable/disable and prioritise the MEP categories that
 *                 Smart MEP Tags will process, showing each category's element count in the model.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF)
 *
 * Dependencies  : System.Windows (WPF), AJTools.UI.ModernStyles, Autodesk.Revit.DB (BuiltInCategory only)
 *
 * Input         : Prebuilt category rows (label, in-model count, enabled, priority) from the command.
 * Output        : The same rows with the user's edits - read by the command after DialogResult == true.
 *
 * Notes         :
 * - Pure UI. No collectors, no tracker access, no transaction - the command counts elements,
 *   builds the rows, and owns saving.
 * - Modal window: shown with ShowDialog() from inside IExternalCommand.Execute.
 * - Validation is live and inline: unticking every category disables Save with a message instead of
 *   closing the window with an error popup (which is what the old WinForms dialog did).
 * - Priority is a fixed-choice ComboBox (High/Medium/Low), so an invalid priority is impossible by
 *   construction - the old grid's "Select a valid priority" failure path no longer exists.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release. Replaces the WinForms DataGridView dialog in
 *                       CmdSmartMepTagSettings.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace AJTools.UI.SmartMepTag
{
    /// <summary>
    /// Interaction logic for SmartMepTagSettingsWindow.xaml
    /// </summary>
    public partial class SmartMepTagSettingsWindow : Window
    {
        private readonly List<SmartTagCategoryRow> _rows;

        /// <summary>
        /// The rows with the user's edits, in the same order they were passed in.
        /// Only meaningful when DialogResult is true.
        /// </summary>
        public IReadOnlyList<SmartTagCategoryRow> Rows => _rows;

        /// <summary>
        /// Creates the settings window.
        /// </summary>
        /// <param name="rows">Category rows prebuilt by the command, in display order.</param>
        public SmartMepTagSettingsWindow(IList<SmartTagCategoryRow> rows)
        {
            InitializeComponent();

            _rows = (rows ?? new List<SmartTagCategoryRow>())
                .Where(row => row != null)
                .ToList();

            foreach (SmartTagCategoryRow row in _rows)
                row.PropertyChanged += OnRowChanged;

            CategoryGrid.ItemsSource = _rows;
            Validate();
        }

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SmartTagCategoryRow.Enabled))
                Validate();
        }

        private void OnTagAll(object sender, RoutedEventArgs e)
        {
            foreach (SmartTagCategoryRow row in _rows)
                row.Enabled = true;
        }

        private void OnTagNone(object sender, RoutedEventArgs e)
        {
            foreach (SmartTagCategoryRow row in _rows)
                row.Enabled = false;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            // Commit any cell edit still in progress before reading the rows.
            CategoryGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            if (!Validate())
                return;

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Checks that at least one category is ticked, refreshes the inline message and
        /// enables or disables Save. Never shows a popup and never closes the window.
        /// </summary>
        private bool Validate()
        {
            if (_rows.Count == 0)
            {
                ErrorText.Text = "No supported categories were found.";
                SaveButton.IsEnabled = false;
                return false;
            }

            if (!_rows.Any(row => row.Enabled))
            {
                ErrorText.Text = "Tick at least one category, or Smart MEP Tag will have nothing to do.";
                SaveButton.IsEnabled = false;
                return false;
            }

            ErrorText.Text = string.Empty;
            SaveButton.IsEnabled = true;
            return true;
        }
    }

    /// <summary>
    /// One category row in the Smart MEP Tag settings grid. Built by the command, edited by the
    /// window (Enabled and PriorityText only), read back by the command after Save.
    /// </summary>
    public sealed class SmartTagCategoryRow : INotifyPropertyChanged
    {
        public const string PriorityHigh = "High";
        public const string PriorityMedium = "Medium";
        public const string PriorityLow = "Low";

        private bool _enabled;
        private string _priorityText = PriorityLow;

        public SmartTagCategoryRow(BuiltInCategory category, string categoryLabel, int countInModel)
        {
            Category = category;
            CategoryLabel = categoryLabel ?? string.Empty;
            CountInModel = countInModel;
        }

        public BuiltInCategory Category { get; }

        public string CategoryLabel { get; }

        public int CountInModel { get; }

        /// <summary>Fixed priority choices shown by the ComboBox column.</summary>
        public IReadOnlyList<string> PriorityOptions { get; } =
            new[] { PriorityHigh, PriorityMedium, PriorityLow };

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;

                _enabled = value;
                OnPropertyChanged(nameof(Enabled));
            }
        }

        public string PriorityText
        {
            get => _priorityText;
            set
            {
                string next = value ?? PriorityLow;
                if (_priorityText == next)
                    return;

                _priorityText = next;
                OnPropertyChanged(nameof(PriorityText));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
