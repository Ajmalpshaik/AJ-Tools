#region Metadata
/*
 * Tool Name     : Auto MEP Dimension - Settings
 * File Name     : MepDimensionSettingsWindow.xaml.cs
 * Purpose       : Modal settings window for Auto MEP Dimension - service categories to measure, the
 *                 reference targets and whether each comes from this model or a linked model, how the
 *                 chain is drawn, and which runs are skipped.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF)
 *
 * Dependencies  : AJTools.UI.ModernStyles, AJTools.Models.Dimensioning, AJTools.Utils (WindowMotionHelper)
 *
 * Input         : The current settings, and the dimension type names found in the project.
 * Output        : Result settings, read by the command after ShowDialog() returns true.
 *
 * Notes         :
 * - Pure UI: no collectors, no transaction. The command owns the model work and the save.
 * - Validation happens INSIDE the window (inline message + Save disabled), never after ShowDialog
 *   returns - a popup after closing loses everything the user typed.
 * - Category tick boxes are built in code rather than XAML so no Checked handler can fire during
 *   InitializeComponent before its sibling controls exist.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using AJTools.Models.Dimensioning;
using AJTools.Utils;

namespace AJTools.UI.Dimensioning
{
    /// <summary>
    /// Settings window for Auto MEP Dimension.
    /// </summary>
    public partial class MepDimensionSettingsWindow : Window
    {
        private const string UseDefaultTypeLabel = "(project default)";

        private readonly ObservableCollection<MepTargetRow> _targetRows = new ObservableCollection<MepTargetRow>();
        private readonly List<CategoryCheck> _categoryChecks = new List<CategoryCheck>();
        private readonly IList<string> _dimensionTypeNames;

        /// <summary>The settings to save. Only meaningful once ShowDialog() returned true.</summary>
        public MepDimensionSettings ResultSettings { get; private set; }

        public MepDimensionSettingsWindow(MepDimensionSettings settings, IList<string> dimensionTypeNames)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

            _dimensionTypeNames = dimensionTypeNames ?? new List<string>();

            TargetGrid.ItemsSource = _targetRows;

            BuildCategoryChecks();
            BuildDimensionTypeList();

            SaveButton.Click += OnSave;
            CancelButton.Click += OnCancel;
            ResetButton.Click += OnReset;

            MinLengthBox.TextChanged += OnTextInputChanged;
            SearchBandBox.TextChanged += OnTextInputChanged;
            RowSpacingBox.TextChanged += OnTextInputChanged;

            Load(settings ?? MepDimensionSettings.CreateDefault());
            Validate();
        }

        // -----------------------------------------------------------------------------------------
        // Load / read
        // -----------------------------------------------------------------------------------------

        private void Load(MepDimensionSettings settings)
        {
            settings.Normalize();

            foreach (CategoryCheck check in _categoryChecks)
                check.Box.IsChecked = settings.MeasuredCategoryIds.Contains((int)check.Category);

            _targetRows.Clear();
            foreach (DimensionTargetKind kind in MepDimensionSettings.SupportedTargetKinds)
            {
                MepTargetRow row = new MepTargetRow(kind, GetTargetLabel(kind));
                row.SetScope(settings.GetScope(kind));
                row.PropertyChanged += OnRowChanged;
                _targetRows.Add(row);
            }

            SingleStringRadio.IsChecked = settings.ChainStyle == DimensionChainStyle.SingleString;
            RowPerRunRadio.IsChecked = settings.ChainStyle == DimensionChainStyle.RowPerRun;
            SeparateSegmentsRadio.IsChecked = settings.ChainStyle == DimensionChainStyle.SeparateSegments;

            BothSidesCheck.IsChecked = settings.DimensionBothSides;
            IncludeWidthCheck.IsChecked = settings.IncludeRunWidth;
            SkipVerticalCheck.IsChecked = settings.SkipVerticalRuns;
            SkipDimensionedCheck.IsChecked = settings.SkipAlreadyDimensioned;
            ShowReportCheck.IsChecked = settings.ShowReport;

            MinLengthBox.Text = Format(settings.MinimumRunLengthMm);
            RowSpacingBox.Text = Format(settings.RowSpacingMm);
            SearchBandBox.Text = Format(settings.SearchBandMm);

            SelectDimensionType(settings.DimensionTypeName);
            UpdateLinkWarning();
        }

        private MepDimensionSettings ReadFromControls()
        {
            MepDimensionSettings settings = MepDimensionSettings.CreateDefault();

            settings.MeasuredCategoryIds = _categoryChecks
                .Where(c => c.Box.IsChecked == true)
                .Select(c => (int)c.Category)
                .ToList();

            settings.Targets = _targetRows
                .Select(r => new MepDimensionTargetOption(r.Kind, r.ToScope()))
                .ToList();

            if (SeparateSegmentsRadio.IsChecked == true)
                settings.ChainStyle = DimensionChainStyle.SeparateSegments;
            else if (RowPerRunRadio.IsChecked == true)
                settings.ChainStyle = DimensionChainStyle.RowPerRun;
            else
                settings.ChainStyle = DimensionChainStyle.SingleString;

            settings.DimensionBothSides = BothSidesCheck.IsChecked == true;
            settings.IncludeRunWidth = IncludeWidthCheck.IsChecked == true;
            settings.SkipVerticalRuns = SkipVerticalCheck.IsChecked == true;
            settings.SkipAlreadyDimensioned = SkipDimensionedCheck.IsChecked == true;
            settings.ShowReport = ShowReportCheck.IsChecked == true;

            if (TryParseNumber(MinLengthBox.Text, out double minLength))
                settings.MinimumRunLengthMm = minLength;

            if (TryParseNumber(RowSpacingBox.Text, out double rowSpacing))
                settings.RowSpacingMm = rowSpacing;

            if (TryParseNumber(SearchBandBox.Text, out double band))
                settings.SearchBandMm = band;

            settings.DimensionTypeName = ReadSelectedDimensionType();

            return settings;
        }

        // -----------------------------------------------------------------------------------------
        // Building controls
        // -----------------------------------------------------------------------------------------

        private void BuildCategoryChecks()
        {
            foreach (BuiltInCategory category in MepDimensionSettings.SupportedMeasuredCategories)
            {
                CheckBox box = new CheckBox
                {
                    Content = GetCategoryLabel(category),
                    Style = (Style)FindResource("ModernCheckBox"),
                    Margin = new Thickness(0, 0, 8, 4),
                    VerticalAlignment = VerticalAlignment.Center
                };

                box.Checked += OnAnyInputChanged;
                box.Unchecked += OnAnyInputChanged;

                CategoryPanel.Children.Add(box);
                _categoryChecks.Add(new CategoryCheck { Category = category, Box = box });
            }
        }

        private void BuildDimensionTypeList()
        {
            DimensionTypeCombo.Items.Add(UseDefaultTypeLabel);
            foreach (string name in _dimensionTypeNames)
                DimensionTypeCombo.Items.Add(name);

            DimensionTypeCombo.SelectedIndex = 0;
        }

        private void SelectDimensionType(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                DimensionTypeCombo.SelectedIndex = 0;
                return;
            }

            foreach (object item in DimensionTypeCombo.Items)
            {
                if (string.Equals(item as string, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    DimensionTypeCombo.SelectedItem = item;
                    return;
                }
            }

            // The saved type is not in THIS project. Settings are one shared user-level file, so
            // quietly resetting to the default here would wipe the choice for every other project the
            // moment this window was opened. The name is kept as a selectable entry instead; the tool
            // falls back to the document default at run time and says so in its report.
            string preserved = name.Trim();
            DimensionTypeCombo.Items.Add(preserved);
            DimensionTypeCombo.SelectedItem = preserved;
        }

        private string ReadSelectedDimensionType()
        {
            string selected = DimensionTypeCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selected) ||
                string.Equals(selected, UseDefaultTypeLabel, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return selected;
        }

        // -----------------------------------------------------------------------------------------
        // Validation
        // -----------------------------------------------------------------------------------------

        private bool Validate()
        {
            if (!_categoryChecks.Any(c => c.Box.IsChecked == true))
                return Reject("Tick at least one service to dimension.");

            bool anyReference = _targetRows.Any(r =>
                r.Kind != DimensionTargetKind.MepRun && (r.UseCurrent || r.UseLinked));

            if (!anyReference)
                return Reject("Tick at least one reference to measure to - a wall, column, beam, floor, grid or level.");

            if (!TryParseNumber(RowSpacingBox.Text, out double rowSpacing) || rowSpacing < 1 || rowSpacing > 200)
                return Reject("The gap between stacked rows must be between 1 and 200 mm.");

            if (!TryParseNumber(MinLengthBox.Text, out double minLength) || minLength < 0)
                return Reject("Minimum run length must be 0 or more.");

            if (!TryParseNumber(SearchBandBox.Text, out double band) || band < 1 || band > 5000)
                return Reject("The search band must be between 1 and 5000 mm.");

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

        private void UpdateLinkWarning()
        {
            bool anyLinked = _targetRows.Any(r => r.UseLinked);

            LinkWarningText.Visibility = anyLinked
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        // -----------------------------------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------------------------------

        private void OnAnyInputChanged(object sender, RoutedEventArgs e)
        {
            Validate();
        }

        private void OnTextInputChanged(object sender, TextChangedEventArgs e)
        {
            Validate();
        }

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateLinkWarning();
            Validate();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            TargetGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (!Validate())
                return;

            ResultSettings = ReadFromControls();
            ResultSettings.Normalize();

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            // Load() rebuilds the rows, so the old ones and their handlers go with them.
            Load(MepDimensionSettings.CreateDefault());
            Validate();
        }

        // -----------------------------------------------------------------------------------------
        // Labels and parsing
        // -----------------------------------------------------------------------------------------

        internal static string GetCategoryLabel(BuiltInCategory category)
        {
            switch (category)
            {
                case BuiltInCategory.OST_DuctCurves: return "Ducts";
                case BuiltInCategory.OST_FlexDuctCurves: return "Flexible ducts";
                case BuiltInCategory.OST_PipeCurves: return "Pipes";
                case BuiltInCategory.OST_FlexPipeCurves: return "Flexible pipes";
                case BuiltInCategory.OST_CableTray: return "Cable trays";
                case BuiltInCategory.OST_Conduit: return "Conduits";
                default: return category.ToString();
            }
        }

        internal static string GetTargetLabel(DimensionTargetKind kind)
        {
            switch (kind)
            {
                case DimensionTargetKind.Wall: return "Walls";
                case DimensionTargetKind.StructuralColumn: return "Structural columns";
                case DimensionTargetKind.StructuralBeam: return "Structural beams";
                case DimensionTargetKind.ArchitecturalColumn: return "Architectural columns";
                case DimensionTargetKind.Floor: return "Floors / slabs";
                case DimensionTargetKind.Grid: return "Grids";
                case DimensionTargetKind.Level: return "Levels";
                case DimensionTargetKind.MepRun: return "Other service runs";
                default: return kind.ToString();
            }
        }

        private static bool TryParseNumber(string raw, out double value)
        {
            raw = raw?.Trim();

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private sealed class CategoryCheck
        {
            internal BuiltInCategory Category { get; set; }
            internal CheckBox Box { get; set; }
        }
    }

    /// <summary>
    /// One reference-target row: what it is, and whether it is read from this model, linked models,
    /// both, or neither.
    /// </summary>
    public sealed class MepTargetRow : INotifyPropertyChanged
    {
        private bool _useCurrent;
        private bool _useLinked;

        public MepTargetRow(DimensionTargetKind kind, string displayName)
        {
            Kind = kind;
            DisplayName = displayName ?? kind.ToString();
        }

        public DimensionTargetKind Kind { get; }

        public string DisplayName { get; }

        public bool UseCurrent
        {
            get => _useCurrent;
            set
            {
                if (_useCurrent == value)
                    return;

                _useCurrent = value;
                OnPropertyChanged(nameof(UseCurrent));
            }
        }

        public bool UseLinked
        {
            get => _useLinked;
            set
            {
                if (_useLinked == value)
                    return;

                _useLinked = value;
                OnPropertyChanged(nameof(UseLinked));
            }
        }

        internal void SetScope(DimensionModelScope scope)
        {
            UseCurrent = scope == DimensionModelScope.CurrentModel || scope == DimensionModelScope.CurrentAndLinked;
            UseLinked = scope == DimensionModelScope.LinkedModels || scope == DimensionModelScope.CurrentAndLinked;
        }

        internal DimensionModelScope ToScope()
        {
            if (UseCurrent && UseLinked)
                return DimensionModelScope.CurrentAndLinked;
            if (UseCurrent)
                return DimensionModelScope.CurrentModel;
            if (UseLinked)
                return DimensionModelScope.LinkedModels;

            return DimensionModelScope.None;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
