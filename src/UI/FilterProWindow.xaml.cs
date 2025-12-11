// Tool Name: Filter Pro UI
// Description: WPF interface for building, applying, and ordering view filters with graphics.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Windows
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models;
using AJTools.Services.FilterPro;

namespace AJTools.UI
{
    /// <summary>
    /// Interaction logic for FilterProWindow.xaml
    /// </summary>
    public partial class FilterProWindow : Window
    {
        private readonly Document _doc;
        private readonly View _activeView;
        private readonly FilterProDataProvider _dataProvider;
        private readonly FilterProStateTracker _stateTracker;

        private readonly List<FilterValueItem> _currentValues = new List<FilterValueItem>();
        private bool _restoringState;
        private List<ApplyViewItem> _allViews = new List<ApplyViewItem>();
        private List<PatternItem> _patterns = new List<PatternItem>();
        private bool _madeChanges;

        public FilterProWindow(Document doc, View activeView)
        {
            InitializeComponent();
            _doc = doc;
            _activeView = activeView;
            _dataProvider = new FilterProDataProvider(_doc);
            _stateTracker = new FilterProStateTracker(_doc);

            WireEvents();
            SetActiveViewName();
            LoadCategories();
            LoadViewsForApply();
            LoadPatterns();
            RestoreLastSelection();

            if (_stateTracker.LastState == null)
                UpdateStatus("Select categories to begin.");

            UpdateApplyScopeLabel();
        }

        private void WireEvents()
        {
            close_button.Click += (s, e) =>
            {
                // Preserve dialog result so Revit keeps changes when the command ends.
                DialogResult = _madeChanges;
                Close();
            };
            create_button.Click += CreateButton_Click;
            apply_view_button.Click += ApplyViewButton_Click;
            shuffle_colors_button.Click += ShuffleColorsButton_Click;
            refresh_views_button.Click += (s, e) => LoadViewsForApply();

            // List selection changes
            categories_listbox.SelectionChanged += async (s, e) =>
            {
                await LoadParameters();
                UpdateNamePreview();
            };

            parameters_listbox.SelectionChanged += async (s, e) =>
            {
                await LoadValues();
                UpdateNamePreview();
            };

            values_listbox.SelectionChanged += (s, e) => UpdateNamePreview();
            views_listbox.SelectionChanged += (s, e) => UpdateApplyScopeLabel();

            // Naming convention changes
            prefix_textbox.TextChanged += (s, e) => UpdateNamePreview();
            suffix_textbox.TextChanged += (s, e) => UpdateNamePreview();
            separator_textbox.TextChanged += (s, e) => UpdateNamePreview();
            include_cat_checkbox.Checked += (s, e) => UpdateNamePreview();
            include_cat_checkbox.Unchecked += (s, e) => UpdateNamePreview();
            include_param_checkbox.Checked += (s, e) => UpdateNamePreview();
            include_param_checkbox.Unchecked += (s, e) => UpdateNamePreview();

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

            // Rule changes
            foreach (var rb in new[]
                     {
                         radio_equals, radio_not_equals, radio_contains, radio_not_contains,
                         radio_starts, radio_not_starts, radio_ends, radio_not_ends,
                         radio_has_value, radio_not_has_value
                     })
            {
                rb.Checked += (s, e) => UpdateNamePreview();
            }

            // Value filtering and sorting
            value_search_textbox.TextChanged += (s, e) => ApplyValueFilters();
            value_sort_combobox.SelectionChanged += (s, e) => ApplyValueFilters();
        }

