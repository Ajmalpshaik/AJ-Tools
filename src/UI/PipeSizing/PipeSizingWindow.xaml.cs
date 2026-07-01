#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeSizingWindow.xaml.cs
 * Purpose       : Provides the Pipe Sizing WPF window, fixture rows, state persistence, calculation refresh, and CSV export.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in WPF UI
 *
 * Dependencies  : AJTools.Models.PipeSizing, AJTools.Services.PipeSizing
 *
 * Input         : User-entered fixture rows, system type, material, and velocity limit.
 * Output        : Fixture-unit total, GPM, minimum ID, selected pipe size, velocity, friction loss, and CSV report.
 *
 * Notes         :
 * - Ported from the original pyRevit pipe sizing UI.
 * - This window does not read from or write to the Revit model.
 * - ESC/cancel handling is not required because this tool does not use PickObject/PickPoint.
 *
 * Changelog     :
 * v1.0.0 (2026-07-01) - Initial C# port for Pipe Sizing.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AJTools.Models.PipeSizing;
using AJTools.Services.PipeSizing;

namespace AJTools.UI.PipeSizing
{
    public partial class PipeSizingWindow : Window
    {
        private const double DefaultVelocityMs = 2.4;
        private const double FeetPerMeter = 3.28084;

        private readonly IReadOnlyList<string> _fixtureNames;
        private bool _isUpdatingVelocity;
        private bool _isLoading;

        public PipeSizingWindow()
        {
            _isLoading = true;
            _fixtureNames = PipeSizingData.GetSortedFixtureNames();

            InitializeComponent();
            LoadInitialState();
            CalculateSizing();
        }

        private void LoadInitialState()
        {
            _isLoading = true;

            try
            {
                PipeSizingState state = PipeSizingStateService.Load();
                if (state != null)
                {
                    SetComboIndexSafe(ComboSystemType, state.SystemType, 0);
                    SetComboIndexSafe(ComboPipeMaterial, state.PipeMaterial, 0);
                    TxtMaxVelocityMS.Text = string.IsNullOrWhiteSpace(state.VelocityMS) ? "2.4" : state.VelocityMS;
                    TxtMaxVelocityFTS.Text = string.IsNullOrWhiteSpace(state.VelocityFTS) ? "7.87" : state.VelocityFTS;

                    if (state.Rows != null && state.Rows.Count > 0)
                    {
                        foreach (PipeSizingStateRow row in state.Rows)
                        {
                            AddFixtureRow(row.Fixture, row.IsCold, row.IsHot, string.IsNullOrWhiteSpace(row.Qty) ? "1" : row.Qty);
                        }
                    }
                    else
                    {
                        AddFixtureRow(null, false, false, "1");
                    }
                }
                else
                {
                    SetComboIndexSafe(ComboSystemType, 0, 0);
                    SetComboIndexSafe(ComboPipeMaterial, 0, 0);
                    TxtMaxVelocityMS.Text = "2.4";
                    TxtMaxVelocityFTS.Text = "7.87";
                    AddFixtureRow(null, false, false, "1");
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveState();
        }

        private void SaveState()
        {
            var state = new PipeSizingState
            {
                SystemType = ComboSystemType.SelectedIndex,
                PipeMaterial = ComboPipeMaterial.SelectedIndex,
                VelocityMS = TxtMaxVelocityMS.Text,
                VelocityFTS = TxtMaxVelocityFTS.Text,
                Rows = new List<PipeSizingStateRow>()
            };

            foreach (FixtureRowControls row in GetRows())
            {
                state.Rows.Add(new PipeSizingStateRow
                {
                    Fixture = GetSelectedFixtureName(row),
                    IsCold = row.ColdButton.IsChecked == true,
                    IsHot = row.HotButton.IsChecked == true,
                    Qty = row.QuantityTextBox.Text
                });
            }

            PipeSizingStateService.Save(state);
        }

        private void BtnAddFixture_Click(object sender, RoutedEventArgs e)
        {
            AddFixtureRow(null, false, false, "1");
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            FixtureListPanel.Children.Clear();
            CalculateSizing();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var rows = new List<string[]>();

            foreach (FixtureRowControls row in GetRows())
            {
                bool cold = row.ColdButton.IsChecked == true;
                bool hot = row.HotButton.IsChecked == true;
                string supplyName = cold && hot ? "Total" : cold ? "Cold" : hot ? "Hot" : "None";

                rows.Add(new[]
                {
                    GetSelectedFixtureName(row),
                    supplyName,
                    row.QuantityTextBox.Text,
                    row.FuPerFixtureText.Text,
                    row.TotalText.Text
                });
            }

            PipeSizingCsvExporter.Export(
                rows,
                TxtTotalFU.Text,
                TxtFlowRate.Text,
                TxtCalcID.Text,
                TxtPipeSize.Text,
                TxtVelocity.Text,
                TxtFrictionLoss.Text);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TxtMaxVelocityMS_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingVelocity || TxtMaxVelocityMS == null || TxtMaxVelocityFTS == null)
            {
                return;
            }

            double ms;
            if (!TryParseDouble(TxtMaxVelocityMS.Text, out ms))
            {
                return;
            }

            _isUpdatingVelocity = true;
            try
            {
                TxtMaxVelocityFTS.Text = FormatPythonNumber(ms * FeetPerMeter, 2);
            }
            finally
            {
                _isUpdatingVelocity = false;
            }

            CalculateSizing();
        }

        private void TxtMaxVelocityFTS_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingVelocity || TxtMaxVelocityMS == null || TxtMaxVelocityFTS == null)
            {
                return;
            }

