#region Metadata
/*
 * Tool Name     : Smart MEP Tagging - Settings (Window)
 * File Name     : SmartMepTagSettingsWindow.xaml.cs
 * Purpose       : WPF settings window - enable/disable and prioritise the MEP categories that
 *                 Smart MEP Tags will process, showing each category's element count in the model,
 *                 plus the skip-vertical-runs rule shared with Create Tags and Stack Tags.
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
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;
using AJTools.Services.SmartTag;
using AJTools.Utils;

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
        /// Whether vertical ducts, pipes and cable trays should be skipped when tagging.
        /// Only meaningful when DialogResult is true.
        /// </summary>
        /// <remarks>
        /// The same value the Create Tags settings window shows - stored once with the Fix Tag Clash
        /// settings, which is the one tagging store that survives closing Revit. Changing it in either
        /// window changes it for Smart MEP Tags, Create Tags and Stack Tags alike. The command owns
        /// reading and writing it; this window only shows the tick box.
        /// </remarks>
        public bool SkipVerticalRuns { get; private set; }

        /// <summary>Shortest run worth tagging, mm. 0 = no minimum. Meaningful only when DialogResult is true.</summary>
        public double MinLengthMm { get; private set; }

        /// <summary>Whether the width/height minimums apply. Meaningful only when DialogResult is true.</summary>
        public bool FilterBySize { get; private set; }

        /// <summary>Minimum width, mm. 0 = no minimum. Meaningful only when DialogResult is true.</summary>
        public double MinWidthMm { get; private set; }

        /// <summary>Minimum height, mm. 0 = no minimum. Meaningful only when DialogResult is true.</summary>
        public double MinHeightMm { get; private set; }

        /// <summary>
        /// Creates the settings window.
        /// </summary>
        /// <param name="rows">Category rows prebuilt by the command, in display order.</param>
        /// <param name="currentSkipVerticalRuns">Skip-vertical-runs value currently stored.</param>
        /// <param name="sizeSettings">Size rules currently stored, used to fill the Advanced tab.</param>
        public SmartMepTagSettingsWindow(
            IList<SmartTagCategoryRow> rows,
            bool currentSkipVerticalRuns,
            SmartTagSettingsState sizeSettings)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

            _rows = (rows ?? new List<SmartTagCategoryRow>())
                .Where(row => row != null)
                .ToList();

            foreach (SmartTagCategoryRow row in _rows)
                row.PropertyChanged += OnRowChanged;

            CategoryGrid.ItemsSource = _rows;
            SkipVerticalBox.IsChecked = currentSkipVerticalRuns;

            SmartTagSettingsState size = sizeSettings ?? SmartTagSettingsTracker.LoadSizeSettingsFromFile();

            MinLengthBox.Text = FormatMm(size.MinLengthMm);
            MinWidthBox.Text = FormatMm(size.MinWidthMm);
            MinHeightBox.Text = FormatMm(size.MinHeightMm);
            FilterBySizeBox.IsChecked = size.FilterBySize;

            // Wired here rather than only in XAML: FilterBySizeBox sets IsChecked above, and a XAML
            // Checked= handler would have fired during InitializeComponent's walk, before SizePanel
            // exists - a NullReferenceException on every open. Same trap recorded in the conventions
            // for the Reassign Level scope toggle.
            UpdateSizePanelEnabled();

            // Every other tabbed window in the suite does this - keeps the tab change animated.
            TabMotionHelper.AttachTabTransitions(this);

            Validate();
        }

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SmartTagCategoryRow.Enabled))
                Validate();
        }

        private void OnValueChanged(object sender, TextChangedEventArgs e)
        {
            Validate();
        }

        private void OnFilterBySizeToggled(object sender, RoutedEventArgs e)
        {
            UpdateSizePanelEnabled();
            Validate();
        }

        /// <summary>
        /// Greys the width/height boxes out when size filtering is off, so it is obvious they are not
        /// being applied rather than leaving live-looking numbers that do nothing.
        /// </summary>
        private void UpdateSizePanelEnabled()
        {
            if (SizePanel != null)
                SizePanel.IsEnabled = FilterBySizeBox.IsChecked == true;
        }

        /// <summary>
        /// Renders a stored mm value for a text box, without a trailing ".0" on whole numbers.
        /// </summary>
        private static string FormatMm(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads one mm box. Blank counts as 0 ("no minimum"), which is a real setting here, not an
        /// error - so an empty box must not block Save.
        /// </summary>
        private static bool TryReadMm(TextBox box, out double value)
        {
            value = 0.0;

            if (box == null)
                return false;

            string text = (box.Text ?? string.Empty).Trim();
            if (text.Length == 0)
                return true;

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return false;
            }

            return value >= 0.0;
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

        /// <summary>
        /// Puts every category back on, every priority back to its own default, and the shared
        /// vertical-run rule back on. Nothing is written until Save.
        /// </summary>
        private void OnReset(object sender, RoutedEventArgs e)
        {
            foreach (SmartTagCategoryRow row in _rows)
            {
                row.Enabled = true;
                row.PriorityText = row.DefaultPriorityText;
                row.UseLeader = true;
            }

            SkipVerticalBox.IsChecked = TagClashSettings.DefaultSkipVerticalRuns;

            MinLengthBox.Text = FormatMm(SmartTagSettingsTracker.DefaultMinLengthMm);
            MinWidthBox.Text = FormatMm(SmartTagSettingsTracker.DefaultMinWidthMm);
            MinHeightBox.Text = FormatMm(SmartTagSettingsTracker.DefaultMinHeightMm);
            FilterBySizeBox.IsChecked = SmartTagSettingsTracker.DefaultFilterBySize;

            UpdateSizePanelEnabled();
            Validate();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            // Commit any cell edit still in progress before reading the rows.
            CategoryGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            if (!Validate())
                return;

            SkipVerticalRuns = SkipVerticalBox.IsChecked == true;

            double minLength;
            double minWidth;
            double minHeight;
            TryReadMm(MinLengthBox, out minLength);
            TryReadMm(MinWidthBox, out minWidth);
            TryReadMm(MinHeightBox, out minHeight);

            MinLengthMm = minLength;
            MinWidthMm = minWidth;
            MinHeightMm = minHeight;
            FilterBySize = FilterBySizeBox.IsChecked == true;

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

            // The three mm boxes. Blank and 0 both mean "no minimum" and are valid; only a value that
            // is not a number, or is negative, is refused. The message names the box, since the error
            // shows outside the tabs and may be read while the other tab is in front.
            double parsed;

            if (!TryReadMm(MinLengthBox, out parsed))
            {
                ErrorText.Text = "Advanced tab: the shortest run must be a number of mm, 0 or more.";
                SaveButton.IsEnabled = false;
                return false;
            }

            if (FilterBySizeBox.IsChecked == true)
            {
                if (!TryReadMm(MinWidthBox, out parsed))
                {
                    ErrorText.Text = "Advanced tab: the minimum width must be a number of mm, 0 or more.";
                    SaveButton.IsEnabled = false;
                    return false;
                }

                if (!TryReadMm(MinHeightBox, out parsed))
                {
                    ErrorText.Text = "Advanced tab: the minimum height must be a number of mm, 0 or more.";
                    SaveButton.IsEnabled = false;
                    return false;
                }
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
        private bool _useLeader = true;
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

        /// <summary>
        /// This category's out-of-the-box priority, set by the command that builds the row. Kept so
        /// "Reset to defaults" can put the priority back without the window needing to know the
        /// per-category rules - those belong to SmartTagSettingsTracker, not to the UI.
        /// </summary>
        public string DefaultPriorityText { get; set; } = PriorityLow;

        /// <summary>Fixed priority choices shown by the ComboBox column.</summary>
        public IReadOnlyList<string> PriorityOptions { get; } =
            new[] { PriorityHigh, PriorityMedium, PriorityLow };

        /// <summary>
        /// Whether this category's tags get a leader. False places the tag beside the element.
        /// </summary>
        public bool UseLeader
        {
            get => _useLeader;
            set
            {
                if (_useLeader == value)
                    return;

                _useLeader = value;
                OnPropertyChanged(nameof(UseLeader));
            }
        }

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
