#region Metadata
/*
 * Tool Name     : Colorize
 * File Name     : ColorizeWindow.xaml.cs
 * Purpose       : WPF code-behind for the Colorize window — mirrors FilterProWindow.xaml.cs's
 *                 category/parameter/value cascade and multi-view apply scope, but drops all
 *                 filter-naming logic and the rule-type step, and reduces the footer to a single
 *                 "Shuffle Colors" action that colorizes matched elements directly, applying its own
 *                 Transaction on every click (window stays open). No ParameterFilterElement is
 *                 created here or anywhere downstream.
 *
 * Author        : Ajmal P.S.
 * Version       : 2.2.0
 *
 * Created Date  : 2026-07-02
 * Last Updated  : 2026-07-02
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, WPF, System.Threading.Tasks
 *
 * Input         : Active View, active Project document.
 * Output        : Element graphics overrides applied directly on every Shuffle Colors click; HasChanges
 *                 is true for CmdColorize once at least one click has applied changes.
 *
 * Notes         :
 * - Category/parameter/value loading and search+sort are ported directly from FilterProWindow.xaml.cs
 *   so both tools behave identically for element matching (always an exact-match Equals rule per
 *   selected value — there is no rule-type step here, unlike Filter Pro).
 * - Parameter selection is OPTIONAL here (unlike Filter Pro, which requires one): leaving it
 *   unselected colorizes the selected categories as a whole via ColorizeApplier's category-only path.
 * - "Shuffle Colors" always randomizes colours (RandomColors = true), matching Filter Pro's own
 *   Shuffle Colors button — there is no separate deterministic-colour mode in the UI, same as Filter Pro.
 * - Owns its own Transaction per click via GraphicsCommandService.ExecuteSummaryTransaction +
 *   ColorizeApplier.ApplyColorizeToViews (same pattern FilterProWindow uses for its own action
 *   buttons) — clicking Shuffle Colors does not close the window, so it can be clicked repeatedly to
 *   keep re-shuffling colours; only Close ends the dialog.
 *
 * Changelog     :
 * v1.0.0 (2026-07-02) - Initial release (custom dark-chrome layout, active view only).
 * v2.0.0 (2026-07-02) - Rebuilt to mirror FilterProWindow.xaml.cs: search+sort on all three lists,
 *                       multi-view apply scope, fill pattern selection. Removed the Naming Convention
 *                       and Create/Apply To View paths entirely.
 * v2.1.0 (2026-07-02) - Shuffle Colors now applies directly (owns its own Transaction) instead of
 *                       closing the dialog for CmdColorize to apply — it can be clicked repeatedly;
 *                       only Close ends the dialog.
 * v2.2.0 (2026-07-02) - Removed the Rule Type step entirely; selected values now always match with an
 *                       exact Equals rule.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models;
using AJTools.Models.GraphicsTools;
using AJTools.Services.Colorize;
using AJTools.Services.FilterPro;
using AJTools.Services.GraphicsTools;

namespace AJTools.UI
{
    /// <summary>
    /// Colorize settings window — Filter Pro's Selection/Apply UX, single Shuffle Colors action,
    /// no saved filter involved.
    /// </summary>
    public partial class ColorizeWindow : Window
    {
        private readonly Document _doc;
        private readonly View _activeView;
        private readonly FilterProDataProvider _dataProvider;

        private readonly List<FilterCategoryItem> _allCategories = new List<FilterCategoryItem>();
        private readonly List<FilterParameterItem> _allParameters = new List<FilterParameterItem>();
        private readonly List<FilterValueItem> _currentValues = new List<FilterValueItem>();
        private List<ApplyViewItem> _allViews = new List<ApplyViewItem>();
        private List<PatternItem> _patterns = new List<PatternItem>();

        private bool _isLoadingParameters;
        private bool _isLoadingValues;
        private bool _madeChanges;

        public ColorizeWindow(Document doc, View activeView)
        {
            InitializeComponent();

            _doc = doc;
            _activeView = activeView;
            _dataProvider = new FilterProDataProvider(_doc);

            WireEvents();
            SetActiveViewName();
            LoadCategories();
            LoadViewsForApply();
            LoadPatterns();
            UpdateStatus("Select one or more categories to begin.");
            UpdateApplyScopeLabel();
        }

        // Exposed to CmdColorize so it can return Succeeded when at least one Shuffle Colors click applied changes.
        internal bool HasChanges => _madeChanges;

        #region Wiring

        private void WireEvents()
        {
            close_button.Click += (s, e) => DialogResult = _madeChanges;
            shuffle_colors_button.Click += ShuffleColorsButton_Click;
            refresh_views_button.Click += (s, e) => LoadViewsForApply();

            categories_listbox.SelectionChanged += async (s, e) => { await LoadParameters(); };
            parameters_listbox.SelectionChanged += async (s, e) => { await LoadValues(); };
            views_listbox.SelectionChanged += (s, e) => UpdateApplyScopeLabel();

            apply_active_radio.Checked += (s, e) =>
            {
                views_listbox.IsEnabled = false;
                UpdateApplyScopeLabel();
            };
            apply_multiple_radio.Checked += (s, e) =>
            {
                views_listbox.IsEnabled = true;
                UpdateApplyScopeLabel();
            };

            value_search_textbox.TextChanged += (s, e) => ApplyValueFilters();
            value_sort_combobox.SelectionChanged += (s, e) => ApplyValueFilters();
            category_search_textbox.TextChanged += (s, e) => ApplyCategoryFilters();
            category_sort_combobox.SelectionChanged += (s, e) => ApplyCategoryFilters();
            parameter_search_textbox.TextChanged += (s, e) => ApplyParameterFilters();
            parameter_sort_combobox.SelectionChanged += (s, e) => ApplyParameterFilters();
        }

        private void SetActiveViewName()
        {
            active_view_name_text.Text = _activeView != null ? _activeView.Name : "(none)";
        }

        #endregion

        #region Categories / Parameters / Values (ported from FilterProWindow)

        private void LoadCategories()
        {
            try
            {
                var cats = _dataProvider.GetFilterableCategories();
                _allCategories.Clear();
                _allCategories.AddRange(cats);

                categories_listbox.DisplayMemberPath = "Name";
                ApplyCategoryFilters();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading categories: {ex.Message}");
            }
        }

        private void ApplyCategoryFilters()
        {
            if (_allCategories == null) return;

            var source = _allCategories.AsEnumerable();

            string term = category_search_textbox.Text;
            if (!string.IsNullOrWhiteSpace(term))
            {
                source = source.Where(c => c.Name != null && c.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (category_sort_combobox.SelectedItem is ComboBoxItem selectedSort && selectedSort.Tag is string sortTag)
            {
                source = sortTag == "za" ? source.OrderByDescending(c => c.Name) : source.OrderBy(c => c.Name);
            }

            categories_listbox.ItemsSource = source.ToList();
        }

        private async Task LoadParameters()
        {
            if (_isLoadingParameters) return;

            _isLoadingParameters = true;
            loading_indicator.Visibility = System.Windows.Visibility.Visible;
            try
            {
                var selectedCategories = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().ToList();

                if (!selectedCategories.Any())
                {
                    _allParameters.Clear();
                    parameters_listbox.ItemsSource = null;
                    values_listbox.ItemsSource = null;
                    UpdateStatus("Select one or more categories.");
                    return;
                }

                await Task.Yield(); // Let the UI render the loading overlay.

                var catIds = selectedCategories.Select(c => c.Id).ToList();
                var parameters = _dataProvider.GetParametersForCategories(catIds);

                _allParameters.Clear();
                _allParameters.AddRange(parameters);

                parameters_listbox.DisplayMemberPath = "Name";
                ApplyParameterFilters();

                UpdateStatus("Select a parameter to colorize by value, or leave it unselected to colorize whole categories.");
                await LoadValues();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading parameters: {ex.Message}");
            }
            finally
            {
                _isLoadingParameters = false;
                loading_indicator.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void ApplyParameterFilters()
        {
            if (_allParameters == null) return;

            var source = _allParameters.AsEnumerable();

            string term = parameter_search_textbox.Text;
            if (!string.IsNullOrWhiteSpace(term))
            {
                source = source.Where(p => p.Name != null && p.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (parameter_sort_combobox.SelectedItem is ComboBoxItem selectedSort && selectedSort.Tag is string sortTag)
            {
                source = sortTag == "za" ? source.OrderByDescending(p => p.Name) : source.OrderBy(p => p.Name);
            }

            parameters_listbox.ItemsSource = source.ToList();
        }

        private async Task LoadValues()
        {
            if (_isLoadingValues) return;

            _isLoadingValues = true;
            values_listbox.ItemsSource = null;
            loading_indicator.Visibility = System.Windows.Visibility.Visible;

            try
            {
                var param = parameters_listbox.SelectedItem as FilterParameterItem;
                var catIds = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().Select(c => c.Id).ToList();

                if (param == null || !catIds.Any())
                {
                    UpdateStatus(param == null
                        ? "No parameter selected — the selected categories will be colorized as a whole."
                        : "Select a parameter to load its values.");
                    return;
                }

                UpdateStatus("Collecting values...");
                await Task.Yield();

                bool hitScanLimit;
                var collectedValues = _dataProvider.GetValues(param, catIds, out hitScanLimit);

                _currentValues.Clear();
                _currentValues.AddRange(collectedValues);

                values_listbox.ItemsSource = collectedValues;
                values_listbox.DisplayMemberPath = "Display";
                ApplyValueFilters();

                UpdateStatus(hitScanLimit
                    ? $"Loaded {collectedValues.Count} unique value(s). Stopped after scanning 10,000 elements for performance."
                    : $"Loaded {collectedValues.Count} unique value(s).");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading values: {ex.Message}");
            }
            finally
            {
                _isLoadingValues = false;
                loading_indicator.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void ApplyValueFilters()
        {
            if (_currentValues == null) return;

            var source = _currentValues.AsEnumerable();

            string term = value_search_textbox.Text;
            if (!string.IsNullOrWhiteSpace(term))
            {
                source = source.Where(v => v.Display.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (value_sort_combobox.SelectedItem is ComboBoxItem selectedSort && selectedSort.Tag is string sortTag)
            {
                source = sortTag == "za" ? source.OrderByDescending(v => v.Display) : source.OrderBy(v => v.Display);
            }

            values_listbox.ItemsSource = source.ToList();
        }

        #endregion

        #region Apply scope + patterns (ported from FilterProWindow)

        private void LoadViewsForApply()
        {
            try
            {
                var previouslySelected = new HashSet<int>(
                    views_listbox.SelectedItems.Cast<ApplyViewItem>().Select(v => v.Id.IntegerValue));

                var views = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v =>
                        v != null &&
                        !v.IsTemplate &&
                        v.ViewType != ViewType.Internal &&
                        v.ViewType != ViewType.ProjectBrowser &&
                        v.ViewType != ViewType.SystemBrowser &&
                        v.ViewType != ViewType.Schedule &&
                        v.ViewType != ViewType.Undefined)
                    .Select(v => new ApplyViewItem(v.Id, v.Name, v.ViewType))
                    .OrderBy(v => v.ViewType)
                    .ThenBy(v => v.Name)
                    .ToList();

                _allViews = views;
                views_listbox.ItemsSource = _allViews;

                if (previouslySelected.Any())
                {
                    views_listbox.SelectedItems.Clear();
                    foreach (ApplyViewItem item in _allViews)
                    {
                        if (previouslySelected.Contains(item.Id.IntegerValue))
                            views_listbox.SelectedItems.Add(item);
                    }
                }

                views_listbox.IsEnabled = apply_multiple_radio.IsChecked == true;
                UpdateApplyScopeLabel();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading views: {ex.Message}");
            }
        }

        private void LoadPatterns()
        {
            try
            {
                var patterns = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .Select(p =>
                    {
                        var fp = p.GetFillPattern();
                        bool isSolid = fp != null && fp.IsSolidFill;
                        string name = isSolid ? "Solid Fill" : p.Name;
                        return new PatternItem(p.Id, name);
                    })
                    .OrderBy(p => p.Name)
                    .ToList();

                _patterns = patterns;
                pattern_combo.ItemsSource = _patterns;

                var solid = _patterns.FirstOrDefault(p => string.Equals(p.Name, "Solid Fill", StringComparison.OrdinalIgnoreCase));
                if (solid != null)
                    pattern_combo.SelectedItem = solid;
                else if (_patterns.Any())
                    pattern_combo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading patterns: {ex.Message}");
            }
        }

        private ElementId GetSelectedPatternId()
        {
            if (pattern_combo?.SelectedItem is PatternItem item)
                return item.Id;
            return ElementId.InvalidElementId;
        }

        private void UpdateApplyScopeLabel()
        {
            if (apply_scope_text == null || apply_tab == null) return;

            if (apply_active_radio.IsChecked == true)
            {
                string viewName = _activeView != null ? _activeView.Name : "None";
                apply_scope_text.Text = $"Apply: Active View ({viewName})";
                apply_tab.Header = "Apply (Active View)";
                views_listbox.IsEnabled = false;
            }
            else
            {
                int count = views_listbox.SelectedItems.Count;
                apply_scope_text.Text = count > 0 ? $"Apply: {count} selected view(s)" : "Apply: Selected Views (none)";
                apply_tab.Header = count > 0 ? $"Apply ({count} Views)" : "Apply (Selected Views)";
                views_listbox.IsEnabled = true;
            }
        }

        private List<View> ResolveTargetViews()
        {
            if (apply_active_radio.IsChecked == true)
            {
                if (_activeView == null)
                {
                    TaskDialog.Show("Validation", "There is no active view to colorize.");
                    return new List<View>();
                }

                return new List<View> { _activeView };
            }

            var selectedItems = views_listbox.SelectedItems.Cast<ApplyViewItem>().ToList();
            if (!selectedItems.Any())
            {
                TaskDialog.Show("Validation", "Please select at least one view when using 'Selected Views'.");
                return new List<View>();
            }

            var results = new List<View>();
            foreach (var item in selectedItems)
            {
                var view = _doc.GetElement(item.Id) as View;
                if (view != null)
                    results.Add(view);
            }

            if (!results.Any())
                TaskDialog.Show("Validation", "Selected views could not be resolved.");

            return results;
        }

        #endregion

        #region Shuffle Colors (the only apply action — colorizes and applies in one click)

        private void ShuffleColorsButton_Click(object sender, RoutedEventArgs e)
        {
            var selection = BuildColorizeSelection();
            if (selection == null)
                return;

            List<View> targetViews = ResolveTargetViews();
            if (!targetViews.Any())
                return;

            var skipped = new List<string>();
            GraphicsOperationSummary summary;

            try
            {
                summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    _doc,
                    "AJ Tools - Colorize",
                    () => ColorizeApplier.ApplyColorizeToViews(_doc, targetViews, selection, skipped));
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Colorize Error", $"An unexpected error occurred:\n\n{ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
                return;
            }

            if (summary.HasChanges)
            {
                _madeChanges = true;
            }

            UpdateStatus(BuildResultStatus(summary, skipped));
        }

        private static string BuildResultStatus(GraphicsOperationSummary summary, IList<string> skipped)
        {
            int skippedCount = skipped?.Count ?? 0;
            string status = $"Attempted: {summary.Attempted} | Applied: {summary.Applied} | Skipped: {summary.Skipped}";

            if (skippedCount > 0)
                status += $"  —  {string.Join("; ", skipped)}";

            return status;
        }

        private FilterSelection BuildColorizeSelection()
        {
            var catIds = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().Select(c => c.Id).ToList();
            if (!catIds.Any())
            {
                TaskDialog.Show("Validation", "Please select at least one category.");
                return null;
            }

            var param = parameters_listbox.SelectedItem as FilterParameterItem;
            var selectedValues = values_listbox.SelectedItems.Cast<FilterValueItem>().ToList();

            if (param != null && selectedValues.Count == 0)
            {
                TaskDialog.Show("Validation", "Please select at least one value, or clear the parameter to colorize whole categories.");
                return null;
            }

            if (color_proj_lines_checkbox.IsChecked != true &&
                color_proj_patterns_checkbox.IsChecked != true &&
                color_cut_lines_checkbox.IsChecked != true &&
                color_cut_patterns_checkbox.IsChecked != true &&
                color_halftone_checkbox.IsChecked != true)
            {
                TaskDialog.Show("Validation", "Enable at least one graphics option (lines, patterns, or halftone).");
                return null;
            }

            List<FilterValueItem> valuesForColorize = param == null ? new List<FilterValueItem>() : selectedValues;

            return new FilterSelection
            {
                CategoryIds = catIds,
                Parameter = param,
                Values = valuesForColorize,
                RuleType = RuleTypes.EqualsRule,
                CaseSensitive = false,
                RandomColors = true,
                ColorProjectionLines = color_proj_lines_checkbox.IsChecked == true,
                ColorProjectionPatterns = color_proj_patterns_checkbox.IsChecked == true,
                ColorCutLines = color_cut_lines_checkbox.IsChecked == true,
                ColorCutPatterns = color_cut_patterns_checkbox.IsChecked == true,
                ColorHalftone = color_halftone_checkbox.IsChecked == true,
                PatternId = GetSelectedPatternId(),
                ApplyGraphics = true
            };
        }

        #endregion

        private void UpdateStatus(string message)
        {
            status_text.Text = message;
        }
    }
}
