// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Handles the Apply Graphics window behavior and input conversion.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.5
// Created      : 2026-03-30
// Last Updated : 2026-05-10
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : User graphics settings selections, apply mode choice, selected source categories, and category selections.
// Output       : Selected Revit OverrideGraphicSettings and apply-mode data for command execution.
// Notes        : Keeps Revit override construction outside the WPF UI layer.
// Changelog    : v1.4.5 - Clamped startup size to the screen work area and kept native close/resize behavior.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;
using AJTools.Services.GraphicsTools;
using Forms = System.Windows.Forms;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace AJTools.UI.GraphicsTools
{
    /// <summary>
    /// Graphics settings dialog used by the Apply Graphics command.
    /// </summary>
    public partial class GraphicsOverrideWindow : Window
    {
        private sealed class CutOverrideState
        {
            public GraphicsColorValue CutLineColor { get; set; }

            public ElementId CutLinePatternId { get; set; }

            public int CutLineWeight { get; set; }

            public GraphicsColorValue CutForegroundColor { get; set; }

            public ElementId CutForegroundPatternId { get; set; }

            public GraphicsColorValue CutBackgroundColor { get; set; }

            public ElementId CutBackgroundPatternId { get; set; }
        }

        private sealed class ColorPreset
        {
            public ColorPreset(string name, byte red, byte green, byte blue)
            {
                Name = name;
                Red = red;
                Green = green;
                Blue = blue;

                Brush = new SolidColorBrush(MediaColor.FromRgb(red, green, blue));
                Brush.Freeze();
            }

            public string Name { get; }

            public byte Red { get; }

            public byte Green { get; }

            public byte Blue { get; }

            public Brush Brush { get; }

            public string TagValue
            {
                get
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2}",
                        Red,
                        Green,
                        Blue);
                }
            }
        }

        private const string ProjectionLineColorKey = "ProjectionLineColor";
        private const string SurfaceForegroundColorKey = "SurfaceForegroundColor";
        private const string SurfaceBackgroundColorKey = "SurfaceBackgroundColor";
        private const string CutLineColorKey = "CutLineColor";
        private const string CutForegroundColorKey = "CutForegroundColor";
        private const string CutBackgroundColorKey = "CutBackgroundColor";
        private const double WorkAreaMargin = 48.0;

        private static readonly Brush FieldBrush = CreateFrozenBrush(37, 37, 38);

        private static readonly IList<ColorPreset> ColorPresets = new List<ColorPreset>
        {
            new ColorPreset("Red", 255, 0, 0),
            new ColorPreset("Blue", 0, 0, 255),
            new ColorPreset("Green", 0, 255, 0),
            new ColorPreset("Cyan", 0, 255, 255),
            new ColorPreset("Magenta", 255, 0, 255),
            new ColorPreset("Yellow", 255, 255, 0),
            new ColorPreset("Orange", 255, 165, 0),
            new ColorPreset("Dark Red", 139, 0, 0),
            new ColorPreset("Dark Blue", 0, 0, 139),
            new ColorPreset("Dark Green", 0, 100, 0),
            new ColorPreset("Purple", 128, 0, 128),
            new ColorPreset("Brown", 165, 42, 42),
            new ColorPreset("Black", 0, 0, 0),
            new ColorPreset("Silver", 192, 192, 192)
        };

        private readonly Dictionary<string, GraphicsColorValue> _colorValues =
            new Dictionary<string, GraphicsColorValue>(StringComparer.Ordinal)
            {
                { ProjectionLineColorKey, GraphicsColorValue.ByView() },
                { SurfaceForegroundColorKey, GraphicsColorValue.ByView() },
                { SurfaceBackgroundColorKey, GraphicsColorValue.ByView() },
                { CutLineColorKey, GraphicsColorValue.ByView() },
                { CutForegroundColorKey, GraphicsColorValue.ByView() },
                { CutBackgroundColorKey, GraphicsColorValue.ByView() }
            };

        private readonly IList<GraphicsCategoryOption> _categoryOptions;
        private readonly ISet<int> _initialSelectedCategoryKeys;
        private ICollectionView _categoryOptionsView;
        private bool _isCutSettingsLinked;
        private CutOverrideState _manualCutState;

        public GraphicsOverrideWindow(Document doc, string windowTitle, OverrideGraphicSettings initialSettings = null)
            : this(doc, doc?.ActiveView, windowTitle, null, null, initialSettings)
        {
        }

        public GraphicsOverrideWindow(
            Document doc,
            View activeView,
            string windowTitle,
            ICollection<Category> availableCategories,
            ICollection<ElementId> preselectedCategoryIds,
            OverrideGraphicSettings initialSettings = null)
        {
            InitializeComponent();
            ApplyInitialWindowBounds();

            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                Title = windowTitle;
            }

            _categoryOptions = GraphicsDataProvider.GetCategoryOptions(availableCategories, preselectedCategoryIds);
            _initialSelectedCategoryKeys = new HashSet<int>(
                _categoryOptions
                    .Where(option => option.IsSelected)
                    .Select(option => option.CategoryId.IntegerValue));

            BindDropdownData(doc);
            PopulateColorPresetPanels();
            BindCategoryData();

            GraphicsOverrideMemoryState memoryState = initialSettings == null
                ? GraphicsOverrideMemoryService.Load()
                : null;
            if (initialSettings != null)
            {
                ApplySettings(initialSettings, inferCutLink: true);
            }
            else if (memoryState != null)
            {
                ApplyMemorySettings(memoryState);
            }
            else
            {
                ApplySettings(new OverrideGraphicSettings(), inferCutLink: true);
            }

            UpdateCategorySearchPlaceholder();
        }

        public OverrideGraphicSettings SelectedOverrideSettings { get; private set; }

        internal GraphicsApplyMode SelectedApplyMode { get; private set; }

        public IList<ElementId> SelectedCategoryIds
        {
            get
            {
                return _categoryOptions
                    .Where(option => option.IsSelected)
                    .Select(option => option.CategoryId)
                    .ToList();
            }
        }

        private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(MediaColor.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        private void ApplyInitialWindowBounds()
        {
            Rect workArea = SystemParameters.WorkArea;
            if (workArea.Width > WorkAreaMargin)
            {
                MaxWidth = Math.Max(MinWidth, workArea.Width - WorkAreaMargin);
                Width = Math.Min(Width, MaxWidth);
            }

            if (workArea.Height > WorkAreaMargin)
            {
                MaxHeight = Math.Max(MinHeight, workArea.Height - WorkAreaMargin);
                Height = Math.Min(Height, MaxHeight);
            }
        }

        private void BindDropdownData(Document doc)
        {
            IList<GraphicsIdOption> linePatternOptions = GraphicsDataProvider.GetLinePatternOptions(doc);
            IList<GraphicsIdOption> fillPatternOptions = GraphicsDataProvider.GetFillPatternOptions(doc);
            IList<GraphicsLineWeightOption> lineWeightOptions = GraphicsDataProvider.GetLineWeightOptions();

            BindIdOptionCombo(ProjectionLinePatternCombo, linePatternOptions);
            BindIdOptionCombo(CutLinePatternCombo, linePatternOptions);
            BindIdOptionCombo(SurfaceForegroundPatternCombo, fillPatternOptions);
            BindIdOptionCombo(SurfaceBackgroundPatternCombo, fillPatternOptions);
            BindIdOptionCombo(CutForegroundPatternCombo, fillPatternOptions);
            BindIdOptionCombo(CutBackgroundPatternCombo, fillPatternOptions);

            BindLineWeightCombo(ProjectionLineWeightCombo, lineWeightOptions);
            BindLineWeightCombo(CutLineWeightCombo, lineWeightOptions);

            TransparencySlider.Value = 0;
            UpdateTransparencyText();
        }

        private void BindCategoryData()
        {
            _categoryOptionsView = CollectionViewSource.GetDefaultView(_categoryOptions);
            if (_categoryOptionsView != null)
            {
                _categoryOptionsView.Filter = FilterCategoryOption;
            }

            if (_categoryOptionsView != null)
            {
                CategoryListBox.ItemsSource = _categoryOptionsView;
            }
            else
            {
                CategoryListBox.ItemsSource = _categoryOptions;
            }
        }

        private void PopulateColorPresetPanels()
        {
            PopulateColorPresetPanel(ProjectionLineColorPresetPanel, ProjectionLineColorKey);
            PopulateColorPresetPanel(SurfaceForegroundColorPresetPanel, SurfaceForegroundColorKey);
            PopulateColorPresetPanel(SurfaceBackgroundColorPresetPanel, SurfaceBackgroundColorKey);
            PopulateColorPresetPanel(CutLineColorPresetPanel, CutLineColorKey);
            PopulateColorPresetPanel(CutForegroundColorPresetPanel, CutForegroundColorKey);
            PopulateColorPresetPanel(CutBackgroundColorPresetPanel, CutBackgroundColorKey);
        }

        private void PopulateColorPresetPanel(System.Windows.Controls.Panel panel, string colorKey)
        {
            if (panel == null || string.IsNullOrWhiteSpace(colorKey))
            {
                return;
            }

            panel.Children.Clear();
            Style swatchStyle = TryFindResource("ColorSwatchButtonStyle") as Style;
            Style byViewStyle = TryFindResource("SmallSecondaryButtonStyle") as Style;

            foreach (ColorPreset preset in ColorPresets)
            {
                var button = new Button
                {
                    Style = swatchStyle,
                    Background = preset.Brush,
                    ToolTip = preset.Name,
                    Tag = string.Format(CultureInfo.InvariantCulture, "{0}|{1}", colorKey, preset.TagValue)
                };
                button.Click += OnColorPreset;
                panel.Children.Add(button);
            }

            var byViewButton = new Button
            {
                Content = "BY VIEW",
                Style = byViewStyle,
                Tag = colorKey + "|ByView",
                Height = 18,
                MinHeight = 18,
                MinWidth = 64,
                Padding = new Thickness(7, 0, 7, 0),
                Margin = new Thickness(2, 0, 0, 0),
                FontSize = 8,
                ToolTip = "By View"
            };
            byViewButton.Click += OnColorPreset;
            panel.Children.Add(byViewButton);
        }

        private bool FilterCategoryOption(object item)
        {
            GraphicsCategoryOption option = item as GraphicsCategoryOption;
            if (option == null)
            {
                return false;
            }

            string searchText = CategorySearchBox == null ? string.Empty : CategorySearchBox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            return option.DisplayName.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static void BindIdOptionCombo(ComboBox comboBox, IList<GraphicsIdOption> options)
        {
            comboBox.ItemsSource = options;
            comboBox.DisplayMemberPath = nameof(GraphicsIdOption.DisplayName);
            comboBox.SelectedIndex = 0;
        }

        private static void BindLineWeightCombo(ComboBox comboBox, IList<GraphicsLineWeightOption> options)
        {
            comboBox.ItemsSource = options;
            comboBox.DisplayMemberPath = nameof(GraphicsLineWeightOption.DisplayName);
            comboBox.SelectedIndex = 0;
        }

        private void ApplySettings(OverrideGraphicSettings settings, bool inferCutLink)
        {
            var safeSettings = settings ?? new OverrideGraphicSettings();

            _colorValues[ProjectionLineColorKey] = GraphicsColorValue.FromRevitColor(safeSettings.ProjectionLineColor);
            _colorValues[SurfaceForegroundColorKey] = GraphicsColorValue.FromRevitColor(safeSettings.SurfaceForegroundPatternColor);
            _colorValues[SurfaceBackgroundColorKey] = GraphicsColorValue.FromRevitColor(safeSettings.SurfaceBackgroundPatternColor);
            _colorValues[CutLineColorKey] = GraphicsColorValue.FromRevitColor(safeSettings.CutLineColor);
            _colorValues[CutForegroundColorKey] = GraphicsColorValue.FromRevitColor(safeSettings.CutForegroundPatternColor);
            _colorValues[CutBackgroundColorKey] = GraphicsColorValue.FromRevitColor(safeSettings.CutBackgroundPatternColor);

            SelectIdOption(ProjectionLinePatternCombo, safeSettings.ProjectionLinePatternId);
            SelectIdOption(SurfaceForegroundPatternCombo, safeSettings.SurfaceForegroundPatternId);
            SelectIdOption(SurfaceBackgroundPatternCombo, safeSettings.SurfaceBackgroundPatternId);
            SelectIdOption(CutLinePatternCombo, safeSettings.CutLinePatternId);
            SelectIdOption(CutForegroundPatternCombo, safeSettings.CutForegroundPatternId);
            SelectIdOption(CutBackgroundPatternCombo, safeSettings.CutBackgroundPatternId);

            SelectLineWeightOption(ProjectionLineWeightCombo, safeSettings.ProjectionLineWeight);
            SelectLineWeightOption(CutLineWeightCombo, safeSettings.CutLineWeight);

            int transparency = safeSettings.Transparency;
            TransparencySlider.Value = transparency >= 0 && transparency <= 100 ? transparency : 0;
            UpdateTransparencyText();

            HalftoneCheckBox.IsChecked = safeSettings.Halftone;
            _manualCutState = CaptureCurrentCutState();
            UseProjectionSurfaceColorsForCutCheckBox.IsChecked = inferCutLink && IsCutLinkedToProjectionSurface(safeSettings);
            RefreshAllColorVisuals();
            ApplyCutColorLinkState(preserveManualState: false);
            SetDefaultApplyMode(GraphicsApplyMode.SelectedElements);
        }

        private void ApplyMemorySettings(GraphicsOverrideMemoryState state)
        {
            if (state == null)
            {
                ApplySettings(new OverrideGraphicSettings(), inferCutLink: false);
                return;
            }

            _colorValues[ProjectionLineColorKey] = ToGraphicsColor(state.ProjectionLineColor);
            _colorValues[SurfaceForegroundColorKey] = ToGraphicsColor(state.SurfaceForegroundColor);
            _colorValues[SurfaceBackgroundColorKey] = ToGraphicsColor(state.SurfaceBackgroundColor);
            _colorValues[CutLineColorKey] = ToGraphicsColor(state.CutLineColor);
            _colorValues[CutForegroundColorKey] = ToGraphicsColor(state.CutForegroundColor);
            _colorValues[CutBackgroundColorKey] = ToGraphicsColor(state.CutBackgroundColor);

            SelectIdOption(ProjectionLinePatternCombo, state.ProjectionLinePattern);
            SelectIdOption(SurfaceForegroundPatternCombo, state.SurfaceForegroundPattern);
            SelectIdOption(SurfaceBackgroundPatternCombo, state.SurfaceBackgroundPattern);
            SelectIdOption(CutLinePatternCombo, state.CutLinePattern);
            SelectIdOption(CutForegroundPatternCombo, state.CutForegroundPattern);
            SelectIdOption(CutBackgroundPatternCombo, state.CutBackgroundPattern);

            SelectLineWeightOption(ProjectionLineWeightCombo, state.ProjectionLineWeight);
            SelectLineWeightOption(CutLineWeightCombo, state.CutLineWeight);

            TransparencySlider.Value = ClampTransparency(state.Transparency);
            UpdateTransparencyText();

            HalftoneCheckBox.IsChecked = state.Halftone;
            RestoreMemoryCategorySelection(state);

            _manualCutState = CaptureCurrentCutState();
            UseProjectionSurfaceColorsForCutCheckBox.IsChecked = state.UseProjectionSurfaceSettingsForCut;
            RefreshAllColorVisuals();
            ApplyCutColorLinkState(preserveManualState: false);
            SetDefaultApplyMode(state.LastApplyMode);
        }

        private void SetDefaultApplyMode(GraphicsApplyMode applyMode)
        {
            bool categoryMode = applyMode == GraphicsApplyMode.Categories;
            ApplyElementsButton.IsDefault = !categoryMode;
            ApplyCategoriesButton.IsDefault = categoryMode;
        }

        private static GraphicsColorValue ToGraphicsColor(GraphicsColorMemoryValue memoryValue)
        {
            return memoryValue == null
                ? GraphicsColorValue.ByView()
                : memoryValue.ToGraphicsColor();
        }

        private static void SelectIdOption(ComboBox comboBox, ElementId id)
        {
            IEnumerable<GraphicsIdOption> options = comboBox.ItemsSource as IEnumerable<GraphicsIdOption>;
            if (options == null)
            {
                return;
            }

            int targetId = (id ?? ElementId.InvalidElementId).IntegerValue;
            GraphicsIdOption match = options.FirstOrDefault(option => option.Id.IntegerValue == targetId);
            comboBox.SelectedItem = match ?? options.FirstOrDefault();
        }

        private static void SelectIdOption(ComboBox comboBox, GraphicsIdMemoryValue memoryValue)
        {
            IEnumerable<GraphicsIdOption> options = comboBox.ItemsSource as IEnumerable<GraphicsIdOption>;
            if (options == null)
            {
                return;
            }

            IList<GraphicsIdOption> optionList = options.ToList();
            if (optionList.Count == 0)
            {
                return;
            }

            GraphicsIdOption match = null;
            if (memoryValue != null)
            {
                match = optionList.FirstOrDefault(option =>
                    option.Id != null &&
                    option.Id.IntegerValue == memoryValue.IntegerValue);

                if (match == null && !string.IsNullOrWhiteSpace(memoryValue.DisplayName))
                {
                    match = optionList.FirstOrDefault(option =>
                        string.Equals(
                            option.DisplayName,
                            memoryValue.DisplayName,
                            StringComparison.CurrentCultureIgnoreCase));
                }
            }

            comboBox.SelectedItem = match ?? optionList.FirstOrDefault();
        }

        private static void SelectLineWeightOption(ComboBox comboBox, int lineWeight)
        {
            IEnumerable<GraphicsLineWeightOption> options = comboBox.ItemsSource as IEnumerable<GraphicsLineWeightOption>;
            if (options == null)
            {
                return;
            }

            GraphicsLineWeightOption match = options.FirstOrDefault(option => option.Weight == lineWeight);
            comboBox.SelectedItem = match ?? options.FirstOrDefault();
        }

        private void RefreshAllColorVisuals()
        {
            UpdateColorVisual(ProjectionLineColorKey);
            UpdateColorVisual(SurfaceForegroundColorKey);
            UpdateColorVisual(SurfaceBackgroundColorKey);
            UpdateColorVisual(CutLineColorKey);
            UpdateColorVisual(CutForegroundColorKey);
            UpdateColorVisual(CutBackgroundColorKey);
        }

        private void UpdateColorVisual(string key)
        {
            if (!TryGetColorPreview(key, out Border preview))
            {
                return;
            }

            GraphicsColorValue value = GetColorValue(key);

            if (value.IsByView)
            {
                preview.Background = FieldBrush;
                preview.ToolTip = "By View";
                return;
            }

            MediaColor mediaColor = MediaColor.FromRgb(value.Red, value.Green, value.Blue);
            preview.Background = new SolidColorBrush(mediaColor);
            preview.ToolTip = ResolveColorDisplayName(value);
        }

        private static string ResolveColorDisplayName(GraphicsColorValue value)
        {
            if (value == null || value.IsByView)
            {
                return "By View";
            }

            ColorPreset match = ColorPresets.FirstOrDefault(
                preset => preset.Red == value.Red &&
                          preset.Green == value.Green &&
                          preset.Blue == value.Blue);

            return match != null
                ? match.Name
                : string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", value.Red, value.Green, value.Blue);
        }

        private bool TryGetColorPreview(string key, out Border preview)
        {
            preview = null;

            switch (key)
            {
                case ProjectionLineColorKey:
                    preview = ProjectionLineColorPreview;
                    break;
                case SurfaceForegroundColorKey:
                    preview = SurfaceForegroundColorPreview;
                    break;
                case SurfaceBackgroundColorKey:
                    preview = SurfaceBackgroundColorPreview;
                    break;
                case CutLineColorKey:
                    preview = CutLineColorPreview;
                    break;
                case CutForegroundColorKey:
                    preview = CutForegroundColorPreview;
                    break;
                case CutBackgroundColorKey:
                    preview = CutBackgroundColorPreview;
                    break;
            }

            return preview != null;
        }

        private GraphicsColorValue GetColorValue(string key)
        {
            return _colorValues.ContainsKey(key)
                ? _colorValues[key]
                : GraphicsColorValue.ByView();
        }

        private void OnSelectAllCategories(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _categoryOptions.Count; i++)
            {
                _categoryOptions[i].IsSelected = true;
            }

            RefreshCategoryList();
        }

        private void OnClearCategories(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _categoryOptions.Count; i++)
            {
                _categoryOptions[i].IsSelected = false;
            }

            RefreshCategoryList();
        }

        private void OnCategorySearchChanged(object sender, TextChangedEventArgs e)
        {
            if (_categoryOptionsView != null)
            {
                _categoryOptionsView.Refresh();
            }

            UpdateCategorySearchPlaceholder();
        }

        private void OnCategorySearchFocusChanged(object sender, RoutedEventArgs e)
        {
            UpdateCategorySearchPlaceholder();
        }

        private void UpdateCategorySearchPlaceholder()
        {
            if (CategorySearchPlaceholderText == null || CategorySearchBox == null)
            {
                return;
            }

            bool showPlaceholder = string.IsNullOrEmpty(CategorySearchBox.Text) &&
                                   !CategorySearchBox.IsKeyboardFocusWithin;
            CategorySearchPlaceholderText.Visibility = showPlaceholder
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void RefreshCategoryList()
        {
            if (_categoryOptionsView != null)
            {
                _categoryOptionsView.Refresh();
                return;
            }

            CategoryListBox.Items.Refresh();
        }

        private void RestoreInitialCategorySelection()
        {
            for (int i = 0; i < _categoryOptions.Count; i++)
            {
                GraphicsCategoryOption option = _categoryOptions[i];
                option.IsSelected = _initialSelectedCategoryKeys.Contains(option.CategoryId.IntegerValue);
            }

            RefreshCategoryList();
        }

        private void RestoreMemoryCategorySelection(GraphicsOverrideMemoryState state)
        {
            if (state == null || !state.HasCategorySelection)
            {
                return;
            }

            var selectedIds = new HashSet<int>(state.SelectedCategoryIntegerIds ?? new List<int>());
            var selectedNames = new HashSet<string>(
                (state.SelectedCategoryNames ?? new List<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.CurrentCultureIgnoreCase);

            if (selectedIds.Count == 0 && selectedNames.Count == 0)
            {
                for (int i = 0; i < _categoryOptions.Count; i++)
                {
                    _categoryOptions[i].IsSelected = false;
                }

                RefreshCategoryList();
                return;
            }

            bool matchedAnyCategory = false;
            for (int i = 0; i < _categoryOptions.Count; i++)
            {
                GraphicsCategoryOption option = _categoryOptions[i];
                bool isSelected =
                    selectedIds.Contains(option.CategoryId.IntegerValue) ||
                    selectedNames.Contains(option.DisplayName);

                option.IsSelected = isSelected;
                matchedAnyCategory = matchedAnyCategory || isSelected;
            }

            if (!matchedAnyCategory)
            {
                RestoreInitialCategorySelection();
                return;
            }

            RefreshCategoryList();
        }

        private void OnPickColor(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string key = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            GraphicsColorValue current = GetColorValue(key);
            using (var dialog = new Forms.ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.Color = current.IsByView
                    ? DrawingColor.White
                    : DrawingColor.FromArgb(current.Red, current.Green, current.Blue);

                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                {
                    return;
                }

                _colorValues[key] = GraphicsColorValue.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                UpdateColorVisual(key);
                HandleProjectionSurfaceDependencies(key);
            }
        }

        private void OnClearColor(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string key = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _colorValues[key] = GraphicsColorValue.ByView();
            UpdateColorVisual(key);
            HandleProjectionSurfaceDependencies(key);
        }

        private void OnColorPreset(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string tagValue = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(tagValue))
            {
                return;
            }

            string[] segments = tagValue.Split('|');
            if (segments.Length != 2)
            {
                return;
            }

            string key = segments[0];
            string presetValue = segments[1];

            if (string.Equals(presetValue, "ByView", StringComparison.OrdinalIgnoreCase))
            {
                _colorValues[key] = GraphicsColorValue.ByView();
            }
            else
            {
                if (!TryParseRgb(presetValue, out byte red, out byte green, out byte blue))
                {
                    return;
                }

                _colorValues[key] = GraphicsColorValue.FromRgb(red, green, blue);
            }

            UpdateColorVisual(key);
            HandleProjectionSurfaceDependencies(key);
        }

        private static bool TryParseRgb(string value, out byte red, out byte green, out byte blue)
        {
            red = 0;
            green = 0;
            blue = 0;

            string[] segments = value.Split(',');
            if (segments.Length != 3)
            {
                return false;
            }

            return byte.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out red) &&
                   byte.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out green) &&
                   byte.TryParse(segments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out blue);
        }

        private void OnProjectionSurfaceSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleProjectionSurfaceDependencies(null);
        }

        private void OnTransparencyChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTransparencyText();
        }

        private void UpdateTransparencyText()
        {
            if (TransparencyValueText == null || TransparencySlider == null)
            {
                return;
            }

            TransparencyValueText.Text = string.Format(CultureInfo.InvariantCulture, "{0}%", ResolveTransparency());
        }

        private void OnUseProjectionSurfaceColorsForCutChanged(object sender, RoutedEventArgs e)
        {
            ApplyCutColorLinkState(preserveManualState: true);
        }

        private void ApplyCutColorLinkState(bool preserveManualState)
        {
            bool useProjectionSurfaceSettings = UseProjectionSurfaceColorsForCutCheckBox.IsChecked == true;

            if (useProjectionSurfaceSettings)
            {
                if (!_isCutSettingsLinked && preserveManualState)
                {
                    _manualCutState = CaptureCurrentCutState();
                }

                ApplyLinkedCutStateFromProjectionSurface();
                _isCutSettingsLinked = true;
            }
            else
            {
                if (_isCutSettingsLinked && _manualCutState != null)
                {
                    RestoreCutState(_manualCutState);
                }

                _isCutSettingsLinked = false;
            }

            CutSettingsPanel.Visibility = useProjectionSurfaceSettings
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
            CutLinkNoticeText.Visibility = useProjectionSurfaceSettings
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void ApplyLinkedCutStateFromProjectionSurface()
        {
            _colorValues[CutLineColorKey] = GetColorValue(ProjectionLineColorKey);
            _colorValues[CutForegroundColorKey] = GetColorValue(SurfaceForegroundColorKey);
            _colorValues[CutBackgroundColorKey] = GetColorValue(SurfaceBackgroundColorKey);

            SelectIdOption(CutLinePatternCombo, GetSelectedId(ProjectionLinePatternCombo));
            SelectLineWeightOption(CutLineWeightCombo, GetSelectedLineWeight(ProjectionLineWeightCombo));
            SelectIdOption(CutForegroundPatternCombo, GetSelectedId(SurfaceForegroundPatternCombo));
            SelectIdOption(CutBackgroundPatternCombo, GetSelectedId(SurfaceBackgroundPatternCombo));

            UpdateColorVisual(CutLineColorKey);
            UpdateColorVisual(CutForegroundColorKey);
            UpdateColorVisual(CutBackgroundColorKey);
        }

        private void HandleProjectionSurfaceDependencies(string changedKey)
        {
            if (UseProjectionSurfaceColorsForCutCheckBox.IsChecked != true)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(changedKey) ||
                changedKey == ProjectionLineColorKey ||
                changedKey == SurfaceForegroundColorKey ||
                changedKey == SurfaceBackgroundColorKey)
            {
                ApplyLinkedCutStateFromProjectionSurface();
            }
        }

        private CutOverrideState CaptureCurrentCutState()
        {
            return new CutOverrideState
            {
                CutLineColor = GetColorValue(CutLineColorKey),
                CutLinePatternId = GetSelectedId(CutLinePatternCombo),
                CutLineWeight = GetSelectedLineWeight(CutLineWeightCombo),
                CutForegroundColor = GetColorValue(CutForegroundColorKey),
                CutForegroundPatternId = GetSelectedId(CutForegroundPatternCombo),
                CutBackgroundColor = GetColorValue(CutBackgroundColorKey),
                CutBackgroundPatternId = GetSelectedId(CutBackgroundPatternCombo)
            };
        }

        private void RestoreCutState(CutOverrideState state)
        {
            if (state == null)
            {
                return;
            }

            _colorValues[CutLineColorKey] = state.CutLineColor ?? GraphicsColorValue.ByView();
            _colorValues[CutForegroundColorKey] = state.CutForegroundColor ?? GraphicsColorValue.ByView();
            _colorValues[CutBackgroundColorKey] = state.CutBackgroundColor ?? GraphicsColorValue.ByView();

            SelectIdOption(CutLinePatternCombo, state.CutLinePatternId);
            SelectLineWeightOption(CutLineWeightCombo, state.CutLineWeight);
            SelectIdOption(CutForegroundPatternCombo, state.CutForegroundPatternId);
            SelectIdOption(CutBackgroundPatternCombo, state.CutBackgroundPatternId);

            UpdateColorVisual(CutLineColorKey);
            UpdateColorVisual(CutForegroundColorKey);
            UpdateColorVisual(CutBackgroundColorKey);
        }

        private bool IsCutLinkedToProjectionSurface(OverrideGraphicSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            return AreColorsEqual(settings.CutLineColor, settings.ProjectionLineColor) &&
                   AreElementIdsEqual(settings.CutLinePatternId, settings.ProjectionLinePatternId) &&
                   settings.CutLineWeight == settings.ProjectionLineWeight &&
                   AreColorsEqual(settings.CutForegroundPatternColor, settings.SurfaceForegroundPatternColor) &&
                   AreElementIdsEqual(settings.CutForegroundPatternId, settings.SurfaceForegroundPatternId) &&
                   AreColorsEqual(settings.CutBackgroundPatternColor, settings.SurfaceBackgroundPatternColor) &&
                   AreElementIdsEqual(settings.CutBackgroundPatternId, settings.SurfaceBackgroundPatternId);
        }

        private static bool AreColorsEqual(Autodesk.Revit.DB.Color first, Autodesk.Revit.DB.Color second)
        {
            bool firstValid = first != null && first.IsValid;
            bool secondValid = second != null && second.IsValid;
            if (!firstValid && !secondValid)
            {
                return true;
            }

            if (firstValid != secondValid)
            {
                return false;
            }

            return first.Red == second.Red &&
                   first.Green == second.Green &&
                   first.Blue == second.Blue;
        }

        private static bool AreElementIdsEqual(ElementId first, ElementId second)
        {
            int firstValue = (first ?? ElementId.InvalidElementId).IntegerValue;
            int secondValue = (second ?? ElementId.InvalidElementId).IntegerValue;
            return firstValue == secondValue;
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;
            CategorySearchBox.Text = string.Empty;
            GraphicsTabControl.SelectedIndex = 0;
            ApplySettings(new OverrideGraphicSettings(), inferCutLink: true);
            RestoreInitialCategorySelection();
            UpdateCategorySearchPlaceholder();
        }

        private void OnApplyElements(object sender, RoutedEventArgs e)
        {
            ApplyAndClose(GraphicsApplyMode.SelectedElements);
        }

        private void OnApplyCategories(object sender, RoutedEventArgs e)
        {
            ApplyAndClose(GraphicsApplyMode.Categories);
        }

        private void ApplyAndClose(GraphicsApplyMode applyMode)
        {
            ErrorText.Text = string.Empty;

            if (!TryBuildInput(applyMode, out GraphicsOverrideInput input, out string errorMessage))
            {
                ErrorText.Text = errorMessage;
                return;
            }

            SelectedApplyMode = input.ApplyMode;
            SelectedOverrideSettings = GraphicsOverrideBuilder.Build(input);
            GraphicsOverrideMemoryService.Save(CreateMemoryState(input));
            CloseDialog(true);
        }

        private bool TryBuildInput(GraphicsApplyMode applyMode, out GraphicsOverrideInput input, out string errorMessage)
        {
            input = new GraphicsOverrideInput();
            errorMessage = string.Empty;

            if (applyMode == GraphicsApplyMode.Categories && SelectedCategoryIds.Count == 0)
            {
                errorMessage = "Select at least one category before applying category graphics.";
                CategorySearchBox.Focus();
                return false;
            }

            int transparency = ResolveTransparency();
            if (transparency < 0 || transparency > 100)
            {
                errorMessage = "Transparency must be a number between 0 and 100.";
                return false;
            }

            input.ApplyMode = applyMode;
            input.UseProjectionSurfaceSettingsForCut = UseProjectionSurfaceColorsForCutCheckBox.IsChecked == true;

            input.ProjectionLineColor = GetColorValue(ProjectionLineColorKey);
            input.ProjectionLinePatternId = GetSelectedId(ProjectionLinePatternCombo);
            input.ProjectionLineWeight = GetSelectedLineWeight(ProjectionLineWeightCombo);

            input.SurfaceForegroundPatternId = GetSelectedId(SurfaceForegroundPatternCombo);
            input.SurfaceForegroundPatternColor = GetColorValue(SurfaceForegroundColorKey);
            input.SurfaceBackgroundPatternId = GetSelectedId(SurfaceBackgroundPatternCombo);
            input.SurfaceBackgroundPatternColor = GetColorValue(SurfaceBackgroundColorKey);
            input.Transparency = transparency;

            input.CutLineColor = GetColorValue(CutLineColorKey);
            input.CutLinePatternId = GetSelectedId(CutLinePatternCombo);
            input.CutLineWeight = GetSelectedLineWeight(CutLineWeightCombo);

            input.CutForegroundPatternId = GetSelectedId(CutForegroundPatternCombo);
            input.CutForegroundPatternColor = GetColorValue(CutForegroundColorKey);
            input.CutBackgroundPatternId = GetSelectedId(CutBackgroundPatternCombo);
            input.CutBackgroundPatternColor = GetColorValue(CutBackgroundColorKey);

            input.Halftone = HalftoneCheckBox.IsChecked == true;
            return true;
        }

        private GraphicsOverrideMemoryState CreateMemoryState(GraphicsOverrideInput input)
        {
            var state = new GraphicsOverrideMemoryState
            {
                LastApplyMode = input.ApplyMode,
                UseProjectionSurfaceSettingsForCut = input.UseProjectionSurfaceSettingsForCut,
                Halftone = input.Halftone,
                Transparency = input.Transparency,

                ProjectionLineColor = GraphicsColorMemoryValue.FromGraphicsColor(input.ProjectionLineColor),
                ProjectionLinePattern = GetSelectedIdMemory(ProjectionLinePatternCombo),
                ProjectionLineWeight = input.ProjectionLineWeight,

                SurfaceForegroundPattern = GetSelectedIdMemory(SurfaceForegroundPatternCombo),
                SurfaceForegroundColor = GraphicsColorMemoryValue.FromGraphicsColor(input.SurfaceForegroundPatternColor),
                SurfaceBackgroundPattern = GetSelectedIdMemory(SurfaceBackgroundPatternCombo),
                SurfaceBackgroundColor = GraphicsColorMemoryValue.FromGraphicsColor(input.SurfaceBackgroundPatternColor),

                CutLineColor = GraphicsColorMemoryValue.FromGraphicsColor(input.CutLineColor),
                CutLinePattern = GetSelectedIdMemory(CutLinePatternCombo),
                CutLineWeight = input.CutLineWeight,

                CutForegroundPattern = GetSelectedIdMemory(CutForegroundPatternCombo),
                CutForegroundColor = GraphicsColorMemoryValue.FromGraphicsColor(input.CutForegroundPatternColor),
                CutBackgroundPattern = GetSelectedIdMemory(CutBackgroundPatternCombo),
                CutBackgroundColor = GraphicsColorMemoryValue.FromGraphicsColor(input.CutBackgroundPatternColor),

                HasCategorySelection = true,
                SelectedCategoryIntegerIds = _categoryOptions
                    .Where(option => option.IsSelected)
                    .Select(option => option.CategoryId.IntegerValue)
                    .ToList(),
                SelectedCategoryNames = _categoryOptions
                    .Where(option => option.IsSelected)
                    .Select(option => option.DisplayName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList()
            };

            return state;
        }

        private int ResolveTransparency()
        {
            return (int)Math.Round(TransparencySlider.Value, MidpointRounding.AwayFromZero);
        }

        private static int ClampTransparency(int transparency)
        {
            if (transparency < 0)
            {
                return 0;
            }

            if (transparency > 100)
            {
                return 100;
            }

            return transparency;
        }

        private static ElementId GetSelectedId(ComboBox comboBox)
        {
            GraphicsIdOption selected = comboBox.SelectedItem as GraphicsIdOption;
            return selected?.Id ?? ElementId.InvalidElementId;
        }

        private static GraphicsIdMemoryValue GetSelectedIdMemory(ComboBox comboBox)
        {
            GraphicsIdOption selected = comboBox.SelectedItem as GraphicsIdOption;
            if (selected == null)
            {
                return GraphicsIdMemoryValue.ByView();
            }

            return new GraphicsIdMemoryValue
            {
                IntegerValue = (selected.Id ?? ElementId.InvalidElementId).IntegerValue,
                DisplayName = selected.DisplayName
            };
        }

        private static int GetSelectedLineWeight(ComboBox comboBox)
        {
            GraphicsLineWeightOption selected = comboBox.SelectedItem as GraphicsLineWeightOption;
            return selected?.Weight ?? OverrideGraphicSettings.InvalidPenNumber;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            CloseDialog(false);
        }

        private void CloseDialog(bool result)
        {
            try
            {
                DialogResult = result;
            }
            catch (InvalidOperationException)
            {
                Close();
            }
        }
    }
}
