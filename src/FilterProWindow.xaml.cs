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
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using AJTools.Models;
using AJTools.Services;

namespace AJTools
{
    /// <summary>
    /// Interaction logic for FilterProWindow.xaml
    /// </summary>
    public partial class FilterProWindow : Window
    {
        private readonly Document _doc;
        private readonly View _activeView;

        private readonly List<FilterValueItem> _currentValues = new List<FilterValueItem>();
        private static FilterProState _lastState;
        private static string _lastDocPath;
        private bool _restoringState;
        private List<ApplyViewItem> _allViews = new List<ApplyViewItem>();
        private List<PatternItem> _patterns = new List<PatternItem>();
        private bool _madeChanges;

        public FilterProWindow(Document doc, View activeView)
        {
            InitializeComponent();
            _doc = doc;
            _activeView = activeView;

            // Reset remembered state when switching between different documents
            string docKey = !string.IsNullOrWhiteSpace(_doc.PathName)
                ? _doc.PathName
                : $"{_doc.Title}|{_doc.GetHashCode()}";

            if (!string.Equals(_lastDocPath, docKey, StringComparison.OrdinalIgnoreCase))
            {
                _lastDocPath = docKey;
                _lastState = null;
            }

            WireEvents();
            SetActiveViewName();
            LoadCategories();
            LoadViewsForApply();
            LoadPatterns();
            RestoreLastSelection();

            if (_lastState == null)
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
            pattern_combo.SelectionChanged += (s, e) => RememberPatternSelection();
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

        private void RememberPatternSelection()
        {
            // noop placeholder to trigger state when needed
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
            if (_lastState == null) return;

            string restoredMessage = "Restored previous Filter Pro selection.";
            _restoringState = true;
            try
            {
                await RestoreCategoriesAndParameters();
                await RestoreValues();
                RestoreRuleTypeAndNaming();
                RestoreGraphicOverrides();
                RestoreApplyScope();
            }
            finally
            {
                _restoringState = false;
            }

            UpdateStatus(restoredMessage);
        }

        private async Task RestoreCategoriesAndParameters()
        {
            if (_lastState.CategoryIds?.Any() == true)
            {
                categories_listbox.SelectedItems.Clear();
                var wanted = new HashSet<int>(_lastState.CategoryIds.Select(id => id.IntegerValue));
                foreach (FilterCategoryItem item in categories_listbox.Items)
                {
                    if (wanted.Contains(item.Id.IntegerValue))
                        categories_listbox.SelectedItems.Add(item);
                }

                if (!parameters_listbox.Items.Cast<object>().Any())
                    await LoadParameters();
            }

            if (_lastState.ParameterId != null)
            {
                foreach (FilterParameterItem item in parameters_listbox.Items)
                {
                    if (item.Id.IntegerValue == _lastState.ParameterId.IntegerValue)
                    {
                        parameters_listbox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private async Task RestoreValues()
        {
            if (!values_listbox.Items.Cast<object>().Any())
                await LoadValues();

            if (_lastState.Values != null && _lastState.Values.Any())
            {
                values_listbox.SelectedItems.Clear();
                foreach (FilterValueItem valueItem in values_listbox.Items)
                {
                    if (MatchesValueKey(valueItem, _lastState.Values))
                        values_listbox.SelectedItems.Add(valueItem);
                }
            }
        }

        private void RestoreRuleTypeAndNaming()
        {
            ApplyRuleTypeSelection(_lastState.RuleType);
            prefix_textbox.Text = _lastState.Prefix ?? string.Empty;
            suffix_textbox.Text = _lastState.Suffix ?? string.Empty;
            separator_textbox.Text = string.IsNullOrWhiteSpace(_lastState.Separator) ? "_" : _lastState.Separator;
            case_sensitive_checkbox.IsChecked = _lastState.CaseSensitive;
            include_cat_checkbox.IsChecked = _lastState.IncludeCategory;
            include_param_checkbox.IsChecked = _lastState.IncludeParameter;
            override_existing_checkbox.IsChecked = _lastState.OverrideExisting;
        }

        private void RestoreGraphicOverrides()
        {
            bool hasColorPrefs = _lastState.ColorProjectionLines || _lastState.ColorProjectionPatterns || _lastState.ColorCutLines || _lastState.ColorCutPatterns || _lastState.ColorHalftone;

            if (hasColorPrefs)
            {
                color_proj_lines_checkbox.IsChecked = _lastState.ColorProjectionLines;
                color_proj_patterns_checkbox.IsChecked = _lastState.ColorProjectionPatterns;
                color_cut_lines_checkbox.IsChecked = _lastState.ColorCutLines;
                color_cut_patterns_checkbox.IsChecked = _lastState.ColorCutPatterns;
                color_halftone_checkbox.IsChecked = _lastState.ColorHalftone;
            }
            else
            {
                color_proj_lines_checkbox.IsChecked = true;
                color_cut_lines_checkbox.IsChecked = true;
                color_proj_patterns_checkbox.IsChecked = false;
                color_cut_patterns_checkbox.IsChecked = false;
                color_halftone_checkbox.IsChecked = false;
            }

            RestorePatternSelection(_lastState.PatternId);
        }

        private void RestoreApplyScope()
        {
            if (_lastState.ApplyToActiveView || _lastState.TargetViewIds == null || _lastState.TargetViewIds.Count == 0)
            {
                apply_active_radio.IsChecked = true;
            }
            else
            {
                apply_multiple_radio.IsChecked = true;
                views_listbox.IsEnabled = true;
                var wantedViews = new HashSet<int>(_lastState.TargetViewIds.Select(id => id.IntegerValue));
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
            _lastState = new FilterProState
            {
                CategoryIds = selection.CategoryIds?.ToList() ?? new List<ElementId>(),
                ParameterId = selection.Parameter?.Id,
                RuleType = selection.RuleType,
                Prefix = selection.Prefix ?? string.Empty,
                Suffix = selection.Suffix ?? string.Empty,
                Separator = string.IsNullOrWhiteSpace(separator_textbox.Text) ? "_" : separator_textbox.Text,
                CaseSensitive = case_sensitive_checkbox.IsChecked == true,
                IncludeCategory = selection.IncludeCategory,
                IncludeParameter = selection.IncludeParameter,
                OverrideExisting = selection.OverrideExisting,
                ApplyToActiveView = apply_active_radio.IsChecked == true,
                TargetViewIds = GetSelectedViewIds(),
                ColorProjectionLines = selection.ColorProjectionLines,
                ColorProjectionPatterns = selection.ColorProjectionPatterns,
                ColorCutLines = selection.ColorCutLines,
                ColorCutPatterns = selection.ColorCutPatterns,
                ColorHalftone = selection.ColorHalftone,
                PatternId = selection.PatternId,
                PlaceNewFiltersFirst = selection.PlaceNewFiltersFirst,
                ApplyGraphics = selection.ApplyGraphics,
                Values = BuildValueKeys(selection.Values)
            };
        }

        private List<FilterValueKey> BuildValueKeys(IList<FilterValueItem> selectedValues)
        {
            var keys = new List<FilterValueKey>();
            if (selectedValues == null) return keys;

            foreach (var v in selectedValues)
            {
                if (v == null) continue;

                if (v.RawValue is Tuple<string, string> familyAndType)
                {
                    const string separator = "|||";
                    const string prefix = "__FAMILY_AND_TYPE__";
                    string key = $"{prefix}{familyAndType.Item1}{separator}{familyAndType.Item2}";
                    keys.Add(FilterValueKey.ForString(key));
                }
                else if (v.StorageType == StorageType.String)
                {
                    string s = v.RawValue as string ?? v.Display;
                    if (!string.IsNullOrWhiteSpace(s))
                        keys.Add(FilterValueKey.ForString(s));
                }
                else if (v.StorageType == StorageType.Integer)
                {
                    int i = Convert.ToInt32(v.RawValue);
                    keys.Add(FilterValueKey.ForInt(i));
                }
                else if (v.StorageType == StorageType.Double)
                {
                    double d = Convert.ToDouble(v.RawValue);
                    keys.Add(FilterValueKey.ForDouble(d));
                }
                else if (v.StorageType == StorageType.ElementId)
                {
                    ElementId eid = v.ElementId ?? v.RawValue as ElementId ?? ElementId.InvalidElementId;
                    if (eid != ElementId.InvalidElementId)
                        keys.Add(FilterValueKey.ForElementId(eid));
                }
            }

            return keys;
        }

        private bool MatchesValueKey(FilterValueItem item, IList<FilterValueKey> keys)
        {
            if (item == null || keys == null || keys.Count == 0) return false;

            foreach (var key in keys)
            {
                if (key.StorageType != item.StorageType) continue;

                if (IsFamilyAndTypeKey(key))
                {
                    if (MatchesFamilyAndType(item, key)) return true;
                }
                else
                {
                    if (MatchesSimpleValue(item, key)) return true;
                }
            }
            return false;
        }

        private bool IsFamilyAndTypeKey(FilterValueKey key)
        {
            const string prefix = "__FAMILY_AND_TYPE__";
            return key.StorageType == StorageType.String && key.StringValue != null && key.StringValue.StartsWith(prefix, StringComparison.Ordinal);
        }

        private bool MatchesFamilyAndType(FilterValueItem item, FilterValueKey key)
        {
            if (!(item.RawValue is Tuple<string, string> familyAndType)) return false;

            const string separator = "|||";
            const string prefix = "__FAMILY_AND_TYPE__";

            string savedKey = key.StringValue;
            string content = savedKey.Substring(prefix.Length);
            var parts = content.Split(new[] { separator }, StringSplitOptions.None);

            if (parts.Length != 2) return false;

            string savedFamily = parts[0];
            string savedType = parts[1];

            return string.Equals(familyAndType.Item1, savedFamily, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(familyAndType.Item2, savedType, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesSimpleValue(FilterValueItem item, FilterValueKey key)
        {
            switch (key.StorageType)
            {
                case StorageType.String:
                    string itemStr = item.RawValue as string ?? item.Display;
                    return itemStr != null && key.StringValue != null && string.Equals(itemStr, key.StringValue, StringComparison.OrdinalIgnoreCase);

                case StorageType.Integer:
                    int itemInt = Convert.ToInt32(item.RawValue);
                    return key.IntValue.HasValue && itemInt == key.IntValue.Value;

                case StorageType.Double:
                    double itemDouble = Convert.ToDouble(item.RawValue);
                    return key.DoubleValue.HasValue && Math.Abs(itemDouble - key.DoubleValue.Value) < 1e-6;

                case StorageType.ElementId:
                    ElementId eid = item.ElementId ?? item.RawValue as ElementId ?? ElementId.InvalidElementId;
                    return key.ElementIdValue.HasValue && eid != null && eid.IntegerValue == key.ElementIdValue.Value;

                default:
                    return false;
            }
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
                ICollection<ElementId> filterableCats =
                    ParameterFilterUtilities.GetAllFilterableCategories();
                var sorted = new List<FilterCategoryItem>();
                foreach (ElementId catId in filterableCats)
                {
                    Category cat = Category.GetCategory(_doc, catId);
                    if (cat != null)
                        sorted.Add(new FilterCategoryItem(catId, cat.Name));
                }

                categories_listbox.ItemsSource = sorted.OrderBy(x => x.Name);
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

                var parameters = await Task.Run(() =>
                {
                    var catIds = selectedCategories.Select(c => c.Id).ToList();
                    var paramIds = new HashSet<ElementId>(
                        ParameterFilterUtilities.GetFilterableParametersInCommon(_doc, catIds));

                    // Manually add Family Name and Type Name to ensure they are always available
                    paramIds.Add(new ElementId(BuiltInParameter.ALL_MODEL_FAMILY_NAME));
                    paramIds.Add(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_NAME));

                    var result = new List<FilterParameterItem>();

                    // Add our special composite parameter at the top of the list
                    result.Add(new FilterParameterItem(
                        SpecialParameterIds.FamilyAndType,
                        "Family and Type",
                        StorageType.String));

                    foreach (ElementId pid in paramIds)
                    {
                        Parameter sample = GetSampleParameter(pid, catIds);
                        StorageType storage = sample?.StorageType ?? StorageType.None;

                        if (pid.IntegerValue == (int)BuiltInParameter.ALL_MODEL_FAMILY_NAME ||
                            pid.IntegerValue == (int)BuiltInParameter.ALL_MODEL_TYPE_NAME)
                        {
                            storage = StorageType.String;
                        }

                        string name = ResolveParameterName(pid, sample);
                        if (storage != StorageType.None)
                        {
                            result.Add(new FilterParameterItem(pid, name, storage));
                        }
                    }
                    return result.OrderBy(p => p.Name).ToList();
                });

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

                var collectedValues = await Task.Run(async () =>
                {
                    if (param.Id.IntegerValue == SpecialParameterIds.FamilyAndType.IntegerValue)
                    {
                        return await LoadFamilyAndTypeValues(catIds);
                    }
                    return await LoadRegularParameterValues(param, catIds);
                });

                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    values_listbox.ItemsSource = collectedValues;
                    values_listbox.DisplayMemberPath = "Display";

                    if (collectedValues.Count >= 10000)
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

        private async Task<List<FilterValueItem>> LoadFamilyAndTypeValues(List<ElementId> catIds)
        {
            var famTypeCollector = new FilteredElementCollector(_doc).WherePasses(new ElementMulticategoryFilter(catIds));
            var famTypeSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var familyAndTypeValues = new List<FilterValueItem>();

            await Task.Run(() =>
            {
                foreach (Element elem in famTypeCollector)
                {
                    var pFam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);
                    string familyName = pFam != null ? pFam.AsString() : string.Empty;

                    var pType = elem.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME);
                    string typeName = pType != null ? pType.AsString() : string.Empty;

                    if (string.IsNullOrWhiteSpace(familyName) && elem.Category != null)
                    {
                        familyName = elem.Category.Name;
                    }

                    if (string.IsNullOrWhiteSpace(familyName) || string.IsNullOrWhiteSpace(typeName))
                        continue;

                    string display = $"{familyName} - {typeName}";
                    if (famTypeSeen.Add(display))
                    {
                        familyAndTypeValues.Add(new FilterValueItem(display, new Tuple<string, string>(familyName, typeName), StorageType.String));
                    }
                }
            });

            _currentValues.Clear();
            _currentValues.AddRange(familyAndTypeValues);
            return familyAndTypeValues.OrderBy(v => v.Display).ToList();
        }

        private async Task<List<FilterValueItem>> LoadRegularParameterValues(FilterParameterItem param, List<ElementId> catIds)
        {
            const int elementScanLimit = 10000;
            int scanned = 0;

            var filter = new ElementMulticategoryFilter(catIds);
            var collector = new FilteredElementCollector(_doc).WherePasses(filter);

            int paramIntId = param.Id.IntegerValue;
            bool isBuiltIn = Enum.IsDefined(typeof(BuiltInParameter), paramIntId);
            BuiltInParameter builtInParam = isBuiltIn ? (BuiltInParameter)paramIntId : 0;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var collectedValuesResult = new List<FilterValueItem>();

            await Task.Run(() =>
            {
                foreach (Element elem in collector)
                {
                    scanned++;
                    if (scanned > elementScanLimit)
                        break;

                    Parameter p = null;

                    if (isBuiltIn)
                    {
                        p = elem.get_Parameter(builtInParam);
                    }

                    if (p == null)
                    {
                        p = elem.LookupParameter(param.Name);
                    }

                    if (p == null)
                    {
                        foreach (Parameter elemParam in elem.Parameters)
                        {
                            if (elemParam.Id.IntegerValue == paramIntId)
                            {
                                p = elemParam;
                                break;
                            }
                        }
                    }

                    if (p == null || p.StorageType == StorageType.None || !p.HasValue) continue;

                    FilterValueItem item = ExtractValueItem(p, elem, param.StorageType, param.Name);
                    if (item?.RawValue == null) continue;

                    string key = item.StorageType == StorageType.String
                        ? item.RawValue as string
                        : item.Display;

                    if (!string.IsNullOrEmpty(key) && seen.Add(key))
                        collectedValuesResult.Add(item);
                }
            });

            _currentValues.Clear();
            _currentValues.AddRange(collectedValuesResult);

            return collectedValuesResult.OrderBy(v => v.Display).ToList();
        }

        private FilterValueItem ExtractValueItem(Parameter param, Element owner, StorageType targetStorage, string paramName)
        {
            if (param == null || !param.HasValue) return null;

            switch (targetStorage)
            {
                case StorageType.String:
                    string text = param.AsString() ?? param.AsValueString();
                    return string.IsNullOrEmpty(text)
                        ? null
                        : new FilterValueItem(text, text, StorageType.String);

                case StorageType.Integer:
                    int i = param.AsInteger();
                    return new FilterValueItem(i.ToString(), i, StorageType.Integer);

                case StorageType.Double:
                    double d = param.AsDouble();
                    string display = param.AsValueString() ?? d.ToString("0.###");
                    return new FilterValueItem(display, d, StorageType.Double);

                case StorageType.ElementId:
                    ElementId eid = param.AsElementId();
                    if (eid == null || eid == ElementId.InvalidElementId) return null;

                    string name = ResolveElementName(_doc.GetElement(eid), eid, paramName);
                    return new FilterValueItem(name, eid, StorageType.ElementId, eid);

                default:
                    return null;
            }
        }

        private string ResolveElementName(Element element, ElementId id, string paramName)
        {
            if (element == null) return "#" + id.IntegerValue;

            // Prefer family + type when available for clarity
            string familyName = null;
            string typeName = null;

            if (element is FamilyInstance inst && inst.Symbol != null)
            {
                familyName = inst.Symbol.FamilyName;
                typeName = inst.Symbol.Name;
            }
            else if (element is FamilySymbol fs)
            {
                familyName = fs.FamilyName;
                typeName = fs.Name;
            }
            else if (element is ElementType et)
            {
                typeName = et.Name;
                Parameter famParam =
                    et.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM) ??
                    et.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);
                if (famParam != null && famParam.HasValue)
                    familyName = famParam.AsString();
            }

            // If the parameter name suggests a system/type name, prefer the raw element name
            if (!string.IsNullOrWhiteSpace(paramName) &&
                paramName.IndexOf("system", StringComparison.OrdinalIgnoreCase) >= 0 &&
                !string.IsNullOrWhiteSpace(element.Name))
            {
                return element.Name;
            }

            if (!string.IsNullOrWhiteSpace(familyName) &&
                !string.IsNullOrWhiteSpace(typeName))
                return $"{familyName} : {typeName}";

            if (!string.IsNullOrWhiteSpace(typeName))
                return typeName;

            string label = element.Name;
            if (!string.IsNullOrWhiteSpace(label))
                return label;

            if (element is MechanicalSystemType)
            {
                Parameter nameParam =
                    element.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM);
                if (nameParam != null)
                {
                    string name = nameParam.AsString();
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }

            if (!string.IsNullOrWhiteSpace(paramName) &&
                paramName.IndexOf("System Type", StringComparison.OrdinalIgnoreCase) >= 0)
                return "System " + id.IntegerValue;

            return "#" + id.IntegerValue;
        }

        private Parameter GetSampleParameter(ElementId paramId, IList<ElementId> categoryIds)
        {
            foreach (ElementId catId in categoryIds)
            {
                var catFilter = new ElementCategoryFilter(catId);

                Element instance = new FilteredElementCollector(_doc)
                    .WherePasses(catFilter)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault();

                if (instance != null)
                {
                    Parameter p = null;

                    // Try built-in parameter
                    if (Enum.IsDefined(typeof(BuiltInParameter), paramId.IntegerValue))
                    {
                        p = instance.get_Parameter((BuiltInParameter)paramId.IntegerValue);
                    }

                    // Try as shared/project parameter
                    if (p == null)
                    {
                        foreach (Parameter elemParam in instance.Parameters)
                        {
                            if (elemParam.Id.IntegerValue == paramId.IntegerValue)
                            {
                                p = elemParam;
                                break;
                            }
                        }
                    }

                    if (p != null) return p;
                }

                Element typeElem = new FilteredElementCollector(_doc)
                    .WherePasses(catFilter)
                    .WhereElementIsElementType()
                    .FirstOrDefault();

                if (typeElem != null)
                {
                    Parameter p = null;

                    // Try built-in parameter
                    if (Enum.IsDefined(typeof(BuiltInParameter), paramId.IntegerValue))
                    {
                        p = typeElem.get_Parameter((BuiltInParameter)paramId.IntegerValue);
                    }

                    // Try as shared/project parameter
                    if (p == null)
                    {
                        foreach (Parameter elemParam in typeElem.Parameters)
                        {
                            if (elemParam.Id.IntegerValue == paramId.IntegerValue)
                            {
                                p = elemParam;
                                break;
                            }
                        }
                    }

                    if (p != null) return p;
                }
            }
            return null;
        }

        private string ResolveParameterName(ElementId paramId, Parameter sample)
        {
            if (sample != null)
                return sample.Definition.Name;

            if (_doc.GetElement(paramId) is ParameterElement paramElem)
                return paramElem.Name;

            if (Enum.IsDefined(typeof(BuiltInParameter), paramId.IntegerValue))
            {
                try
                {
                    return LabelUtils.GetLabelFor(
                        (BuiltInParameter)paramId.IntegerValue);
                }
                catch
                {
                    // ignore
                }
            }

            return "Param " + paramId.IntegerValue;
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