        private void ApplyValueFilters()
        {
            if (_currentValues == null) return;

            var source = _currentValues.AsEnumerable();

            // Filter by search text
            string term = value_search_textbox.Text;
            if (!string.IsNullOrWhiteSpace(term))
            {
                source = source.Where(v =>
                    v.Display.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Sort
            if (value_sort_combobox.SelectedItem is ComboBoxItem selectedSort &&
                selectedSort.Tag is string sortTag)
            {
                if (sortTag == "za")
                    source = source.OrderByDescending(v => v.Display);
                else
                    source = source.OrderBy(v => v.Display);
            }

            values_listbox.ItemsSource = source.ToList();
        }

        private void SetActiveViewName()
        {
            active_view_name_text.Text = _activeView != null ? _activeView.Name : "(none)";
        }

        private void LoadViewsForApply()
        {
            try
            {
                var previouslySelected = new HashSet<int>(
                    views_listbox.SelectedItems
                        .Cast<ApplyViewItem>()
                        .Select(v => v.Id.IntegerValue));

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

                if (_restoringState)
                    return;

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

                // Default to Solid Fill if available
                var solid = _patterns.FirstOrDefault(p =>
                    string.Equals(p.Name, "Solid Fill", StringComparison.OrdinalIgnoreCase));
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

        private void UpdateNamePreview()
        {
            if (preview_text == null) return;

            var param = parameters_listbox.SelectedItem as FilterParameterItem;
            var values = values_listbox.SelectedItems.Cast<FilterValueItem>().ToList();

            string valueText = "Value";
            if (values.Count == 1)
                valueText = values[0].Display;
            else if (values.Count > 1)
                valueText = $"{values.Count} Values";

            string separator = string.IsNullOrWhiteSpace(separator_textbox.Text)
                ? "_"
                : separator_textbox.Text;

            var tempSelection = new FilterSelection
            {
                CategoryIds = categories_listbox.SelectedItems
                    .Cast<FilterCategoryItem>()
                    .Select(c => c.Id)
                    .ToList(),
                Parameter = param,
                Prefix = prefix_textbox.Text,
                Suffix = suffix_textbox.Text,
                IncludeCategory = include_cat_checkbox.IsChecked == true,
                IncludeParameter = include_param_checkbox.IsChecked == true,
                Separator = separator,
                CaseSensitive = case_sensitive_checkbox.IsChecked == true
            };

            var previewValue = new FilterValueItem(valueText, valueText, StorageType.String);

            preview_text.Text = FilterCreator.ComposeFilterName(tempSelection, previewValue, _doc);
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
                apply_scope_text.Text = count > 0
                    ? $"Apply: {count} selected view(s)"
                    : "Apply: Selected Views (none)";
                apply_tab.Header = count > 0 ? $"Apply ({count} Views)" : "Apply (Selected Views)";
                views_listbox.IsEnabled = true;
            }
        }

        private ElementId GetSelectedPatternId()
        {
            if (pattern_combo?.SelectedItem is PatternItem item)
                return item.Id;
            if (pattern_combo?.SelectedValue is ElementId eid)
                return eid;
            return ElementId.InvalidElementId;
        }

        private void RestorePatternSelection(ElementId savedId)
        {
            if (savedId == null || savedId == ElementId.InvalidElementId || pattern_combo == null)
                return;

            foreach (PatternItem item in pattern_combo.Items)
            {
                if (item.Id != null && item.Id.IntegerValue == savedId.IntegerValue)
                {
                    pattern_combo.SelectedItem = item;
                    return;
                }
            }
        }

        private PatternItem GetPatternItem(ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId) return null;
            return _patterns.FirstOrDefault(p => p.Id.IntegerValue == id.IntegerValue);
        }

        private List<ElementId> GetSelectedViewIds()
        {
            if (apply_active_radio.IsChecked == true)
            {
                if (_activeView != null)
                    return new List<ElementId> { _activeView.Id };
                return new List<ElementId>();
            }

            return views_listbox.SelectedItems
                .Cast<ApplyViewItem>()
                .Select(v => v.Id)
                .ToList();
        }

        private List<View> ResolveTargetViews(out List<ElementId> targetIds)
        {
            targetIds = new List<ElementId>();
            var results = new List<View>();

            if (apply_active_radio.IsChecked == true)
            {
                if (_activeView == null)
                {
                    TaskDialog.Show("Validation", "There is no active view to apply filters.");
                    return results;
                }

                targetIds.Add(_activeView.Id);
                results.Add(_activeView);
                return results;
            }

            var selectedItems = views_listbox.SelectedItems.Cast<ApplyViewItem>().ToList();
            if (!selectedItems.Any())
            {
                TaskDialog.Show("Validation", "Please select at least one view when using 'Selected Views'.");
                return results;
            }

            foreach (var item in selectedItems)
            {
                targetIds.Add(item.Id);
                var view = _doc.GetElement(item.Id) as View;
                if (view != null)
                    results.Add(view);
            }

            if (!results.Any())
                TaskDialog.Show("Validation", "Selected views could not be resolved.");

            return results;
        }

        private async void RestoreLastSelection()
        {
            var lastState = _stateTracker.LastState;
            if (lastState == null) return;

            string restoredMessage = "Restored previous Filter Pro selection.";
            _restoringState = true;
            try
            {
                await RestoreCategoriesAndParameters(lastState);
                await RestoreValues(lastState);
                RestoreRuleTypeAndNaming(lastState);
                RestoreGraphicOverrides(lastState);
                RestoreApplyScope(lastState);
            }
            finally
            {
                _restoringState = false;
            }

            UpdateStatus(restoredMessage);
        }

        private async Task RestoreCategoriesAndParameters(FilterProState lastState)
        {
            if (lastState.CategoryIds?.Any() == true)
            {
                categories_listbox.SelectedItems.Clear();
                var wanted = new HashSet<int>(lastState.CategoryIds.Select(id => id.IntegerValue));
                foreach (FilterCategoryItem item in categories_listbox.Items)
                {
                    if (wanted.Contains(item.Id.IntegerValue))
                        categories_listbox.SelectedItems.Add(item);
                }

                if (!parameters_listbox.Items.Cast<object>().Any())
                    await LoadParameters();
            }

            if (lastState.ParameterId != null)
            {
                foreach (FilterParameterItem item in parameters_listbox.Items)
                {
                    if (item.Id.IntegerValue == lastState.ParameterId.IntegerValue)
                    {
                        parameters_listbox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private async Task RestoreValues(FilterProState lastState)
        {
            if (!values_listbox.Items.Cast<object>().Any())
                await LoadValues();

            if (lastState.Values != null && lastState.Values.Any())
            {
                values_listbox.SelectedItems.Clear();
                foreach (FilterValueItem valueItem in values_listbox.Items)
                {
                    if (FilterValueKeyMatcher.MatchesValue(valueItem, lastState.Values))
                        values_listbox.SelectedItems.Add(valueItem);
                }
            }
        }

        private void RestoreRuleTypeAndNaming(FilterProState lastState)
        {
            ApplyRuleTypeSelection(lastState.RuleType);
            prefix_textbox.Text = lastState.Prefix ?? string.Empty;
            suffix_textbox.Text = lastState.Suffix ?? string.Empty;
            separator_textbox.Text = string.IsNullOrWhiteSpace(lastState.Separator) ? "_" : lastState.Separator;
            case_sensitive_checkbox.IsChecked = lastState.CaseSensitive;
            include_cat_checkbox.IsChecked = lastState.IncludeCategory;
            include_param_checkbox.IsChecked = lastState.IncludeParameter;
            override_existing_checkbox.IsChecked = lastState.OverrideExisting;
        }

        private void RestoreGraphicOverrides(FilterProState lastState)
        {
            bool hasColorPrefs = lastState.ColorProjectionLines || lastState.ColorProjectionPatterns || lastState.ColorCutLines || lastState.ColorCutPatterns || lastState.ColorHalftone;

            if (hasColorPrefs)
            {
                color_proj_lines_checkbox.IsChecked = lastState.ColorProjectionLines;
                color_proj_patterns_checkbox.IsChecked = lastState.ColorProjectionPatterns;
                color_cut_lines_checkbox.IsChecked = lastState.ColorCutLines;
                color_cut_patterns_checkbox.IsChecked = lastState.ColorCutPatterns;
                color_halftone_checkbox.IsChecked = lastState.ColorHalftone;
            }
            else
            {
                color_proj_lines_checkbox.IsChecked = true;
                color_cut_lines_checkbox.IsChecked = true;
                color_proj_patterns_checkbox.IsChecked = false;
                color_cut_patterns_checkbox.IsChecked = false;
                color_halftone_checkbox.IsChecked = false;
            }

            RestorePatternSelection(lastState.PatternId);
        }

        private void RestoreApplyScope(FilterProState lastState)
        {
            if (lastState.ApplyToActiveView || lastState.TargetViewIds == null || lastState.TargetViewIds.Count == 0)
            {
                apply_active_radio.IsChecked = true;
            }
            else
            {
                apply_multiple_radio.IsChecked = true;
                views_listbox.IsEnabled = true;
                var wantedViews = new HashSet<int>(lastState.TargetViewIds.Select(id => id.IntegerValue));
                views_listbox.SelectedItems.Clear();
                foreach (ApplyViewItem item in views_listbox.Items)
                {
                    if (wantedViews.Contains(item.Id.IntegerValue))
                        views_listbox.SelectedItems.Add(item);
                }
            }
            UpdateApplyScopeLabel();
        }

        private void ApplyRuleTypeSelection(string ruleType)
        {
            if (string.IsNullOrEmpty(ruleType))
                ruleType = RuleTypes.EqualsRule;

            foreach (var panel in rule_type_panel.Children.OfType<StackPanel>())
            {
                foreach (var child in panel.Children.OfType<RadioButton>())
                {
                    if (child.Tag as string == ruleType)
                    {
                        child.IsChecked = true;
                        return;
                    }
                }
            }
        }

        private void RememberState(FilterSelection selection)
        {
            _stateTracker.Save(
                selection,
                separator_textbox.Text,
                apply_active_radio.IsChecked == true,
                GetSelectedViewIds(),
                case_sensitive_checkbox.IsChecked == true);
        }

        private string BuildResultStatus(string actionText, int created, IList<string> skipped)
        {
            string status = $"{created} filter(s) {actionText}.";
            if (skipped != null && skipped.Count > 0)
                status += $" Skipped: {string.Join(", ", skipped)}.";
            return status;
        }

        private void ShowWarningIfNeeded(IList<string> skipped)
        {
            if (skipped == null || skipped.Count == 0) return;
            string msg = "Some filters were skipped:\n\n- " + string.Join("\n- ", skipped);
            TaskDialog.Show("Warning", msg);
        }

        private FilterSelection BuildFilterSelection(
            bool requiresTargetViews,
            out List<View> targetViews,
            out List<FilterValueItem> selectedValues)
        {
            targetViews = new List<View>();
            selectedValues = new List<FilterValueItem>();

            var catIds = categories_listbox.SelectedItems
                .Cast<FilterCategoryItem>()
                .Select(c => c.Id)
                .ToList();
            if (!catIds.Any())
            {
                TaskDialog.Show("Validation", "Please select at least one category.");
                return null;
            }

            var param = parameters_listbox.SelectedItem as FilterParameterItem;
            if (param == null)
            {
                TaskDialog.Show("Validation", "Please select a parameter.");
                return null;
            }

            var ruleType = GetSelectedRuleType();
            selectedValues = values_listbox.SelectedItems
                .Cast<FilterValueItem>()
                .ToList();

            if (selectedValues.Count == 0 &&
                ruleType != RuleTypes.HasValue &&
                ruleType != RuleTypes.HasNoValue)
            {
                TaskDialog.Show("Validation", "Please select at least one value for the chosen rule.");
                return null;
            }

            if (requiresTargetViews)
            {
                targetViews = ResolveTargetViews(out var targetIds);
                if (!targetViews.Any())
                    return null;
            }

            string separator = string.IsNullOrWhiteSpace(separator_textbox.Text)
                ? "_"
                : separator_textbox.Text;

            var valuesForCreation = selectedValues.Count == 0
                ? new List<FilterValueItem>
                  {
                      new FilterValueItem("Any", null, param.StorageType)
                  }
                : selectedValues;

            return new FilterSelection
            {
                CategoryIds = catIds,
                Parameter = param,
                Values = valuesForCreation,
                RuleType = ruleType,
                OverrideExisting = override_existing_checkbox.IsChecked == true,
                ColorProjectionLines = color_proj_lines_checkbox.IsChecked == true,
                ColorProjectionPatterns = color_proj_patterns_checkbox.IsChecked == true,
                ColorCutLines = color_cut_lines_checkbox.IsChecked == true,
                ColorCutPatterns = color_cut_patterns_checkbox.IsChecked == true,
                ColorHalftone = color_halftone_checkbox.IsChecked == true,
                PatternId = GetSelectedPatternId(),
                PlaceNewFiltersFirst = true,
                Prefix = prefix_textbox.Text,
                Suffix = suffix_textbox.Text,
                IncludeCategory = include_cat_checkbox.IsChecked == true,
                IncludeParameter = include_param_checkbox.IsChecked == true,
                TargetViewIds = targetViews.Select(v => v.Id).ToList(),
                ApplyToActiveView = apply_active_radio.IsChecked == true,
                Separator = separator,
                CaseSensitive = case_sensitive_checkbox.IsChecked == true
            };
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var selection = BuildFilterSelection(false, out _, out var selectedValues);
            if (selection == null) return;

            selection.ApplyToView = false;
            selection.ApplyGraphics = false;
            selection.RandomColors = false;

            var skipped = new List<string>();
            int created = 0;
            try
            {
                using (var t = new Transaction(_doc, "Create Filters"))
                {
                    t.Start();
                    created = FilterProHelper.CreateFilters(_doc, new List<View>(), selection, skipped);
                    t.Commit();
                    _madeChanges = true;
                }

                RememberState(selection);
                UpdateStatus(BuildResultStatus("created", created, skipped));
                ShowWarningIfNeeded(skipped);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to create filters: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        private string GetSelectedRuleType()
        {
            if (radio_equals.IsChecked == true) return RuleTypes.EqualsRule;
            if (radio_not_equals.IsChecked == true) return RuleTypes.NotEquals;
            if (radio_contains.IsChecked == true) return RuleTypes.Contains;
            if (radio_not_contains.IsChecked == true) return RuleTypes.NotContains;
            if (radio_starts.IsChecked == true) return RuleTypes.BeginsWith;
            if (radio_not_starts.IsChecked == true) return RuleTypes.NotBeginsWith;
            if (radio_ends.IsChecked == true) return RuleTypes.EndsWith;
            if (radio_not_ends.IsChecked == true) return RuleTypes.NotEndsWith;
            if (radio_has_value.IsChecked == true) return RuleTypes.HasValue;
            if (radio_not_has_value.IsChecked == true) return RuleTypes.HasNoValue;
            return RuleTypes.EqualsRule; // Default fallback
        }

        private void LoadCategories()
        {
            try
            {
                categories_listbox.ItemsSource = _dataProvider.GetFilterableCategories();
                categories_listbox.DisplayMemberPath = "Name";
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading categories: {ex.Message}");
            }
        }

        private async Task LoadParameters()
        {
            loading_indicator.Visibility = global::System.Windows.Visibility.Visible;
            try
            {
                var selectedCategories = await Task.Run(() =>
                    Dispatcher.Invoke(() =>
                        categories_listbox.SelectedItems
                            .Cast<FilterCategoryItem>()
                            .ToList()));

                if (!selectedCategories.Any())
                {
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        parameters_listbox.ItemsSource = null;
                        values_listbox.ItemsSource = null;
                        UpdateStatus("Select one or more categories.");
                    }));
                    return;
                }

                var catIds = selectedCategories.Select(c => c.Id).ToList();
                var parameters = await Task.Run(() => _dataProvider.GetParametersForCategories(catIds));

                await Dispatcher.BeginInvoke(new Action(async () =>
                {
                    parameters_listbox.ItemsSource = parameters;
                    parameters_listbox.DisplayMemberPath = "Name";
                    UpdateStatus("Select a parameter to load its values.");
                    await LoadValues();
                }));
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading parameters: {ex.Message}");
            }
            finally
            {
                loading_indicator.Visibility = global::System.Windows.Visibility.Collapsed;
            }
        }

        private async Task LoadValues()
        {
            values_listbox.ItemsSource = null;
            loading_indicator.Visibility = global::System.Windows.Visibility.Visible;

            try
            {
                var param = await Task.Run(() =>
                    Dispatcher.Invoke(() => parameters_listbox.SelectedItem as FilterParameterItem));
                var catIds = await Task.Run(() =>
                    Dispatcher.Invoke(() =>
                        categories_listbox.SelectedItems
                            .Cast<FilterCategoryItem>()
                            .Select(c => c.Id)
                            .ToList()));

                if (param == null || !catIds.Any())
                {
                    UpdateStatus("Select a parameter to load its values.");
                    return;
                }

                UpdateStatus("Collecting values...");

                bool hitScanLimit = false;
                var collectedValues = await Task.Run(() =>
                {
                    var values = _dataProvider.GetValues(param, catIds, out bool hitLimit);
                    hitScanLimit = hitLimit;
                    return values;
                });

                _currentValues.Clear();
                _currentValues.AddRange(collectedValues);

                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    values_listbox.ItemsSource = collectedValues;
                    values_listbox.DisplayMemberPath = "Display";

                    if (hitScanLimit)
                        UpdateStatus($"Loaded {collectedValues.Count} unique value(s). Stopped after scanning 10,000 elements for performance.");
                    else
                        UpdateStatus($"Loaded {collectedValues.Count} unique value(s).");
                }));
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading values: {ex.Message}");
            }
            finally
            {
                loading_indicator.Visibility = global::System.Windows.Visibility.Collapsed;
            }
        }

        private void UpdateStatus(string message)
        {
            if (_restoringState)
                return;

            status_text.Text = message;
        }

        private void ApplyViewButton_Click(object sender, RoutedEventArgs e)
        {
            var selection = BuildFilterSelection(true, out var targetViews, out var selectedValues);
            if (selection == null) return;

            selection.ApplyToView = true;
            selection.ApplyGraphics = false;
            selection.RandomColors = false;
            selection.OverrideExisting = true; // reuse/update existing filters when applying

            var skipped = new List<string>();
            int created = 0;
            try
            {
                using (var t = new Transaction(_doc, "Create and Apply Filters"))
                {
                    t.Start();
                    created = FilterProHelper.CreateFilters(_doc, targetViews, selection, skipped);
                    t.Commit();
                    _madeChanges = true;
                }

                RememberState(selection);
                UpdateStatus(BuildResultStatus("created and applied to view", created, skipped));
                ShowWarningIfNeeded(skipped);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to create filters: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        private void ShuffleColorsButton_Click(object sender, RoutedEventArgs e)
        {
            var selection = BuildFilterSelection(true, out var targetViews, out var selectedValues);
            if (selection == null) return;

            selection.ApplyToView = true;
            selection.ApplyGraphics = true;
            selection.RandomColors = true;
            selection.OverrideExisting = true;

            var skipped = new List<string>();
            int created = 0;
            try
            {
                using (var t = new Transaction(_doc, "Create Filters with Random Colors"))
                {
                    t.Start();
                    created = FilterProHelper.CreateFilters(_doc, targetViews, selection, skipped);
                    t.Commit();
                    _madeChanges = true;
                }

                RememberState(selection);
                UpdateStatus(
                    BuildResultStatus("updated with random colors and applied to view",
                        created, skipped));
                ShowWarningIfNeeded(skipped);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to create filters: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        // Exposed to the external command so it can return Succeeded when changes were made.
        internal bool HasChanges => _madeChanges;
    }


}
