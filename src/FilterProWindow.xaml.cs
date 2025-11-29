using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AJTools
{
    /// <summary>
    /// Interaction logic for FilterProWindow.xaml
    /// </summary>
    public partial class FilterProWindow : Window
    {
        private readonly Document _doc;
        private readonly View _activeView;

        private readonly List<RuleTypeItem> _allRuleTypes;
        private readonly List<FilterValueItem> _currentValues = new List<FilterValueItem>();

        public FilterProWindow(Document doc, View activeView)
        {
            InitializeComponent();
            _doc = doc;
            _activeView = activeView;

            _allRuleTypes = new List<RuleTypeItem>
            {
                new RuleTypeItem(RuleTypes.Equals, "Equals", true, true, true),
                new RuleTypeItem(RuleTypes.NotEquals, "Does not equal", true, true, true),
                new RuleTypeItem(RuleTypes.Greater, "Is greater than", false, true, false),
                new RuleTypeItem(RuleTypes.GreaterOrEqual, "Is greater than or equal to", false, true, false),
                new RuleTypeItem(RuleTypes.Less, "Is less than", false, true, false),
                new RuleTypeItem(RuleTypes.LessOrEqual, "Is less than or equal to", false, true, false),
                new RuleTypeItem(RuleTypes.Contains, "Contains", true, false, false),
                new RuleTypeItem(RuleTypes.NotContains, "Does not contain", true, false, false),
                new RuleTypeItem(RuleTypes.BeginsWith, "Begins with", true, false, false),
                new RuleTypeItem(RuleTypes.NotBeginsWith, "Does not begin with", true, false, false),
                new RuleTypeItem(RuleTypes.EndsWith, "Ends with", true, false, false),
                new RuleTypeItem(RuleTypes.NotEndsWith, "Does not end with", true, false, false),
                new RuleTypeItem(RuleTypes.HasValue, "Has a value", true, true, true),
                new RuleTypeItem(RuleTypes.HasNoValue, "Has no value", true, true, true)
            };

            WireEvents();
            LoadCategories();
            UpdateStatus("Select categories to begin.");
        }

        private void WireEvents()
        {
            close_button.Click += (s, e) => Close();
            create_button.Click += CreateButton_Click;
            apply_view_button.Click += ApplyViewButton_Click;
            shuffle_colors_button.Click += ShuffleColorsButton_Click;

            // List selection changes
            categories_listbox.SelectionChanged += (s, e) => { LoadParameters(); UpdateNamePreview(); };
            parameters_listbox.SelectionChanged += (s, e) => { LoadValues(); UpdateNamePreview(); };
            values_listbox.SelectionChanged += (s, e) => UpdateNamePreview();

            // Naming convention changes
            prefix_textbox.TextChanged += (s, e) => UpdateNamePreview();
            suffix_textbox.TextChanged += (s, e) => UpdateNamePreview();
            separator_textbox.TextChanged += (s, e) => UpdateNamePreview();
            include_cat_checkbox.Checked += (s, e) => UpdateNamePreview();
            include_cat_checkbox.Unchecked += (s, e) => UpdateNamePreview();
            include_param_checkbox.Checked += (s, e) => UpdateNamePreview();
            include_param_checkbox.Unchecked += (s, e) => UpdateNamePreview();
            
            // Rule changes
            foreach (var rb in new[] { radio_equals, radio_not_equals, radio_contains, radio_not_contains, radio_starts, radio_not_starts, radio_ends, radio_not_ends, radio_has_value, radio_not_has_value })
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
                source = source.Where(v => v.Display.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Sort
            if (value_sort_combobox.SelectedItem is ComboBoxItem selectedSort && selectedSort.Tag is string sortTag)
            {
                if (sortTag == "za")
                    source = source.OrderByDescending(v => v.Display);
                else
                    source = source.OrderBy(v => v.Display);
            }

            values_listbox.ItemsSource = source.ToList();
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

            var tempSelection = new FilterSelection
            {
                CategoryIds = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().Select(c => c.Id).ToList(),
                Parameter = param,
                Values = values,
                Prefix = prefix_textbox.Text,
                Suffix = suffix_textbox.Text,
                IncludeCategory = include_cat_checkbox.IsChecked == true,
                IncludeParameter = include_param_checkbox.IsChecked == true
            };
            
            // This is a simplified version of the logic in CmdFilterPro.ComposeFilterName
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(tempSelection.Prefix)) parts.Add(tempSelection.Prefix);
            if (tempSelection.IncludeCategory && tempSelection.CategoryIds.Any())
            {
                var catName = (categories_listbox.SelectedItems.Cast<FilterCategoryItem>().FirstOrDefault())?.Name ?? "Category";
                parts.Add(catName);
            }
            if (tempSelection.IncludeParameter && tempSelection.Parameter != null) parts.Add(param.Name);
            parts.Add(valueText);
            if (!string.IsNullOrWhiteSpace(tempSelection.Suffix)) parts.Add(tempSelection.Suffix);

            string separator = string.IsNullOrWhiteSpace(separator_textbox.Text) ? "_" : separator_textbox.Text;
            string name = string.Join($" {separator} ", parts);

            preview_text.Text = name;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var catIds = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().Select(c => c.Id).ToList();
            if (!catIds.Any())
            {
                TaskDialog.Show("Validation", "Please select at least one category.");
                return;
            }

            var param = parameters_listbox.SelectedItem as FilterParameterItem;
            if (param == null)
            {
                TaskDialog.Show("Validation", "Please select a parameter.");
                return;
            }
            
            var ruleType = GetSelectedRuleType();

            var values = values_listbox.SelectedItems.Cast<FilterValueItem>().ToList();
            if (values.Count == 0 && ruleType != RuleTypes.HasValue && ruleType != RuleTypes.HasNoValue)
            {
                TaskDialog.Show("Validation", "Please select at least one value for the chosen rule.");
                return;
            }

            // For HasValue/HasNoValue, create a dummy value
            if (values.Count == 0)
            {
                values = new List<FilterValueItem>
                {
                    new FilterValueItem("Any", null, param.StorageType)
                };
            }

            var selection = new FilterSelection
            {
                CategoryIds = catIds,
                Parameter = param,
                Values = values,
                RuleType = ruleType,
                ApplyToView = false,
                OverrideExisting = override_existing_checkbox.IsChecked == true,
                RandomColors = false,
                Prefix = prefix_textbox.Text,
                Suffix = suffix_textbox.Text,
                IncludeCategory = include_cat_checkbox.IsChecked == true,
                IncludeParameter = include_param_checkbox.IsChecked == true
            };

            var skipped = new List<string>();
            int created = 0;
            try
            {
                using (var t = new Transaction(_doc, "Create Filters"))
                {
                    t.Start();
                    created = FilterProHelper.CreateFilters(_doc, _activeView, selection, skipped);
                    t.Commit();
                }

                string info = $"{created} filter(s) created.";
                if (skipped.Any())
                {
                    info += $"\n\nSkipped:\n- {string.Join("\n- ", skipped)}";
                }
                TaskDialog.Show("Filter Pro Results", info);
                UpdateStatus($"{created} created, {skipped.Count} skipped.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to create filters: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
            }
        }
        
        private string GetSelectedRuleType()
        {
            if (radio_equals.IsChecked == true) return RuleTypes.Equals;
            if (radio_not_equals.IsChecked == true) return RuleTypes.NotEquals;
            if (radio_contains.IsChecked == true) return RuleTypes.Contains;
            if (radio_not_contains.IsChecked == true) return RuleTypes.NotContains;
            if (radio_starts.IsChecked == true) return RuleTypes.BeginsWith;
            if (radio_not_starts.IsChecked == true) return RuleTypes.NotBeginsWith;
            if (radio_ends.IsChecked == true) return RuleTypes.EndsWith;
            if (radio_not_ends.IsChecked == true) return RuleTypes.NotEndsWith;
            if (radio_has_value.IsChecked == true) return RuleTypes.HasValue;
            if (radio_not_has_value.IsChecked == true) return RuleTypes.HasNoValue;
            return RuleTypes.Equals; // Default fallback
        }

        private void LoadCategories()
        {
            try
            {
                ICollection<ElementId> filterableCats = ParameterFilterUtilities.GetAllFilterableCategories();
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

        private void LoadParameters()
        {
            try
            {
                var selectedCategories = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().ToList();
                if (!selectedCategories.Any())
                {
                    parameters_listbox.ItemsSource = null;
                    values_listbox.ItemsSource = null;
                    UpdateStatus("Select one or more categories.");
                    return;
                }

                var catIds = selectedCategories.Select(c => c.Id).ToList();
                ICollection<ElementId> paramIds = ParameterFilterUtilities.GetFilterableParametersInCommon(_doc, catIds);
                var parameters = new List<FilterParameterItem>();

                foreach (ElementId pid in paramIds)
                {
                    Parameter sample = GetSampleParameter(pid, catIds);
                    StorageType storage = sample?.StorageType ?? StorageType.None;
                    string name = ResolveParameterName(pid, sample);
                    if (storage != StorageType.None)
                    {
                        parameters.Add(new FilterParameterItem(pid, name, storage));
                    }
                }

                parameters_listbox.ItemsSource = parameters.OrderBy(p => p.Name);
                parameters_listbox.DisplayMemberPath = "Name";
                UpdateStatus("Select a parameter to load its values.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading parameters: {ex.Message}");
            }
        }

        private void LoadValues()
        {
            values_listbox.ItemsSource = null;
            var param = parameters_listbox.SelectedItem as FilterParameterItem;
            var catIds = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().Select(c => c.Id).ToList();

            if (param == null || !catIds.Any())
            {
                UpdateStatus("Select a parameter to load its values.");
                return;
            }

            UpdateStatus("Collecting values...");
            try
            {
                var filter = new ElementMulticategoryFilter(catIds);
                var collector = new FilteredElementCollector(_doc).WherePasses(filter);

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var values = new List<FilterValueItem>();

                foreach (Element elem in collector)
                {
                    Parameter p = null;
                    
                    // Try built-in parameter first
                    if (Enum.IsDefined(typeof(BuiltInParameter), param.Id.IntegerValue))
                    {
                        p = elem.get_Parameter((BuiltInParameter)param.Id.IntegerValue);
                    }
                    
                    // Try as shared parameter or project parameter
                    if (p == null)
                    {
                        // Try to get by definition
                        foreach (Parameter elemParam in elem.Parameters)
                        {
                            if (elemParam.Id.IntegerValue == param.Id.IntegerValue)
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
                        values.Add(item);
                }

                _currentValues.Clear();
                _currentValues.AddRange(values);

                values_listbox.ItemsSource = values.OrderBy(v => v.Display);
                values_listbox.DisplayMemberPath = "Display";
                UpdateStatus($"Loaded {values.Count} unique value(s).");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading values: {ex.Message}");
            }
        }
        
        private FilterValueItem ExtractValueItem(Parameter param, Element owner, StorageType targetStorage, string paramName)
        {
            if (param == null || !param.HasValue) return null;
            switch (targetStorage)
            {
                case StorageType.String:
                    string text = param.AsString() ?? param.AsValueString();
                    return string.IsNullOrEmpty(text) ? null : new FilterValueItem(text, text, StorageType.String);

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

            string label = element.Name;
            if (!string.IsNullOrWhiteSpace(label)) return label;

            if (element is MechanicalSystemType)
            {
                Parameter nameParam = element.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM);
                if (nameParam != null)
                {
                    string name = nameParam.AsString();
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }

            if (paramName.IndexOf("System Type", StringComparison.OrdinalIgnoreCase) >= 0)
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
                    return LabelUtils.GetLabelFor((BuiltInParameter)paramId.IntegerValue);
                }
                catch { /* ignore */ }
            }

            return "Param " + paramId.IntegerValue;
        }


        private void UpdateStatus(string message)
        {
            status_text.Text = message;
        }

        private void ApplyViewButton_Click(object sender, RoutedEventArgs e)
        {
            var catIds = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().Select(c => c.Id).ToList();
            if (!catIds.Any())
            {
                TaskDialog.Show("Validation", "Please select at least one category.");
                return;
            }

            var param = parameters_listbox.SelectedItem as FilterParameterItem;
            if (param == null)
            {
                TaskDialog.Show("Validation", "Please select a parameter.");
                return;
            }
            
            var ruleType = GetSelectedRuleType();
            var values = values_listbox.SelectedItems.Cast<FilterValueItem>().ToList();
            
            if (values.Count == 0 && ruleType != RuleTypes.HasValue && ruleType != RuleTypes.HasNoValue)
            {
                TaskDialog.Show("Validation", "Please select at least one value for the chosen rule.");
                return;
            }

            var selection = new FilterSelection
            {
                CategoryIds = catIds,
                Parameter = param,
                Values = values.Count == 0 ? new List<FilterValueItem> { new FilterValueItem("Any", null, param.StorageType) } : values,
                RuleType = ruleType,
                ApplyToView = true,
                OverrideExisting = override_existing_checkbox.IsChecked == true,
                RandomColors = false,
                Prefix = prefix_textbox.Text,
                Suffix = suffix_textbox.Text,
                IncludeCategory = include_cat_checkbox.IsChecked == true,
                IncludeParameter = include_param_checkbox.IsChecked == true
            };

            var skipped = new List<string>();
            int created = 0;
            try
            {
                using (var t = new Transaction(_doc, "Create and Apply Filters"))
                {
                    t.Start();
                    created = FilterProHelper.CreateFilters(_doc, _activeView, selection, skipped);
                    t.Commit();
                }

                string info = $"{created} filter(s) created and applied to view.";
                if (skipped.Any())
                {
                    info += $"\n\nSkipped:\n- {string.Join("\n- ", skipped)}";
                }
                TaskDialog.Show("Filter Pro Results", info);
                UpdateStatus($"{created} created and applied, {skipped.Count} skipped.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to create filters: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        private void ShuffleColorsButton_Click(object sender, RoutedEventArgs e)
        {
            var catIds = categories_listbox.SelectedItems.Cast<FilterCategoryItem>().Select(c => c.Id).ToList();
            if (!catIds.Any())
            {
                TaskDialog.Show("Validation", "Please select at least one category.");
                return;
            }

            var param = parameters_listbox.SelectedItem as FilterParameterItem;
            if (param == null)
            {
                TaskDialog.Show("Validation", "Please select a parameter.");
                return;
            }
            
            var ruleType = GetSelectedRuleType();
            var values = values_listbox.SelectedItems.Cast<FilterValueItem>().ToList();
            
            if (values.Count == 0 && ruleType != RuleTypes.HasValue && ruleType != RuleTypes.HasNoValue)
            {
                TaskDialog.Show("Validation", "Please select at least one value for the chosen rule.");
                return;
            }

            var selection = new FilterSelection
            {
                CategoryIds = catIds,
                Parameter = param,
                Values = values.Count == 0 ? new List<FilterValueItem> { new FilterValueItem("Any", null, param.StorageType) } : values,
                RuleType = ruleType,
                ApplyToView = true,
                OverrideExisting = override_existing_checkbox.IsChecked == true,
                RandomColors = true,
                Prefix = prefix_textbox.Text,
                Suffix = suffix_textbox.Text,
                IncludeCategory = include_cat_checkbox.IsChecked == true,
                IncludeParameter = include_param_checkbox.IsChecked == true
            };

            var skipped = new List<string>();
            int created = 0;
            try
            {
                using (var t = new Transaction(_doc, "Create Filters with Random Colors"))
                {
                    t.Start();
                    created = FilterProHelper.CreateFilters(_doc, _activeView, selection, skipped);
                    t.Commit();
                }

                string info = $"{created} filter(s) created with random colors.";
                if (skipped.Any())
                {
                    info += $"\n\nSkipped:\n- {string.Join("\n- ", skipped)}";
                }
                TaskDialog.Show("Filter Pro Results", info);
                UpdateStatus($"{created} created with random colors, {skipped.Count} skipped.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to create filters: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
            }
        }
    }

    // Data model classes migrated from the old FilterProForm
    internal class FilterSelection
    {
        public IList<ElementId> CategoryIds { get; set; }
        public FilterParameterItem Parameter { get; set; }
        public IList<FilterValueItem> Values { get; set; }
        public string RuleType { get; set; }
        public bool ApplyToView { get; set; }
        public bool OverrideExisting { get; set; }
        public bool RandomColors { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public bool IncludeCategory { get; set; }
        public bool IncludeParameter { get; set; }
    }

    internal static class RuleTypes
    {
        public const string Equals = "equals";
        public const string NotEquals = "not_equals";
        public const string Contains = "contains";
        public const string NotContains = "not_contains";
        public const string BeginsWith = "begins_with";
        public const string NotBeginsWith = "not_begins_with";
        public const string EndsWith = "ends_with";
        public const string NotEndsWith = "not_ends_with";
        public const string Greater = "greater";
        public const string GreaterOrEqual = "greater_or_equal";
        public const string Less = "less";
        public const string LessOrEqual = "less_or_equal";
        public const string HasValue = "has_value";
        public const string HasNoValue = "has_no_value";
    }

    internal class FilterCategoryItem
    {
        public FilterCategoryItem(ElementId id, string name)
        {
            Id = id;
            Name = name;
        }

        public ElementId Id { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }

    internal class FilterParameterItem
    {
        public FilterParameterItem(ElementId id, string name, StorageType storageType)
        {
            Id = id;
            Name = name;
            StorageType = storageType;
        }

        public ElementId Id { get; }
        public string Name { get; }
        public StorageType StorageType { get; }

        public override string ToString() => Name;
    }

    internal class FilterValueItem
    {
        public FilterValueItem(string display, object rawValue, StorageType storageType, ElementId elementId = null)
        {
            Display = display;
            RawValue = rawValue;
            StorageType = storageType;
            ElementId = elementId;
        }

        public string Display { get; }
        public object RawValue { get; }
        public StorageType StorageType { get; }
        public ElementId ElementId { get; }
        public override string ToString() => Display;
    }

    internal static class ColorPalette
    {
        private static readonly Random _rand = new Random();
        private static readonly Color[] Palette =
        {
            new Color((byte)230, (byte)126, (byte)34),
            new Color((byte)52, (byte)152, (byte)219),
            new Color((byte)46, (byte)204, (byte)113),
            new Color((byte)155, (byte)89, (byte)182),
            new Color((byte)241, (byte)196, (byte)15),
            new Color((byte)26, (byte)188, (byte)156),
            new Color((byte)231, (byte)76, (byte)60),
            new Color((byte)149, (byte)165, (byte)166),
            new Color((byte)99, (byte)110, (byte)114),
            new Color((byte)127, (byte)140, (byte)141)
        };

        public static Color GetColorFor(ElementId id)
        {
            if (id == null || id.IntegerValue == 0)
                return Palette[0];
            int index = Math.Abs(id.IntegerValue);
            return Palette[index % Palette.Length];
        }

        public static Color GetRandomColor()
        {
            int idx = _rand.Next(Palette.Length);
            return Palette[idx];
        }
    }

    internal class RuleTypeItem
    {
        public RuleTypeItem(string key, string label, bool enabledForStrings, bool enabledForNumbers, bool enabledForIds)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }
}
