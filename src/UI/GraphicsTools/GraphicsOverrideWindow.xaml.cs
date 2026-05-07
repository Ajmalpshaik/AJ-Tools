// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Handles the Apply Graphics window behavior and input conversion.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.3
// Created      : 2026-03-30
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : User graphics settings selections, apply mode choice, selected source categories, and category selections.
// Output       : Selected Revit OverrideGraphicSettings and apply-mode data for command execution.
// Notes        : Keeps cut-link state explicit so linked cut settings match projection/surface settings exactly.
// Changelog    : v1.4.3 - Restored per-field preset colors and aligned category mode to the selected-element source.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        private const string ProjectionLineColorKey = "ProjectionLineColor";
        private const string SurfaceForegroundColorKey = "SurfaceForegroundColor";
        private const string SurfaceBackgroundColorKey = "SurfaceBackgroundColor";
        private const string CutLineColorKey = "CutLineColor";
        private const string CutForegroundColorKey = "CutForegroundColor";
        private const string CutBackgroundColorKey = "CutBackgroundColor";

        private static readonly Brush ByViewBrush = new SolidColorBrush(MediaColor.FromRgb(17, 26, 46));
        private static readonly Brush PreviewTextOnDarkBrush = Brushes.White;
        private static readonly Brush PreviewTextOnLightBrush = new SolidColorBrush(MediaColor.FromRgb(17, 17, 17));

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

            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                Title = windowTitle;
            }

            _categoryOptions = GraphicsDataProvider.GetCategoryOptions(availableCategories, preselectedCategoryIds);
            BindDropdownData(doc);
            BindCategoryData();
            InitializeApplyMode();
            ApplySettings(initialSettings ?? new OverrideGraphicSettings());
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

        private void BindDropdownData(Document doc)
        {
            IList<GraphicsIdOption> linePatternOptions = GraphicsDataProvider.GetLinePatternOptions(doc);
            IList<GraphicsIdOption> fillPatternOptions = GraphicsDataProvider.GetFillPatternOptions(doc);
            IList<GraphicsLineWeightOption> lineWeightOptions = GraphicsDataProvider.GetLineWeightOptions();
            IList<int> transparencyOptions = GraphicsDataProvider.GetTransparencyOptions();

            BindIdOptionCombo(ProjectionLinePatternCombo, linePatternOptions);
            BindIdOptionCombo(CutLinePatternCombo, linePatternOptions);
            BindIdOptionCombo(SurfaceForegroundPatternCombo, fillPatternOptions);
            BindIdOptionCombo(SurfaceBackgroundPatternCombo, fillPatternOptions);
            BindIdOptionCombo(CutForegroundPatternCombo, fillPatternOptions);
            BindIdOptionCombo(CutBackgroundPatternCombo, fillPatternOptions);

            BindLineWeightCombo(ProjectionLineWeightCombo, lineWeightOptions);
            BindLineWeightCombo(CutLineWeightCombo, lineWeightOptions);

            TransparencyCombo.ItemsSource = transparencyOptions;
            TransparencyCombo.SelectedItem = 0;
        }

        private void BindCategoryData()
        {
            CategoryListBox.ItemsSource = _categoryOptions;
            CategoryModeHintText.Text = _categoryOptions.Count == 0
                ? "No supported categories were found from the selected elements."
                : "Choose categories from the selected elements only, then apply active-view category overrides.";
        }

        private void InitializeApplyMode()
        {
            ApplyModeElementsRadioButton.IsChecked = true;
            SelectedApplyMode = GraphicsApplyMode.SelectedElements;
            ApplyApplyModeState();
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

        private void ApplySettings(OverrideGraphicSettings settings)
        {
            _colorValues[ProjectionLineColorKey] = GraphicsColorValue.FromRevitColor(settings.ProjectionLineColor);
            _colorValues[SurfaceForegroundColorKey] = GraphicsColorValue.FromRevitColor(settings.SurfaceForegroundPatternColor);
            _colorValues[SurfaceBackgroundColorKey] = GraphicsColorValue.FromRevitColor(settings.SurfaceBackgroundPatternColor);
            _colorValues[CutLineColorKey] = GraphicsColorValue.FromRevitColor(settings.CutLineColor);
            _colorValues[CutForegroundColorKey] = GraphicsColorValue.FromRevitColor(settings.CutForegroundPatternColor);
            _colorValues[CutBackgroundColorKey] = GraphicsColorValue.FromRevitColor(settings.CutBackgroundPatternColor);

            SelectIdOption(ProjectionLinePatternCombo, settings.ProjectionLinePatternId);
            SelectIdOption(SurfaceForegroundPatternCombo, settings.SurfaceForegroundPatternId);
            SelectIdOption(SurfaceBackgroundPatternCombo, settings.SurfaceBackgroundPatternId);
            SelectIdOption(CutLinePatternCombo, settings.CutLinePatternId);
            SelectIdOption(CutForegroundPatternCombo, settings.CutForegroundPatternId);
            SelectIdOption(CutBackgroundPatternCombo, settings.CutBackgroundPatternId);

            SelectLineWeightOption(ProjectionLineWeightCombo, settings.ProjectionLineWeight);
            SelectLineWeightOption(CutLineWeightCombo, settings.CutLineWeight);

            int transparency = settings.Transparency;
            TransparencyCombo.SelectedItem = transparency >= 0 && transparency <= 100 ? transparency : 0;

            HalftoneCheckBox.IsChecked = settings.Halftone;
            _manualCutState = CaptureCurrentCutState();
            UseProjectionSurfaceColorsForCutCheckBox.IsChecked = IsCutLinkedToProjectionSurface(settings);
            RefreshAllColorVisuals();
            ApplyCutColorLinkState(preserveManualState: false);
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
            if (!TryGetColorControls(key, out Border preview, out TextBlock textBlock))
            {
                return;
            }

            GraphicsColorValue value = GetColorValue(key);

            if (value.IsByView)
            {
                preview.Background = ByViewBrush;
                textBlock.Text = "By View";
                textBlock.Foreground = PreviewTextOnDarkBrush;
                return;
            }

            MediaColor mediaColor = MediaColor.FromRgb(value.Red, value.Green, value.Blue);
            preview.Background = new SolidColorBrush(mediaColor);
            textBlock.Text = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", value.Red, value.Green, value.Blue);
            textBlock.Foreground = ResolvePreviewTextBrush(mediaColor);
        }

        private static Brush ResolvePreviewTextBrush(MediaColor background)
        {
            double luminance = ((0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B)) / 255.0;
            return luminance >= 0.65 ? PreviewTextOnLightBrush : PreviewTextOnDarkBrush;
        }

        private bool TryGetColorControls(string key, out Border preview, out TextBlock textBlock)
        {
            preview = null;
            textBlock = null;

            switch (key)
            {
                case ProjectionLineColorKey:
                    preview = ProjectionLineColorPreview;
                    textBlock = ProjectionLineColorText;
                    break;
                case SurfaceForegroundColorKey:
                    preview = SurfaceForegroundColorPreview;
                    textBlock = SurfaceForegroundColorText;
                    break;
                case SurfaceBackgroundColorKey:
                    preview = SurfaceBackgroundColorPreview;
                    textBlock = SurfaceBackgroundColorText;
                    break;
                case CutLineColorKey:
                    preview = CutLineColorPreview;
                    textBlock = CutLineColorText;
                    break;
                case CutForegroundColorKey:
                    preview = CutForegroundColorPreview;
                    textBlock = CutForegroundColorText;
                    break;
                case CutBackgroundColorKey:
                    preview = CutBackgroundColorPreview;
                    textBlock = CutBackgroundColorText;
                    break;
            }

            return preview != null && textBlock != null;
        }

        private GraphicsColorValue GetColorValue(string key)
        {
            return _colorValues.ContainsKey(key)
                ? _colorValues[key]
                : GraphicsColorValue.ByView();
        }

        private void OnApplyModeChanged(object sender, RoutedEventArgs e)
        {
            SelectedApplyMode = ApplyModeCategoriesRadioButton.IsChecked == true
                ? GraphicsApplyMode.Categories
                : GraphicsApplyMode.SelectedElements;
            ApplyApplyModeState();
        }

        private void ApplyApplyModeState()
        {
            bool isCategoryMode = SelectedApplyMode == GraphicsApplyMode.Categories;
            CategorySelectionPanel.Visibility = isCategoryMode ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            ElementModeHintText.Visibility = isCategoryMode ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        private void OnSelectAllCategories(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _categoryOptions.Count; i++)
            {
                _categoryOptions[i].IsSelected = true;
            }

            CategoryListBox.Items.Refresh();
        }

        private void OnClearCategories(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _categoryOptions.Count; i++)
            {
                _categoryOptions[i].IsSelected = false;
            }

            CategoryListBox.Items.Refresh();
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

            CutSettingsPanel.IsEnabled = !useProjectionSurfaceSettings;
            CutLinkNoticeText.Visibility = useProjectionSurfaceSettings ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
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
            ApplySettings(new OverrideGraphicSettings());
            ApplyModeElementsRadioButton.IsChecked = true;
            SelectedApplyMode = GraphicsApplyMode.SelectedElements;
            ApplyApplyModeState();

            for (int i = 0; i < _categoryOptions.Count; i++)
            {
                _categoryOptions[i].IsSelected = false;
            }

            CategoryListBox.Items.Refresh();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            if (!TryBuildInput(out GraphicsOverrideInput input, out string errorMessage))
            {
                ErrorText.Text = errorMessage;
                return;
            }

            SelectedApplyMode = input.ApplyMode;
            SelectedOverrideSettings = GraphicsOverrideBuilder.Build(input);
            DialogResult = true;
            Close();
        }

        private bool TryBuildInput(out GraphicsOverrideInput input, out string errorMessage)
        {
            input = new GraphicsOverrideInput();
            errorMessage = string.Empty;

            if (ApplyModeCategoriesRadioButton.IsChecked == true && SelectedCategoryIds.Count == 0)
            {
                errorMessage = "Select at least one category for Category mode.";
                return false;
            }

            int transparency = ResolveTransparency();
            if (transparency < 0 || transparency > 100)
            {
                errorMessage = "Transparency must be a number between 0 and 100.";
                return false;
            }

            input.ApplyMode = ApplyModeCategoriesRadioButton.IsChecked == true
                ? GraphicsApplyMode.Categories
                : GraphicsApplyMode.SelectedElements;
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

        private int ResolveTransparency()
        {
            if (TransparencyCombo.SelectedItem is int intValue)
            {
                return intValue;
            }

            if (int.TryParse(TransparencyCombo.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsedValue))
            {
                return parsedValue;
            }

            return -1;
        }

        private static ElementId GetSelectedId(ComboBox comboBox)
        {
            GraphicsIdOption selected = comboBox.SelectedItem as GraphicsIdOption;
            return selected?.Id ?? ElementId.InvalidElementId;
        }

        private static int GetSelectedLineWeight(ComboBox comboBox)
        {
            GraphicsLineWeightOption selected = comboBox.SelectedItem as GraphicsLineWeightOption;
            return selected?.Weight ?? OverrideGraphicSettings.InvalidPenNumber;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