            double feetPerSecond;
            if (!TryParseDouble(TxtMaxVelocityFTS.Text, out feetPerSecond))
            {
                return;
            }

            _isUpdatingVelocity = true;
            try
            {
                TxtMaxVelocityMS.Text = FormatPythonNumber(feetPerSecond / FeetPerMeter, 2);
            }
            finally
            {
                _isUpdatingVelocity = false;
            }

            CalculateSizing();
        }

        private void CalculateSizing_Event(object sender, RoutedEventArgs e)
        {
            CalculateSizing();
        }

        private void AddFixtureRow(string fixtureName, bool isCold, bool isHot, string quantity)
        {
            if (string.IsNullOrWhiteSpace(fixtureName) && FixtureListPanel.Children.Count > 0)
            {
                Grid lastGrid = FixtureListPanel.Children[FixtureListPanel.Children.Count - 1] as Grid;
                FixtureRowControls lastRow = lastGrid?.Tag as FixtureRowControls;
                if (lastRow != null)
                {
                    fixtureName = GetSelectedFixtureName(lastRow);
                }
            }

            var grid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

            var comboFixture = new ComboBox
            {
                Margin = new Thickness(0, 0, 8, 0),
                MinHeight = 30
            };

            foreach (string name in _fixtureNames)
            {
                comboFixture.Items.Add(name);
            }

            comboFixture.SelectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(fixtureName))
            {
                int index = _fixtureNames.ToList().FindIndex(name => string.Equals(name, fixtureName, StringComparison.Ordinal));
                if (index >= 0)
                {
                    comboFixture.SelectedIndex = index;
                }
            }

            comboFixture.SelectionChanged += FixtureCombo_SelectionChanged;
            Grid.SetColumn(comboFixture, 0);
            grid.Children.Add(comboFixture);

            var supplyPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var coldButton = CreateSupplyToggle("Cold", isCold, new Thickness(0, 0, 6, 0));
            coldButton.Checked += SupplyToggle_Changed;
            coldButton.Unchecked += SupplyToggle_Changed;

            var hotButton = CreateSupplyToggle("Hot", isHot, new Thickness(0));
            hotButton.Checked += SupplyToggle_Changed;
            hotButton.Unchecked += SupplyToggle_Changed;

            supplyPanel.Children.Add(coldButton);
            supplyPanel.Children.Add(hotButton);
            Grid.SetColumn(supplyPanel, 1);
            grid.Children.Add(supplyPanel);

