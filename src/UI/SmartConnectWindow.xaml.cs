// Tool Name: Connect MEP Elements (Smart Connect) - Settings Window
// Description: Code-behind for the Connect MEP Elements settings UI.
// Author: Ajmal P.S.
// Version: 2.1.0
// Last Updated: 2026-08-15
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
using AJTools.Utils;

namespace AJTools.UI
{
    /// <summary>
    /// Interaction logic for SmartConnectWindow.xaml
    /// </summary>
    public partial class SmartConnectWindow : Window
    {
        private readonly ObservableCollection<AngleItem> _customAngles = new ObservableCollection<AngleItem>();
        private bool _isInternalSelectionChange;

        /// <summary>The settings as confirmed by the user. Only meaningful when <see cref="Confirmed"/> is true.</summary>
        public SmartConnectSettings Settings { get; private set; }

        /// <summary>True when the user saved rather than cancelled.</summary>
        public bool Confirmed { get; private set; }

        public SmartConnectWindow(SmartConnectSettings initialSettings)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

            CustomAnglesList.ItemsSource = _customAngles;
            CustomAnglesList.DisplayMemberPath = "DisplayName";

            Settings = initialSettings ?? new SmartConnectSettings();
            ApplySettingsToControls(Settings);
        }

        // ------------------------------------------------------------------
        // Angle editing
        // ------------------------------------------------------------------

        private void OnAddCustomAngleClick(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            double angle;
            if (!SmartConnectSettingsService.TryParseAngle(CustomAngleBox.Text, out angle))
            {
                ErrorText.Text = "Enter an angle between 5 and 90.";
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
            if (_isInternalSelectionChange || CustomAnglesList.SelectedItem == null)
            {
                return;
            }

            ErrorText.Text = string.Empty;
            SetPresetSelection(false, false);
        }

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            SmartConnectSettings collected;
            string error;
            if (!TryCollectSettings(out collected, out error))
            {
                ErrorText.Text = error;
                return;
            }

            Settings = collected;
            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;
            _customAngles.Clear();
            ApplySettingsToControls(new SmartConnectSettings());
        }

        /// <summary>
        /// Shows or hides the advanced block. Everything in it stays live either way - collapsing it
        /// only keeps it out of the way, it never resets or ignores a setting.
        /// </summary>
        private void OnToggleAdvancedClick(object sender, RoutedEventArgs e)
        {
            bool isHidden = AdvancedPanel.Visibility != Visibility.Visible;
            AdvancedPanel.Visibility = isHidden ? Visibility.Visible : Visibility.Collapsed;
            AdvancedToggleButton.Content = isHidden ? "Hide advanced settings" : "Show advanced settings";
        }

        // ------------------------------------------------------------------
        // Control state
        // ------------------------------------------------------------------

        private void ApplySettingsToControls(SmartConnectSettings settings)
        {
            SmartConnectSettings source = settings ?? new SmartConnectSettings();

            ModeAutoRadio.IsChecked = source.RoutingMode == SmartConnectRoutingMode.Auto;
            ModeSingleElbowRadio.IsChecked = source.RoutingMode == SmartConnectRoutingMode.SingleElbow;
            ModeOffsetRadio.IsChecked = source.RoutingMode == SmartConnectRoutingMode.OffsetWithTwoElbows;

            MoveBothRadio.IsChecked = source.MoveMode == SmartConnectMoveMode.Both;
            MoveFirstRadio.IsChecked = source.MoveMode == SmartConnectMoveMode.FirstOnly;
            MoveSecondRadio.IsChecked = source.MoveMode == SmartConnectMoveMode.SecondOnly;
            MoveNoneRadio.IsChecked = source.MoveMode == SmartConnectMoveMode.None;

            AngleFallbackCheck.IsChecked = source.UseAngleFallback;
            AllowConduitCheck.IsChecked = source.AllowConduit;
            AllowFlexCheck.IsChecked = source.AllowFlexCurves;
            AllowFamilyInstanceCheck.IsChecked = source.AllowFamilyInstanceConnectors;
            AllowNonParallelCheck.IsChecked = source.AllowNonParallelEnds;

            BatchFromSelectionCheck.IsChecked = source.BatchFromSelection;
            SingleUndoCheck.IsChecked = source.SingleUndoForBatch;
            SummaryReportCheck.IsChecked = source.ShowSummaryReport;
            MaxPairDistanceBox.Text = source.MaxPairDistanceMm.ToString("0.##", CultureInfo.CurrentCulture);

            CopyInsulationCheck.IsChecked = source.CopyInsulationAndLining;
            CopyParametersCheck.IsChecked = source.CopyInstanceParameters;
            AutoTransitionCheck.IsChecked = source.AutoTransitionOnSizeMismatch;
            WarnOnClashCheck.IsChecked = source.WarnOnClash;

            ApplyAngleSelection(source);
        }

