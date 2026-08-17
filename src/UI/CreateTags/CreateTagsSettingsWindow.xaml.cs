#region Metadata
/*
 * Tool Name     : Create Tags - Settings (Window)
 * File Name     : CreateTagsSettingsWindow.xaml.cs
 * Purpose       : WPF settings window - enable/disable the categories Create Tags can pick from,
 *                 and the minimum element length below which an element is skipped.
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
 * Input         : Prebuilt category rows (label, in-model count, enabled) and the current minimum
 *                 length (mm), from the command.
 * Output        : The same rows plus the validated MinLengthMm - read by the command after
 *                 DialogResult == true.
 *
 * Notes         :
 * - Pure UI. No collectors, no tracker access, no transaction - the command counts elements,
 *   builds the rows, and owns saving.
 * - Modal window: shown with ShowDialog() from inside IExternalCommand.Execute.
 * - Validation is live and inline: unticking every category, or an invalid length, disables Save
 *   with a message instead of closing the window with an error popup.
 * - Accepts both "1000" and "1000," style input; comma or dot both read as the decimal separator
 *   depending on locale (same technique as ArrangeTagsSettingsWindow).
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
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
using AJTools.Utils;

namespace AJTools.UI.CreateTags
{
    /// <summary>
    /// Interaction logic for CreateTagsSettingsWindow.xaml
    /// </summary>
    public partial class CreateTagsSettingsWindow : Window
    {
        private const double MinLengthFloorMm = 0.0;

        private readonly List<CreateTagsCategoryRow> _rows;

        /// <summary>
        /// The out-of-the-box minimum length, handed in by the command so "Reset to defaults" does not
        /// have to hard-code a number the settings tracker already owns.
        /// </summary>
        private readonly double _defaultMinLengthMm;

        /// <summary>Out-of-the-box distance from the element, used by Reset.</summary>
        private readonly double _defaultTagOffsetMm;

        /// <summary>
        /// The rows with the user's edits, in the same order they were passed in.
        /// Only meaningful when DialogResult is true.
        /// </summary>
        public IReadOnlyList<CreateTagsCategoryRow> Rows => _rows;

        /// <summary>
        /// The validated minimum length (mm). Only meaningful when DialogResult is true.
        /// </summary>
        public double MinLengthMm { get; private set; }

        /// <summary>
        /// The validated distance from the element, mm on the printed sheet. Only meaningful when
        /// DialogResult is true.
        /// </summary>
        public double TagOffsetMm { get; private set; }

        /// <summary>
        /// Whether vertical ducts, pipes and cable trays should be skipped when tagging.
        /// Only meaningful when DialogResult is true.
        /// </summary>
        /// <remarks>
        /// Shown here because it is a tagging rule, but stored with the Fix Tag Clash settings - that
        /// is the one tagging store that survives closing Revit, unlike this tool's own in-memory
        /// tracker. The command owns reading and writing it; this window only shows the tick box.
        /// </remarks>
        public bool SkipVerticalRuns { get; private set; }

        /// <summary>
        /// Creates the settings window.
        /// </summary>
        /// <param name="rows">Category rows prebuilt by the command, in display order.</param>
        /// <param name="currentMinLengthMm">Minimum length currently stored in settings.</param>
        /// <param name="currentSkipVerticalRuns">Skip-vertical-runs value currently stored.</param>
        /// <param name="defaultMinLengthMm">The out-of-the-box minimum length, used by Reset.</param>
        public CreateTagsSettingsWindow(
            IList<CreateTagsCategoryRow> rows,
            double currentMinLengthMm,
            bool currentSkipVerticalRuns,
            double defaultMinLengthMm,
            double currentTagOffsetMm,
            double defaultTagOffsetMm)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

            _rows = (rows ?? new List<CreateTagsCategoryRow>())
                .Where(row => row != null)
                .ToList();

            foreach (CreateTagsCategoryRow row in _rows)
                row.PropertyChanged += OnRowChanged;

            CategoryGrid.ItemsSource = _rows;

            _defaultMinLengthMm = defaultMinLengthMm >= 0 ? defaultMinLengthMm : 1000.0;

            double start = currentMinLengthMm >= 0 ? currentMinLengthMm : _defaultMinLengthMm;
            MinLengthBox.Text = Format(start);

            _defaultTagOffsetMm = defaultTagOffsetMm > 0 ? defaultTagOffsetMm : 300.0;
            OffsetBox.Text = Format(currentTagOffsetMm > 0 ? currentTagOffsetMm : _defaultTagOffsetMm);

            SkipVerticalBox.IsChecked = currentSkipVerticalRuns;

            Validate();
        }

        /// <summary>
        /// Puts every category back on, the minimum length back to its default, and the shared
        /// vertical-run rule back on. Nothing is written until Save.
        /// </summary>
        private void OnReset(object sender, RoutedEventArgs e)
        {
            foreach (CreateTagsCategoryRow row in _rows)
                row.Enabled = true;

            MinLengthBox.Text = Format(_defaultMinLengthMm);
            OffsetBox.Text = Format(_defaultTagOffsetMm);
            SkipVerticalBox.IsChecked = TagClashSettings.DefaultSkipVerticalRuns;
            Validate();
        }

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CreateTagsCategoryRow.Enabled))
                Validate();
        }

        private void OnMinLengthChanged(object sender, TextChangedEventArgs e)
        {
            Validate();
        }

        private void OnTagAll(object sender, RoutedEventArgs e)
        {
            foreach (CreateTagsCategoryRow row in _rows)
                row.Enabled = true;
        }

        private void OnTagNone(object sender, RoutedEventArgs e)
        {
            foreach (CreateTagsCategoryRow row in _rows)
                row.Enabled = false;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            CategoryGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (!Validate())
                return;

            SkipVerticalRuns = SkipVerticalBox.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Checks that at least one category is ticked and the length box holds a valid, non-negative
        /// number, refreshes the inline message, and enables or disables Save. Never shows a popup and
        /// never closes the window.
        /// </summary>
        private bool Validate()
        {
            if (_rows.Count == 0)
            {
                return Reject("No supported categories were found.");
            }

            if (!_rows.Any(row => row.Enabled))
            {
                return Reject("Tick at least one category, or Create Tags will have nothing to pick from.");
            }

            string raw = MinLengthBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return Reject("Enter a minimum length in mm (0 means no minimum).");

            if (!TryParseNumber(raw, out double parsed))
                return Reject("That is not a number. Type a value such as 1000 or 0.");

            if (parsed < MinLengthFloorMm)
                return Reject("Minimum length can't be negative.");

            MinLengthMm = parsed;

            // Distance from the element. Unlike the minimum length, 0 is NOT valid here - a tag sitting
            // exactly on its element is unreadable, and there is no sensible reason to ask for it.
            string offsetRaw = OffsetBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(offsetRaw))
                return Reject("Enter how far the tag should sit from the element, in mm.");

            if (!TryParseNumber(offsetRaw, out double offsetParsed))
                return Reject("The distance from the element is not a number. Type a value such as 300.");

            if (offsetParsed <= 0)
                return Reject("The distance from the element must be more than 0 - a tag cannot sit on top of it.");

            TagOffsetMm = offsetParsed;

            ErrorText.Text = string.Empty;
            SaveButton.IsEnabled = true;
            return true;
        }

        private bool Reject(string message)
        {
            ErrorText.Text = message;
            SaveButton.IsEnabled = false;
            return false;
        }

        private static bool TryParseNumber(string raw, out double value)
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Format(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// One category row in the Create Tags settings grid. Built by the command, edited by the
    /// window (Enabled only), read back by the command after Save.
    /// </summary>
    public sealed class CreateTagsCategoryRow : INotifyPropertyChanged
    {
        private bool _enabled;

        public CreateTagsCategoryRow(BuiltInCategory category, string categoryLabel, int countInModel)
        {
            Category = category;
            CategoryLabel = categoryLabel ?? string.Empty;
            CountInModel = countInModel;
        }

        public BuiltInCategory Category { get; }

        public string CategoryLabel { get; }

        public int CountInModel { get; }

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

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