            var txtQuantity = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(quantity) ? "1" : quantity,
                Margin = new Thickness(4, 0, 4, 0),
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = TryFindResource("ModernTextBox") as Style,
                MinHeight = 30
            };
            txtQuantity.TextChanged += CalculateSizing_Event;
            Grid.SetColumn(txtQuantity, 2);
            grid.Children.Add(txtQuantity);

            var txtFuFix = CreateRowTextBlock("0.0", false);
            Grid.SetColumn(txtFuFix, 3);
            grid.Children.Add(txtFuFix);

            var txtTotalRow = CreateRowTextBlock("0.0", true);
            Grid.SetColumn(txtTotalRow, 4);
            grid.Children.Add(txtTotalRow);

            var btnRemove = new Button
            {
                Content = "X",
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(232, 17, 35)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnRemove.Click += RemoveFixtureRow_Click;
            Grid.SetColumn(btnRemove, 5);
            grid.Children.Add(btnRemove);

            var rowControls = new FixtureRowControls
            {
                RowGrid = grid,
                FixtureCombo = comboFixture,
                ColdButton = coldButton,
                HotButton = hotButton,
                QuantityTextBox = txtQuantity,
                FuPerFixtureText = txtFuFix,
                TotalText = txtTotalRow
            };

            grid.Tag = rowControls;
            FixtureListPanel.Children.Add(grid);

            ApplyFixtureHotAvailability(rowControls);
            UpdateToggleColors(rowControls);

            if (!_isLoading)
            {
                CalculateSizing();
            }
        }

        private ToggleButton CreateSupplyToggle(string text, bool isChecked, Thickness margin)
        {
            return new ToggleButton
            {
                Content = text,
                IsChecked = isChecked,
                Margin = margin,
                Style = TryFindResource("SupplyToggleButton") as Style
            };
        }

        private TextBlock CreateRowTextBlock(string text, bool bold)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = TryFindResource("TextPrimary") as Brush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            };
        }

        private void RemoveFixtureRow_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null)
            {
                return;
            }

            Grid row = button.Parent as Grid;
            if (row == null)
            {
                return;
            }

            FixtureListPanel.Children.Remove(row);
            CalculateSizing();
        }

        private void FixtureCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            Grid grid = comboBox?.Parent as Grid;
            FixtureRowControls row = grid?.Tag as FixtureRowControls;
            if (row == null)
            {
                return;
            }

            ApplyFixtureHotAvailability(row);

            if (!_isLoading)
            {
                CalculateSizing();
            }
        }

        private void SupplyToggle_Changed(object sender, RoutedEventArgs e)
        {
            ToggleButton button = sender as ToggleButton;
            StackPanel panel = button?.Parent as StackPanel;
            Grid grid = panel?.Parent as Grid;
            FixtureRowControls row = grid?.Tag as FixtureRowControls;
            if (row == null)
            {
                return;
            }

            UpdateToggleColors(row);

            if (!_isLoading)
            {
                CalculateSizing();
            }
        }

        private void ApplyFixtureHotAvailability(FixtureRowControls row)
        {
            string fixtureName = GetSelectedFixtureName(row);
            PipeFixtureData fixtureData;
            if (!PipeSizingData.FixtureData.TryGetValue(fixtureName, out fixtureData))
            {
                return;
            }

            if (fixtureData.Hot <= 0)
            {
                row.HotButton.IsChecked = false;
                row.HotButton.IsEnabled = false;
                row.HotButton.Opacity = 0.4;
            }
            else
            {
                row.HotButton.IsEnabled = true;
                row.HotButton.Opacity = 1.0;
            }

            UpdateToggleColors(row);
        }

        private void UpdateToggleColors(FixtureRowControls row)
        {
            bool coldChecked = row.ColdButton.IsChecked == true;
            bool hotChecked = row.HotButton.IsChecked == true;

            ResetToggle(row.ColdButton);
            ResetToggle(row.HotButton);

            if (coldChecked && hotChecked)
            {
                ApplyToggleBrush(row.ColdButton, Color.FromRgb(15, 123, 15));
                ApplyToggleBrush(row.HotButton, Color.FromRgb(15, 123, 15));
                return;
            }

            if (coldChecked)
            {
                ApplyToggleBrush(row.ColdButton, Color.FromRgb(0, 120, 212));
            }

            if (hotChecked)
            {
                ApplyToggleBrush(row.HotButton, Color.FromRgb(232, 17, 35));
            }
        }

        private static void ResetToggle(ToggleButton button)
        {
            button.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            button.Foreground = new SolidColorBrush(Color.FromRgb(168, 180, 190));
            button.BorderBrush = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
            button.BorderThickness = new Thickness(1);
        }

        private static void ApplyToggleBrush(ToggleButton button, Color color)
        {
            button.Background = new SolidColorBrush(color);
            button.Foreground = Brushes.White;
            button.BorderThickness = new Thickness(0);
        }

        private void CalculateSizing()
        {
            if (_isLoading || !IsCalculationUiReady())
            {
                return;
            }

            try
            {
                double aggregateFixtureUnits = 0.0;

                foreach (FixtureRowControls row in GetRows())
                {
                    string fixtureName = GetSelectedFixtureName(row);
                    PipeFixtureData fixtureData;
                    if (!PipeSizingData.FixtureData.TryGetValue(fixtureName, out fixtureData))
                    {
                        continue;
                    }

                    double baseFixtureUnits;
                    bool cold = row.ColdButton.IsChecked == true;
                    bool hot = row.HotButton.IsChecked == true;

                    if (cold && hot)
                    {
                        baseFixtureUnits = fixtureData.Total;
                    }
                    else if (cold)
                    {
                        baseFixtureUnits = fixtureData.Cold;
                    }
                    else if (hot)
                    {
                        baseFixtureUnits = fixtureData.Hot;
                    }
                    else
                    {
                        baseFixtureUnits = 0.0;
                    }

                    int quantity = ParseQuantity(row.QuantityTextBox.Text);
                    double rowTotal = baseFixtureUnits * quantity;
                    aggregateFixtureUnits += rowTotal;

                    row.FuPerFixtureText.Text = FormatPythonNumber(baseFixtureUnits, 2);
                    row.TotalText.Text = FormatPythonNumber(PipeSizingCalculator.Round2(rowTotal), 2);
                }

                TxtTotalFU.Text = FormatPythonNumber(PipeSizingCalculator.Round2(aggregateFixtureUnits), 2);

                double velocityLimitMs;
                if (!TryParseDouble(TxtMaxVelocityMS.Text, out velocityLimitMs))
                {
                    velocityLimitMs = DefaultVelocityMs;
                }

                bool isFlushValve = ComboSystemType.SelectedIndex == 0;
                int materialIndex = ComboPipeMaterial.SelectedIndex >= 0 ? ComboPipeMaterial.SelectedIndex : 0;
                PipeSizingResult result = PipeSizingCalculator.Calculate(
                    aggregateFixtureUnits,
                    isFlushValve,
                    materialIndex,
                    velocityLimitMs);

                double velocityMs = result.VelocityFeetPerSecond / FeetPerMeter;

                TxtFlowRate.Text = FormatPythonNumber(result.FlowGpm, 2) + " GPM";
                TxtCalcID.Text = FormatPythonNumber(result.RequiredInternalDiameterMm, 2) + " mm";
                TxtPipeSize.Text = result.SelectedSizeLabel + " [ID: " + FormatPythonNumber(result.SelectedSizeMm, 2) + "mm]";
                TxtVelocity.Text = FormatPythonNumber(PipeSizingCalculator.Round2(velocityMs), 2) +
                                   " m/s (" +
                                   FormatPythonNumber(result.VelocityFeetPerSecond, 2) +
                                   " ft/s)";
                TxtFrictionLoss.Text = FormatPythonNumber(result.FrictionLossPsiPer100Ft, 2) + " psi/100ft";
                TxtStatus.Text = string.Empty;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                SetStatusText(ex.Message);
            }
            catch (Exception ex)
            {
                SetStatusText("Calculation error: " + ex.Message);
            }
        }

        private bool IsCalculationUiReady()
        {
            return FixtureListPanel != null &&
                   TxtTotalFU != null &&
                   TxtMaxVelocityMS != null &&
                   ComboSystemType != null &&
                   ComboPipeMaterial != null &&
                   TxtFlowRate != null &&
                   TxtCalcID != null &&
                   TxtPipeSize != null &&
                   TxtVelocity != null &&
                   TxtFrictionLoss != null &&
                   TxtStatus != null;
        }

        private void SetStatusText(string text)
        {
            if (TxtStatus != null)
            {
                TxtStatus.Text = text ?? string.Empty;
            }
        }

        private IEnumerable<FixtureRowControls> GetRows()
        {
            foreach (UIElement child in FixtureListPanel.Children)
            {
                Grid grid = child as Grid;
                FixtureRowControls row = grid?.Tag as FixtureRowControls;
                if (row != null)
                {
                    yield return row;
                }
            }
        }

        private static string GetSelectedFixtureName(FixtureRowControls row)
        {
            if (row == null || row.FixtureCombo.SelectedItem == null)
            {
                return string.Empty;
            }

            return row.FixtureCombo.SelectedItem.ToString();
        }

        private static int ParseQuantity(string text)
        {
            int quantity;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity))
            {
                return 0;
            }

            return quantity < 0 ? 0 : quantity;
        }

        private static bool TryParseDouble(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static string FormatPythonNumber(double value, int decimals)
        {
            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            string format = decimals <= 0 ? "0" : "0." + new string('#', decimals);
            string text = rounded.ToString(format, CultureInfo.InvariantCulture);

            if (!text.Contains("."))
            {
                text += ".0";
            }

            return text;
        }

        private static void SetComboIndexSafe(ComboBox comboBox, int requestedIndex, int fallbackIndex)
        {
            if (comboBox == null || comboBox.Items.Count == 0)
            {
                return;
            }

            comboBox.SelectedIndex = requestedIndex >= 0 && requestedIndex < comboBox.Items.Count
                ? requestedIndex
                : fallbackIndex;
        }

        private sealed class FixtureRowControls
        {
            public Grid RowGrid { get; set; }
            public ComboBox FixtureCombo { get; set; }
            public ToggleButton ColdButton { get; set; }
            public ToggleButton HotButton { get; set; }
            public TextBox QuantityTextBox { get; set; }
            public TextBlock FuPerFixtureText { get; set; }
            public TextBlock TotalText { get; set; }
        }
    }
}