        private void ApplyAngleSelection(SmartConnectSettings settings)
        {
            _customAngles.Clear();

            IEnumerable<double> saved = settings.CustomAngles ?? new List<double>();
            foreach (double angle in saved.OrderBy(value => value))
            {
                double normalized;
                if (SmartConnectSettingsService.TryNormalizeAngle(angle, out normalized) &&
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

            double customAngle;
            if (SmartConnectSettingsService.TryNormalizeAngle(settings.SelectedAngleDegrees, out customAngle))
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

        private bool TryCollectSettings(out SmartConnectSettings settings, out string errorMessage)
        {
            settings = null;
            errorMessage = string.Empty;

            double selectedAngle;
            if (Angle45Radio.IsChecked == true)
            {
                selectedAngle = 45.0;
            }
            else if (Angle90Radio.IsChecked == true)
            {
                selectedAngle = 90.0;
            }
            else
            {
                AngleItem selectedCustom = CustomAnglesList.SelectedItem as AngleItem;
                if (selectedCustom == null)
                {
                    errorMessage = "Choose one angle: 45, 90, or one from the custom list.";
                    return false;
                }

                selectedAngle = selectedCustom.Value;
            }

            double maxPairDistance;
            if (!SmartConnectSettingsService.TryParseDistanceMm(MaxPairDistanceBox.Text, out maxPairDistance))
            {
                errorMessage = "Enter a pairing distance between 100 and 50000 mm.";
                return false;
            }

            settings = new SmartConnectSettings
            {
                RoutingMode = ReadRoutingMode(),
                MoveMode = ReadMoveMode(),
                SelectedAngleDegrees = selectedAngle,
                CustomAngles = _customAngles.Select(item => item.Value).ToList(),
                UseAngleFallback = AngleFallbackCheck.IsChecked == true,

                // The window does not edit the fallback list, so carry the saved one through -
                // building a fresh object here would silently reset it on every save.
                FallbackAngles = Settings != null && Settings.FallbackAngles != null
                    ? Settings.FallbackAngles.ToList()
                    : new SmartConnectSettings().FallbackAngles,
                AllowConduit = AllowConduitCheck.IsChecked == true,
                AllowFlexCurves = AllowFlexCheck.IsChecked == true,
                AllowFamilyInstanceConnectors = AllowFamilyInstanceCheck.IsChecked == true,
                AllowNonParallelEnds = AllowNonParallelCheck.IsChecked == true,
                BatchFromSelection = BatchFromSelectionCheck.IsChecked == true,
                SingleUndoForBatch = SingleUndoCheck.IsChecked == true,
                ShowSummaryReport = SummaryReportCheck.IsChecked == true,
                MaxPairDistanceMm = maxPairDistance,
                CopyInsulationAndLining = CopyInsulationCheck.IsChecked == true,
                CopyInstanceParameters = CopyParametersCheck.IsChecked == true,
                AutoTransitionOnSizeMismatch = AutoTransitionCheck.IsChecked == true,
                WarnOnClash = WarnOnClashCheck.IsChecked == true
            };

            return true;
        }

        private SmartConnectRoutingMode ReadRoutingMode()
        {
            if (ModeSingleElbowRadio.IsChecked == true)
            {
                return SmartConnectRoutingMode.SingleElbow;
            }

            if (ModeOffsetRadio.IsChecked == true)
            {
                return SmartConnectRoutingMode.OffsetWithTwoElbows;
            }

            return SmartConnectRoutingMode.Auto;
        }

        private SmartConnectMoveMode ReadMoveMode()
        {
            if (MoveFirstRadio.IsChecked == true)
            {
                return SmartConnectMoveMode.FirstOnly;
            }

            if (MoveSecondRadio.IsChecked == true)
            {
                return SmartConnectMoveMode.SecondOnly;
            }

            if (MoveNoneRadio.IsChecked == true)
            {
                return SmartConnectMoveMode.None;
            }

            return SmartConnectMoveMode.Both;
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
                DisplayName = value.ToString("0.##", CultureInfo.CurrentCulture) + "°";
            }

            public double Value { get; private set; }

            public string DisplayName { get; private set; }
        }
    }
}
