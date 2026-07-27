#region Metadata
/*
 * Tool Name     : Reassign Reference Level - Level Picker (Window)
 * File Name     : ReassignLevelWindow.xaml.cs
 * Purpose       : WPF level picker for Reassign Reference Level - choose the FROM and TO level.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF)
 *
 * Dependencies  : System.Windows (WPF), Autodesk.Revit.DB (Level - read-only), AJTools.UI.ModernStyles
 *
 * Input         : The project's levels, already sorted by elevation by the calling command.
 * Output        : FromLevel / ToLevel - read by the command after DialogResult == true.
 *
 * Notes         :
 * - Pure UI. Reads Level.Name/Elevation only; no collectors, no transaction, no model change.
 * - Modal window: shown with ShowDialog() from inside IExternalCommand.Execute, so the command is
 *   still in a valid API context when it returns.
 * - Validation is live and inline: picking the same level twice disables Run instead of closing the
 *   window with an error, which is what the old WinForms dialog did.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release. Replaces the WinForms level prompt in CmdReassignLevel.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace AJTools.UI.ReassignLevel
{
    /// <summary>
    /// Interaction logic for ReassignLevelWindow.xaml
    /// </summary>
    public partial class ReassignLevelWindow : Window
    {
        private const double MetersPerFoot = 0.3048;

        private bool _loaded;

        /// <summary>
        /// Level the elements currently reference. Only meaningful when DialogResult is true.
        /// </summary>
        public Level FromLevel { get; private set; }

        /// <summary>
        /// Level the elements will reference instead. Only meaningful when DialogResult is true.
        /// </summary>
        public Level ToLevel { get; private set; }

        /// <summary>
        /// Creates the level picker.
        /// </summary>
        /// <param name="levels">Project levels, already ordered by elevation.</param>
        public ReassignLevelWindow(IList<Level> levels)
        {
            InitializeComponent();

            List<LevelChoice> items = (levels ?? new List<Level>())
                .Where(level => level != null)
                .Select(level => new LevelChoice(level))
                .ToList();

            // Two separate lists: a shared ItemsSource would make both combos move together.
            FromCombo.ItemsSource = items;
            ToCombo.ItemsSource = items.ToList();
            FromCombo.DisplayMemberPath = nameof(LevelChoice.Label);
            ToCombo.DisplayMemberPath = nameof(LevelChoice.Label);

            if (items.Count > 0)
                FromCombo.SelectedIndex = 0;

            ToCombo.SelectedIndex = items.Count > 1 ? 1 : 0;

            _loaded = true;
            Validate(out _, out _);
        }

        private void OnLevelChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_loaded)
                return;

            Validate(out _, out _);
        }

        private void OnSwap(object sender, RoutedEventArgs e)
        {
            int from = FromCombo.SelectedIndex;
            FromCombo.SelectedIndex = ToCombo.SelectedIndex;
            ToCombo.SelectedIndex = from;
        }

        private void OnRun(object sender, RoutedEventArgs e)
        {
            if (!Validate(out Level from, out Level to))
                return;

            FromLevel = from;
            ToLevel = to;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Checks the current pair, refreshes the inline message and enables or disables Run.
        /// Never shows a popup and never closes the window.
        /// </summary>
        private bool Validate(out Level from, out Level to)
        {
            from = (FromCombo.SelectedItem as LevelChoice)?.Level;
            to = (ToCombo.SelectedItem as LevelChoice)?.Level;

            if (from == null || to == null)
                return Reject("Choose both a FROM level and a TO level.");

            if (from.Id == to.Id)
                return Reject("FROM and TO are the same level. Pick a different TO level, or press Swap.");

            ErrorText.Text = string.Empty;
            RunButton.IsEnabled = true;
            return true;
        }

        private bool Reject(string message)
        {
            ErrorText.Text = message;
            RunButton.IsEnabled = false;
            return false;
        }

        private sealed class LevelChoice
        {
            public LevelChoice(Level level)
            {
                Level = level;
                Label = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1:0.000} m)",
                    level?.Name ?? "<Unnamed>",
                    level == null ? 0 : level.Elevation * MetersPerFoot);
            }

            public Level Level { get; }

            public string Label { get; }

            public override string ToString() => Label;
        }
    }
}
