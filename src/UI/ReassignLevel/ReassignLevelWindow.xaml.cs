#region Metadata
/*
 * Tool Name     : Reassign Reference Level - Level Picker (Window)
 * File Name     : ReassignLevelWindow.xaml.cs
 * Purpose       : WPF scope/level picker for Reassign Reference Level - choose Whole Project (FROM/TO)
 *                 or Selected Elements (TO only, using whatever was already selected in Revit).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
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
 * Input         : The project's levels (sorted by elevation), plus how many elements are currently
 *                 selected in Revit and how many of those are eligible for reassignment - all supplied
 *                 by the calling command.
 * Output        : Scope, FromLevel (Whole Project only), ToLevel - read by the command after
 *                 DialogResult == true.
 *
 * Notes         :
 * - Pure UI. Reads Level.Name/Elevation only; no collectors, no transaction, no model change.
 * - Modal window: shown with ShowDialog() from inside IExternalCommand.Execute, so the command is
 *   still in a valid API context when it returns.
 * - Validation is live and inline: an invalid combination (same FROM/TO, or Selected Elements with
 *   nothing eligible) disables Run instead of closing the window with an error.
 * - The Selected Elements radio's Checked handler is wired up in code-behind AFTER
 *   InitializeComponent(), not via a XAML "Checked=" attribute - see Changelog v1.1.0.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release. Replaces the WinForms level prompt in CmdReassignLevel.
 * v1.1.0 (2026-07-28) - Added a Selected Elements scope alongside Whole Project: when elements are
 *                       already selected in Revit, the window can act on just those (only a TO level
 *                       is needed - FROM is read per-element by the service). FROM/Swap collapse and
 *                       an "N of M selected" summary shows instead. The Selected Elements radio is
 *                       disabled with an explanatory tooltip when nothing eligible is selected, so
 *                       there is no dead-end Run click.
 *                       WPF gotcha #1 found while building this: wiring "Checked" on a RadioButton via a
 *                       XAML attribute on the same element that also sets IsChecked="True" fires the
 *                       handler synchronously during InitializeComponent()'s BAML walk - before
 *                       later-declared x:Name siblings exist yet - so a handler that touches those
 *                       siblings (as OnScopeChanged does here) would NullReferenceException on every
 *                       single open. Fixed by attaching both radios' Checked handler in code-behind
 *                       after InitializeComponent() returns, then calling the update method once
 *                       manually for the initial state.
 *                       WPF gotcha #2: a bare "Visibility.Visible"/"Visibility.Collapsed" inside a
 *                       Window/UserControl class does not compile (CS0176) - UIElement already
 *                       declares an INSTANCE property called Visibility, which shadows the enum type
 *                       name in that scope, so the compiler resolves "Visibility" to "this.Visibility"
 *                       first. Must fully qualify as System.Windows.Visibility.Visible/.Collapsed
 *                       inside any class that itself has a Visibility property (any UIElement).
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
    /// Scope for the level reassignment - the whole project (FROM level -> TO level) or just the
    /// elements already selected in Revit (TO level only; each element's own current level is used
    /// as its FROM).
    /// </summary>
    public enum ReassignScope
    {
        WholeProject,
        SelectedElements
    }

    /// <summary>
    /// Interaction logic for ReassignLevelWindow.xaml
    /// </summary>
    public partial class ReassignLevelWindow : Window
    {
        private const double MetersPerFoot = 0.3048;

        private readonly int _eligibleSelectedCount;
        private bool _loaded;

        /// <summary>
        /// Whole Project or Selected Elements. Only meaningful when DialogResult is true.
        /// </summary>
        public ReassignScope Scope { get; private set; }

        /// <summary>
        /// Level the elements currently reference. Only set (and only meaningful) for the Whole
        /// Project scope; null for Selected Elements, where each element's own level is used instead.
        /// </summary>
        public Level FromLevel { get; private set; }

        /// <summary>
        /// Level the elements will reference instead. Only meaningful when DialogResult is true.
        /// </summary>
        public Level ToLevel { get; private set; }

        /// <summary>
        /// Creates the scope/level picker.
        /// </summary>
        /// <param name="levels">Project levels, already ordered by elevation.</param>
        /// <param name="totalSelectedCount">How many elements are currently selected in Revit.</param>
        /// <param name="eligibleSelectedCount">How many of those can actually be reassigned.</param>
        public ReassignLevelWindow(IList<Level> levels, int totalSelectedCount, int eligibleSelectedCount)
        {
            InitializeComponent();

            _eligibleSelectedCount = eligibleSelectedCount;

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

            if (eligibleSelectedCount > 0)
            {
                SelectionSummaryText.Text = totalSelectedCount == eligibleSelectedCount
                    ? string.Format(CultureInfo.CurrentCulture, "{0} selected element(s) will be reassigned.", eligibleSelectedCount)
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} of your {1} selected element(s) can be reassigned. The rest are hosted or unsupported.",
                        eligibleSelectedCount,
                        totalSelectedCount);
            }
            else
            {
                SelectedElementsRadio.IsEnabled = false;
                SelectedElementsRadio.ToolTip = totalSelectedCount == 0
                    ? "Select elements in the model first, then run this tool again."
                    : "None of the currently selected element(s) can be reassigned (only MEP curves, free-standing families, and spaces are supported).";
                SelectionSummaryText.Text = "No eligible elements selected.";
            }

            // Wired here, not via a XAML "Checked=" attribute - see the class-level Notes/Changelog:
            // touching FromPanel/NoteBodyText from a handler fired during InitializeComponent() itself
            // (which the XAML-attribute form can do) would NullReferenceException before those
            // x:Name fields exist.
            WholeProjectRadio.Checked += OnScopeChanged;
            SelectedElementsRadio.Checked += OnScopeChanged;

            if (eligibleSelectedCount > 0)
            {
                SelectedElementsRadio.IsChecked = true;
            }

            UpdateScopeVisibility();

            _loaded = true;
            Validate(out _, out _);
        }

        private void OnScopeChanged(object sender, RoutedEventArgs e)
        {
            UpdateScopeVisibility();

            if (_loaded)
                Validate(out _, out _);
        }

        private void UpdateScopeVisibility()
        {
            bool wholeProject = WholeProjectRadio.IsChecked == true;

            FromPanel.Visibility = wholeProject ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            SwapButton.Visibility = wholeProject ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            SelectionInfoPanel.Visibility = wholeProject ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

            NoteBodyText.Text = wholeProject
                ? "Applies to the whole project, not just the active view. Hosted family instances are skipped — their level follows the host. You will see the element count and can still cancel before anything changes."
                : "Applies only to your current selection. Hosted family instances and unsupported element types are skipped and reported. You can still cancel before anything changes.";
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

            bool wholeProject = WholeProjectRadio.IsChecked == true;
            Scope = wholeProject ? ReassignScope.WholeProject : ReassignScope.SelectedElements;
            FromLevel = wholeProject ? from : null;
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
        /// Checks the current scope/level combination, refreshes the inline message and enables or
        /// disables Run. Never shows a popup and never closes the window.
        /// </summary>
        private bool Validate(out Level from, out Level to)
        {
            from = (FromCombo.SelectedItem as LevelChoice)?.Level;
            to = (ToCombo.SelectedItem as LevelChoice)?.Level;

            bool wholeProject = WholeProjectRadio.IsChecked == true;

            if (wholeProject)
            {
                if (from == null || to == null)
                    return Reject("Choose both a FROM level and a TO level.");

                if (from.Id == to.Id)
                    return Reject("FROM and TO are the same level. Pick a different TO level, or press Swap.");
            }
            else
            {
                if (_eligibleSelectedCount == 0)
                    return Reject("Select elements in the model first, then run this tool again.");

                if (to == null)
                    return Reject("Choose a TO level.");
            }

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
