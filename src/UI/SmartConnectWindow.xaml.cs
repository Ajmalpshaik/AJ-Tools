// Tool Name: Smart Connect Settings Window
// Description: Code-behind for Smart Connect routing and angle settings UI.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-25
// Revit Version: 2020
// Dependencies: AJTools.Models, AJTools.Services.SmartConnect

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AJTools.Models;
using AJTools.Services.SmartConnect;

namespace AJTools.UI
{
    /// <summary>
    /// Interaction logic for SmartConnectWindow.xaml
    /// </summary>
    public partial class SmartConnectWindow : Window
    {
        private readonly ObservableCollection<AngleItem> _customAngles = new ObservableCollection<AngleItem>();
        private bool _isInternalSelectionChange;

        public SmartConnectRoutingMode SelectedRoutingMode { get; private set; } = SmartConnectRoutingMode.SingleElbow;

        public double SelectedAngleDegrees { get; private set; } = 90.0;

        public IList<double> CustomAngles => _customAngles.Select(item => item.Value).ToList();

        public SmartConnectWindow(SmartConnectSettings initialSettings)
        {
            InitializeComponent();

            CustomAnglesList.ItemsSource = _customAngles;
            CustomAnglesList.DisplayMemberPath = nameof(AngleItem.DisplayName);

            ApplyInitialSettings(initialSettings ?? new SmartConnectSettings());
        }

        private void OnAddCustomAngleClick(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            if (!SmartConnectSettingsService.TryParseAngle(CustomAngleBox.Text, out double angle))
            {
                ErrorText.Text = "Enter a valid practical angle (5 to 175).";
                return;
            }

            CustomAngleBox.Clear();

            if (SmartConnectSettingsService.IsPredefinedAngle(angle))
            {
                SelectPresetAngle(angle);
                return;
            }

            if (!TrySelectExistingCustomAngle(angle))
            {
                InsertCustomAngle(angle);
                TrySelectExistingCustomAngle(angle);
            }

            SetPresetSelection(false, false);
        }

        private void OnPresetAngleChecked(object sender, RoutedEventArgs e)
        {
            if (_isInternalSelectionChange)
            {
                return;
            }

            ErrorText.Text = string.Empty;
            _isInternalSelectionChange = true;
            CustomAnglesList.SelectedItem = null;
            _isInternalSelectionChange = false;
        }

        private void OnCustomAnglesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInternalSelectionChange)
            {
                return;
            }

            if (CustomAnglesList.SelectedItem == null)
            {
                return;
            }

            ErrorText.Text = string.Empty;
            SetPresetSelection(false, false);
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            SelectedRoutingMode = SmartConnectRoutingMode.SingleElbow;

            if (Angle45Radio.IsChecked == true)
            {
                SelectedAngleDegrees = 45.0;
            }
            else if (Angle90Radio.IsChecked == true)
            {
                SelectedAngleDegrees = 90.0;
            }
            else if (CustomAnglesList.SelectedItem is AngleItem selectedCustom)
            {
                SelectedAngleDegrees = selectedCustom.Value;
            }
            else
            {
                ErrorText.Text = "Select one angle (45, 90, or one custom angle).";
                return;
            }

            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyInitialSettings(SmartConnectSettings settings)
        {
            foreach (double angle in settings.CustomAngles.OrderBy(value => value))
            {
                if (SmartConnectSettingsService.TryNormalizeAngle(angle, out double normalized) &&
                    !SmartConnectSettingsService.IsPredefinedAngle(normalized) &&
                    !ContainsCustomAngle(normalized))
                {
                    _customAngles.Add(new AngleItem(normalized));
                }
            }

            if (SmartConnectSettingsService.AreAnglesEqual(settings.SelectedAngleDegrees, 45.0))
            {
                SelectPresetAngle(45.0);
                return;
            }

            if (SmartConnectSettingsService.AreAnglesEqual(settings.SelectedAngleDegrees, 90.0))
            {
                SelectPresetAngle(90.0);
                return;
            }

            if (SmartConnectSettingsService.TryNormalizeAngle(settings.SelectedAngleDegrees, out double customAngle))
            {
                if (!SmartConnectSettingsService.IsPredefinedAngle(customAngle) && !ContainsCustomAngle(customAngle))
                {
                    InsertCustomAngle(customAngle);
                }

                if (TrySelectExistingCustomAngle(customAngle))
                {
                    SetPresetSelection(false, false);
                    return;
                }
            }

            SelectPresetAngle(90.0);
        }

        private bool ContainsCustomAngle(double value)
        {
            return _customAngles.Any(item => SmartConnectSettingsService.AreAnglesEqual(item.Value, value));
        }

        private bool TrySelectExistingCustomAngle(double value)
        {
            AngleItem match = _customAngles.FirstOrDefault(item => SmartConnectSettingsService.AreAnglesEqual(item.Value, value));
            if (match == null)
            {
                return false;
            }

            _isInternalSelectionChange = true;
            CustomAnglesList.SelectedItem = match;
            CustomAnglesList.ScrollIntoView(match);
            _isInternalSelectionChange = false;
            return true;
        }

        private void InsertCustomAngle(double value)
        {
            int insertIndex = 0;
            while (insertIndex < _customAngles.Count && _customAngles[insertIndex].Value < value)
            {
                insertIndex++;
            }

            _customAngles.Insert(insertIndex, new AngleItem(value));
        }

        private void SelectPresetAngle(double value)
        {
            _isInternalSelectionChange = true;
            CustomAnglesList.SelectedItem = null;
            Angle45Radio.IsChecked = SmartConnectSettingsService.AreAnglesEqual(value, 45.0);
            Angle90Radio.IsChecked = SmartConnectSettingsService.AreAnglesEqual(value, 90.0);
            _isInternalSelectionChange = false;
        }

        private void SetPresetSelection(bool angle45, bool angle90)
        {
            _isInternalSelectionChange = true;
            Angle45Radio.IsChecked = angle45;
            Angle90Radio.IsChecked = angle90;
            _isInternalSelectionChange = false;
        }

        private sealed class AngleItem
        {
            public AngleItem(double value)
            {
                Value = value;
                DisplayName = value.ToString("0.##", CultureInfo.CurrentCulture) + "\u00B0";
            }

            public double Value { get; }

            public string DisplayName { get; }
        }
    }
}
