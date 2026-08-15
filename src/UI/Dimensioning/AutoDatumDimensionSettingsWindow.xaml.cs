#region Metadata
/*
 * Tool Name     : Automatic Dimension - Settings
 * File Name     : AutoDatumDimensionSettingsWindow.xaml.cs
 * Purpose       : Modal settings window for the Automatic Dimension (grids / levels) tools - which rows
 *                 are created, which side they sit on, their gaps and styles, which datums are used and
 *                 whether they may come from a linked model.
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
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AJTools.Models.Dimensioning;
using AJTools.Utils;

namespace AJTools.UI.Dimensioning
{
    /// <summary>
    /// Settings window for the Automatic Dimension (grids / levels) tools.
    /// </summary>
    public partial class AutoDatumDimensionSettingsWindow : Window
    {
        private const string UseDefaultTypeLabel = "(project default)";

        private const string ScopeNone = "Do not dimension";
        private const string ScopeCurrent = "This model only";
        private const string ScopeLinked = "Linked models only";
        private const string ScopeBoth = "This model and linked models";

        private readonly IList<string> _dimensionTypeNames;

        /// <summary>The settings to save. Only meaningful once ShowDialog() returned true.</summary>
        public AutoDatumDimensionSettings ResultSettings { get; private set; }

        public AutoDatumDimensionSettingsWindow(AutoDatumDimensionSettings settings, IList<string> dimensionTypeNames)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

            _dimensionTypeNames = dimensionTypeNames ?? new List<string>();

            BuildTypeCombo(IndividualTypeCombo);
            BuildTypeCombo(OverallTypeCombo);
            BuildScopeCombo(GridScopeCombo);
            BuildScopeCombo(LevelScopeCombo);

            SaveButton.Click += OnSave;
            CancelButton.Click += OnCancel;
            ResetButton.Click += OnReset;

            FirstOffsetBox.TextChanged += OnTextInputChanged;
            RowSpacingBox.TextChanged += OnTextInputChanged;

            IndividualRowCheck.Checked += OnAnyInputChanged;
            IndividualRowCheck.Unchecked += OnAnyInputChanged;
            OverallRowCheck.Checked += OnAnyInputChanged;
            OverallRowCheck.Unchecked += OnAnyInputChanged;

            GridScopeCombo.SelectionChanged += OnSelectionChanged;
            LevelScopeCombo.SelectionChanged += OnSelectionChanged;

            Load(settings ?? AutoDatumDimensionSettings.CreateDefault());
            Validate();
        }

        // -----------------------------------------------------------------------------------------
        // Load / read
        // -----------------------------------------------------------------------------------------

        private void Load(AutoDatumDimensionSettings settings)
        {
            settings.Normalize();

            IndividualRowCheck.IsChecked = settings.CreateIndividualRow;
            OverallRowCheck.IsChecked = settings.CreateOverallRow;
            SkipOverallForTwoCheck.IsChecked = settings.SkipOverallWhenTwoDatums;

            SidePrimaryRadio.IsChecked = settings.Side == DimensionPlacementSide.Primary;
            SideSecondaryRadio.IsChecked = settings.Side == DimensionPlacementSide.Secondary;
            SideBothRadio.IsChecked = settings.Side == DimensionPlacementSide.Both;

            FirstOffsetBox.Text = Format(settings.FirstRowOffsetMm);
            RowSpacingBox.Text = Format(settings.RowSpacingMm);

            SelectType(IndividualTypeCombo, settings.IndividualDimensionTypeName);
            SelectType(OverallTypeCombo, settings.OverallDimensionTypeName);

            SelectScope(GridScopeCombo, settings.GridScope);
            SelectScope(LevelScopeCombo, settings.LevelScope);

            StoryLevelsOnlyCheck.IsChecked = settings.StoryLevelsOnly;
            ExcludeBox.Text = settings.ExcludeNameFragments ?? string.Empty;

            SkipDimensionedCheck.IsChecked = settings.SkipAlreadyDimensioned;
            RequireCropCheck.IsChecked = settings.RequireCropBox;
            ShowReportCheck.IsChecked = settings.ShowReport;

            UpdateLinkWarning();
        }

        private AutoDatumDimensionSettings ReadFromControls()
        {
            AutoDatumDimensionSettings settings = AutoDatumDimensionSettings.CreateDefault();

            settings.CreateIndividualRow = IndividualRowCheck.IsChecked == true;
            settings.CreateOverallRow = OverallRowCheck.IsChecked == true;
            settings.SkipOverallWhenTwoDatums = SkipOverallForTwoCheck.IsChecked == true;

            if (SideBothRadio.IsChecked == true)
                settings.Side = DimensionPlacementSide.Both;
            else if (SideSecondaryRadio.IsChecked == true)
                settings.Side = DimensionPlacementSide.Secondary;
            else
                settings.Side = DimensionPlacementSide.Primary;

            if (TryParseNumber(FirstOffsetBox.Text, out double firstOffset))
                settings.FirstRowOffsetMm = firstOffset;

            if (TryParseNumber(RowSpacingBox.Text, out double spacing))
                settings.RowSpacingMm = spacing;

            settings.IndividualDimensionTypeName = ReadType(IndividualTypeCombo);
            settings.OverallDimensionTypeName = ReadType(OverallTypeCombo);

            settings.GridScope = ReadScope(GridScopeCombo);
            settings.LevelScope = ReadScope(LevelScopeCombo);

            settings.StoryLevelsOnly = StoryLevelsOnlyCheck.IsChecked == true;
            settings.ExcludeNameFragments = ExcludeBox.Text?.Trim() ?? string.Empty;

            settings.SkipAlreadyDimensioned = SkipDimensionedCheck.IsChecked == true;
            settings.RequireCropBox = RequireCropCheck.IsChecked == true;
            settings.ShowReport = ShowReportCheck.IsChecked == true;

            return settings;
        }

        // -----------------------------------------------------------------------------------------
        // Combo helpers
        // -----------------------------------------------------------------------------------------

        private void BuildTypeCombo(ComboBox combo)
        {
            combo.Items.Add(UseDefaultTypeLabel);
            foreach (string name in _dimensionTypeNames)
                combo.Items.Add(name);

            combo.SelectedIndex = 0;
        }

        private static void BuildScopeCombo(ComboBox combo)
        {
            combo.Items.Add(ScopeNone);
            combo.Items.Add(ScopeCurrent);
            combo.Items.Add(ScopeLinked);
            combo.Items.Add(ScopeBoth);
            combo.SelectedIndex = 1;
        }

        private static void SelectType(ComboBox combo, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                combo.SelectedIndex = 0;
                return;
            }

            foreach (object item in combo.Items)
            {
                if (string.Equals(item as string, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            // The saved type is not in THIS project. Settings are one shared user-level file, so
            // quietly resetting to the default here would wipe the choice for every other project the
            // moment this window was opened. The name is kept as a selectable entry instead; the tool
            // falls back to the document default at run time and says so in its report.
            string preserved = name.Trim();
            combo.Items.Add(preserved);
            combo.SelectedItem = preserved;
        }

        private static string ReadType(ComboBox combo)
        {
            string selected = combo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selected) ||
                string.Equals(selected, UseDefaultTypeLabel, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return selected;
        }

        private static void SelectScope(ComboBox combo, DimensionModelScope scope)
        {
            switch (scope)
            {
                case DimensionModelScope.None:
                    combo.SelectedItem = ScopeNone;
                    break;
                case DimensionModelScope.LinkedModels:
                    combo.SelectedItem = ScopeLinked;
                    break;
                case DimensionModelScope.CurrentAndLinked:
                    combo.SelectedItem = ScopeBoth;
                    break;
                default:
                    combo.SelectedItem = ScopeCurrent;
                    break;
            }
        }

        private static DimensionModelScope ReadScope(ComboBox combo)
        {
            string selected = combo.SelectedItem as string;

            if (string.Equals(selected, ScopeNone, StringComparison.Ordinal))
                return DimensionModelScope.None;
            if (string.Equals(selected, ScopeLinked, StringComparison.Ordinal))
                return DimensionModelScope.LinkedModels;
            if (string.Equals(selected, ScopeBoth, StringComparison.Ordinal))
                return DimensionModelScope.CurrentAndLinked;

            return DimensionModelScope.CurrentModel;
        }

        // -----------------------------------------------------------------------------------------
        // Validation
        // -----------------------------------------------------------------------------------------

        private bool Validate()
        {
            if (IndividualRowCheck.IsChecked != true && OverallRowCheck.IsChecked != true)
                return Reject("Tick at least one row to create - the chain, the overall, or both.");

            DimensionModelScope gridScope = ReadScope(GridScopeCombo);
            DimensionModelScope levelScope = ReadScope(LevelScopeCombo);

            if (gridScope == DimensionModelScope.None && levelScope == DimensionModelScope.None)
                return Reject("Choose where grids or levels come from - both are set to 'Do not dimension'.");

            if (!TryParseNumber(FirstOffsetBox.Text, out double firstOffset) || firstOffset < 0 || firstOffset > 200)
                return Reject("The gap to the first row must be a number between 0 and 200 mm.");

            if (!TryParseNumber(RowSpacingBox.Text, out double spacing) || spacing < 0 || spacing > 200)
                return Reject("The gap between rows must be a number between 0 and 200 mm.");

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
            DimensionModelScope gridScope = ReadScope(GridScopeCombo);
            DimensionModelScope levelScope = ReadScope(LevelScopeCombo);

            bool anyLinked =
                gridScope == DimensionModelScope.LinkedModels || gridScope == DimensionModelScope.CurrentAndLinked ||
                levelScope == DimensionModelScope.LinkedModels || levelScope == DimensionModelScope.CurrentAndLinked;

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

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateLinkWarning();
            Validate();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
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
            Load(AutoDatumDimensionSettings.CreateDefault());
            Validate();
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
    }
}
