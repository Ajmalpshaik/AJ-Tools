// Tool Name: Reassign Reference Level UI
// Description: Code-behind for the ModernStyles-based source/target level picker.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2026-07-03
// Revit Version: 2020

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace AJTools.UI
{
    /// <summary>
    /// Modern WPF level picker for the Reassign Reference Level command.
    /// </summary>
    public partial class ReassignLevelWindow : Window
    {
        private const double MetersPerFoot = 0.3048;
        private readonly IList<LevelChoice> _levelItems;

        public ReassignLevelWindow(IList<Level> levels)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            InitializeComponent();

            _levelItems = levels
                .Select(level => new LevelChoice(level))
                .ToList();

            FromLevelComboBox.ItemsSource = _levelItems;
            ToLevelComboBox.ItemsSource = _levelItems;

            if (_levelItems.Count > 0)
            {
                FromLevelComboBox.SelectedIndex = 0;
            }

            if (_levelItems.Count > 1)
            {
                ToLevelComboBox.SelectedIndex = 1;
            }
            else if (_levelItems.Count > 0)
            {
                ToLevelComboBox.SelectedIndex = 0;
            }

            UpdateUiState();
        }

        public Level SelectedFromLevel { get; private set; }

        public Level SelectedToLevel { get; private set; }

        private void OnLevelSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUiState();
        }

        private void OnReassignClick(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedChoices(out LevelChoice fromChoice, out LevelChoice toChoice))
            {
                UpdateUiState();
                return;
            }

            SelectedFromLevel = fromChoice.Level;
            SelectedToLevel = toChoice.Level;
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateUiState()
        {
            LevelChoice fromChoice = FromLevelComboBox.SelectedItem as LevelChoice;
            LevelChoice toChoice = ToLevelComboBox.SelectedItem as LevelChoice;

            if (fromChoice == null || toChoice == null)
            {
                ReassignButton.IsEnabled = false;
                StatusText.Text = "Select both levels.";
                ChangeSummaryText.Text = string.Empty;
                return;
            }

            if (fromChoice.Level.Id == toChoice.Level.Id)
            {
                ReassignButton.IsEnabled = false;
                StatusText.Text = "FROM and TO levels must be different.";
                ChangeSummaryText.Text = "Choose a different target level before continuing.";
                return;
            }

            double deltaMeters = FeetToMeters(toChoice.Level.Elevation - fromChoice.Level.Elevation);
            ReassignButton.IsEnabled = true;
            StatusText.Text = "Ready to continue.";
            ChangeSummaryText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Level change: {0} -> {1} | Elevation delta: {2:+0.000;-0.000;0.000} m",
                fromChoice.Name,
                toChoice.Name,
                deltaMeters);
        }

        private bool TryGetSelectedChoices(out LevelChoice fromChoice, out LevelChoice toChoice)
        {
            fromChoice = FromLevelComboBox.SelectedItem as LevelChoice;
            toChoice = ToLevelComboBox.SelectedItem as LevelChoice;

            return fromChoice != null &&
                   toChoice != null &&
                   fromChoice.Level.Id != toChoice.Level.Id;
        }

        private static double FeetToMeters(double feet)
        {
            return feet * MetersPerFoot;
        }

        private sealed class LevelChoice
        {
            public LevelChoice(Level level)
            {
                Level = level ?? throw new ArgumentNullException(nameof(level));
                Name = string.IsNullOrWhiteSpace(level.Name) ? "<Unnamed>" : level.Name;
                Label = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1:0.000} m)",
                    Name,
                    FeetToMeters(level.Elevation));
            }

            public Level Level { get; }

            public string Name { get; }

            public string Label { get; }

            public override string ToString()
            {
                return Label;
            }
        }
    }
}
