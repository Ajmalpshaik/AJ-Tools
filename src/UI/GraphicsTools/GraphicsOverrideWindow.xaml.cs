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
    public enum GraphicsOverrideWindowMode
    {
        Default = 0,
        CategoryApply = 1
    }

    /// <summary>
    /// Graphics settings dialog used by category and element override commands.
    /// </summary>
    public partial class GraphicsOverrideWindow : Window
    {
        private const string ProjectionLineColorKey = "ProjectionLineColor";
        private const string SurfaceForegroundColorKey = "SurfaceForegroundColor";
        private const string SurfaceBackgroundColorKey = "SurfaceBackgroundColor";
        private const string CutLineColorKey = "CutLineColor";
        private const string CutForegroundColorKey = "CutForegroundColor";
        private const string CutBackgroundColorKey = "CutBackgroundColor";

        private static readonly Brush ByViewBrush = new SolidColorBrush(MediaColor.FromRgb(63, 63, 70));

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

        private readonly GraphicsOverrideWindowMode _windowMode;

        public GraphicsOverrideWindow(Document doc, string windowTitle, OverrideGraphicSettings initialSettings = null)
            : this(doc, windowTitle, GraphicsOverrideWindowMode.Default, initialSettings)
        {
        }

        public GraphicsOverrideWindow(
            Document doc,
            string windowTitle,
            GraphicsOverrideWindowMode windowMode,
            OverrideGraphicSettings initialSettings = null)
        {
            InitializeComponent();
            _windowMode = windowMode;

            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                Title = windowTitle;
            }

            BindDropdownData(doc);
            ApplySettings(initialSettings ?? new OverrideGraphicSettings());
            ConfigureModeBehavior();
        }

        public OverrideGraphicSettings SelectedOverrideSettings { get; private set; }

        private bool IsCategoryApplyMode
        {
            get { return _windowMode == GraphicsOverrideWindowMode.CategoryApply; }
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

        private void ConfigureModeBehavior()
        {
            if (IsCategoryApplyMode)
            {
                QuickColorTargetLabel.Visibility = System.Windows.Visibility.Collapsed;
                QuickColorTargetCombo.Visibility = System.Windows.Visibility.Collapsed;
                QuickColorAllNoticeText.Visibility = System.Windows.Visibility.Visible;

                UseProjectionSurfaceColorsForCutCheckBox.Visibility = System.Windows.Visibility.Visible;
                UseProjectionSurfaceColorsForCutCheckBox.IsEnabled = false;
                UseProjectionSurfaceColorsForCutCheckBox.Content = "Use Projection/Surface Colors For Cut (Always ON)";
                UseProjectionSurfaceColorsForCutCheckBox.IsChecked = true;
            }
            else
            {
                QuickColorTargetLabel.Visibility = System.Windows.Visibility.Visible;
                QuickColorTargetCombo.Visibility = System.Windows.Visibility.Visible;
                QuickColorAllNoticeText.Visibility = System.Windows.Visibility.Collapsed;

                UseProjectionSurfaceColorsForCutCheckBox.Visibility = System.Windows.Visibility.Visible;
                UseProjectionSurfaceColorsForCutCheckBox.IsEnabled = true;
                UseProjectionSurfaceColorsForCutCheckBox.Content = "Use Projection/Surface Colors For Cut";
                UseProjectionSurfaceColorsForCutCheckBox.IsChecked = false;
            }

            ApplyCutColorLinkState();
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
            RefreshAllColorVisuals();
            ApplyCutColorLinkState();
        }

        private static void SelectIdOption(ComboBox comboBox, ElementId id)
        {
            var options = comboBox.ItemsSource as IEnumerable<GraphicsIdOption>;
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
            var options = comboBox.ItemsSource as IEnumerable<GraphicsLineWeightOption>;
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
                return;
            }

            var mediaColor = MediaColor.FromRgb(value.Red, value.Green, value.Blue);
            preview.Background = new SolidColorBrush(mediaColor);
            textBlock.Text = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", value.Red, value.Green, value.Blue);
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

        private void OnPickColor(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
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
                HandleColorDependencies(key);
            }
        }

        private void OnClearColor(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string key = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _colorValues[key] = GraphicsColorValue.ByView();
            UpdateColorVisual(key);
            HandleColorDependencies(key);
        }

        private void OnQuickColorPreset(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string presetTag = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(presetTag))
            {
                return;
            }

            bool isByView = string.Equals(presetTag, "ByView", StringComparison.OrdinalIgnoreCase);
            GraphicsColorValue presetColor;

            if (isByView)
            {
                presetColor = GraphicsColorValue.ByView();
            }
            else
            {
                if (!TryParseRgb(presetTag, out byte red, out byte green, out byte blue))
                {
                    return;
                }

                presetColor = GraphicsColorValue.FromRgb(red, green, blue);
            }

            if (IsCategoryApplyMode)
            {
                ApplyQuickColorToAllSettings(presetColor);
                return;
            }

            string targetKey = ResolveQuickColorTargetKey();
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                return;
            }

            string effectiveTargetKey = ResolveEffectiveTargetKey(targetKey);
            _colorValues[effectiveTargetKey] = presetColor;
            UpdateColorVisual(effectiveTargetKey);
            HandleColorDependencies(effectiveTargetKey);
        }

        private void ApplyQuickColorToAllSettings(GraphicsColorValue colorValue)
        {
            _colorValues[ProjectionLineColorKey] = colorValue;
            _colorValues[SurfaceForegroundColorKey] = colorValue;
            _colorValues[SurfaceBackgroundColorKey] = colorValue;

            UpdateColorVisual(ProjectionLineColorKey);
            UpdateColorVisual(SurfaceForegroundColorKey);
            UpdateColorVisual(SurfaceBackgroundColorKey);

            if (UseProjectionSurfaceColorsForCutCheckBox.IsChecked == true)
            {
                SyncCutColorsFromProjectionSurface();
            }
            else
            {
                _colorValues[CutLineColorKey] = colorValue;
                _colorValues[CutForegroundColorKey] = colorValue;
                _colorValues[CutBackgroundColorKey] = colorValue;

                UpdateColorVisual(CutLineColorKey);
                UpdateColorVisual(CutForegroundColorKey);
                UpdateColorVisual(CutBackgroundColorKey);
            }
        }

        private string ResolveQuickColorTargetKey()
        {
            var selectedItem = QuickColorTargetCombo.SelectedItem as ComboBoxItem;
            return selectedItem?.Tag as string;
        }

        private string ResolveEffectiveTargetKey(string requestedTargetKey)
        {
            if (UseProjectionSurfaceColorsForCutCheckBox.IsChecked != true)
            {
                return requestedTargetKey;
            }

            switch (requestedTargetKey)
            {
                case CutLineColorKey:
                    return ProjectionLineColorKey;
                case CutForegroundColorKey:
                    return SurfaceForegroundColorKey;
                case CutBackgroundColorKey:
                    return SurfaceBackgroundColorKey;
                default:
                    return requestedTargetKey;
            }
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

            if (!byte.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out red))
            {
                return false;
            }

            if (!byte.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out green))
            {
                return false;
            }

            if (!byte.TryParse(segments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out blue))
            {
                return false;
            }

            return true;
        }

        private void OnUseProjectionSurfaceColorsForCutChanged(object sender, RoutedEventArgs e)
        {
            if (IsCategoryApplyMode)
            {
                UseProjectionSurfaceColorsForCutCheckBox.IsChecked = true;
            }

            ApplyCutColorLinkState();
        }

        private void ApplyCutColorLinkState()
        {
            bool useProjectionSurfaceColors = UseProjectionSurfaceColorsForCutCheckBox.IsChecked == true;

            CutLineColorPickButton.IsEnabled = !useProjectionSurfaceColors;
            CutLineColorClearButton.IsEnabled = !useProjectionSurfaceColors;
            CutForegroundColorPickButton.IsEnabled = !useProjectionSurfaceColors;
            CutForegroundColorClearButton.IsEnabled = !useProjectionSurfaceColors;
            CutBackgroundColorPickButton.IsEnabled = !useProjectionSurfaceColors;
            CutBackgroundColorClearButton.IsEnabled = !useProjectionSurfaceColors;

            if (useProjectionSurfaceColors)
            {
                SyncCutColorsFromProjectionSurface();
            }
        }

        private void SyncCutColorsFromProjectionSurface()
        {
            _colorValues[CutLineColorKey] = GetColorValue(ProjectionLineColorKey);
            _colorValues[CutForegroundColorKey] = GetColorValue(SurfaceForegroundColorKey);
            _colorValues[CutBackgroundColorKey] = GetColorValue(SurfaceBackgroundColorKey);

            UpdateColorVisual(CutLineColorKey);
            UpdateColorVisual(CutForegroundColorKey);
            UpdateColorVisual(CutBackgroundColorKey);
        }

        private void HandleColorDependencies(string changedColorKey)
        {
            if (UseProjectionSurfaceColorsForCutCheckBox.IsChecked != true)
            {
                return;
            }

            if (changedColorKey == ProjectionLineColorKey ||
                changedColorKey == SurfaceForegroundColorKey ||
                changedColorKey == SurfaceBackgroundColorKey)
            {
                SyncCutColorsFromProjectionSurface();
            }
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;
            ApplySettings(new OverrideGraphicSettings());
            ConfigureModeBehavior();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            if (!TryBuildInput(out GraphicsOverrideInput input, out string errorMessage))
            {
                ErrorText.Text = errorMessage;
                return;
            }

            SelectedOverrideSettings = GraphicsOverrideBuilder.Build(input);
            DialogResult = true;
            Close();
        }

        private bool TryBuildInput(out GraphicsOverrideInput input, out string errorMessage)
        {
            input = new GraphicsOverrideInput();
            errorMessage = string.Empty;

            int transparency = ResolveTransparency();
            if (transparency < 0 || transparency > 100)
            {
                errorMessage = "Transparency must be a number between 0 and 100.";
                return false;
            }

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
            var selected = comboBox.SelectedItem as GraphicsIdOption;
            return selected?.Id ?? ElementId.InvalidElementId;
        }

        private static int GetSelectedLineWeight(ComboBox comboBox)
        {
            var selected = comboBox.SelectedItem as GraphicsLineWeightOption;
            return selected?.Weight ?? OverrideGraphicSettings.InvalidPenNumber;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
