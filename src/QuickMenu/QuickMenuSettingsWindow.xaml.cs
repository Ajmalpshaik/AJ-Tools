#region Metadata
/*
 * Tool Name     : AJ Quick Menu (customise)
 * File Name     : QuickMenuSettingsWindow.xaml.cs
 * Purpose       : Lets Ajmal decide what the quick wheel holds - which AJ Tools button sits in each
 *                 slot, how many slots there are (4 to 12) and how big the wheel opens.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-08-18
 * Last Updated  : 2026-08-19
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF, modal dialog)
 *
 * Dependencies  : QuickMenuCatalog entries + QuickMenuConfig (passed in), UI/ModernStyles.xaml
 *
 * Input         : The live tool list and the current saved layout.
 * Output        : The saved layout file, written on Save. No model changes.
 *
 * Notes         :
 * - Pure UI: the window never reads the ribbon or touches Revit itself, the command hands it the
 *   tool list and hands the saved config back.
 * - Both lists are filled with real ListBoxItem objects rather than data binding, because
 *   QuickToolEntry is an internal type and WPF binding wants public ones - the entry itself rides
 *   along in the item's Tag.
 * - Validation is inline (the Set button simply does nothing useful without both a tool and a slot
 *   selected, and both buttons enable/disable with the selection) - never a popup after the fact,
 *   per the house rule.
 * - Visibility values would be fully qualified (System.Windows.Visibility.*) per the house rule -
 *   none are needed in this file.
 *
 * Changelog     :
 * v1.1.0 (2026-08-19) - Tool list now also holds Revit's own commands, with a Show filter to see
 *                       only AJ Tools buttons, only Revit commands, or everything.
 * v1.0.0 (2026-08-18) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AJTools.Services.QuickMenu;

namespace AJTools.UI.QuickMenu
{
    /// <summary>Customise window for the AJ Quick Menu wheel.</summary>
    public partial class QuickMenuSettingsWindow : Window
    {
        private static readonly int[] SlotCountChoices = { 4, 6, 8, 10, 12 };

        private static readonly int[] WheelSizeChoices = { 460, 560, 660, 760 };

        private static readonly string[] WheelSizeLabels =
        {
            "Small (460)",
            "Medium (560)",
            "Large (660)",
            "Extra large (760)"
        };

        /// <summary>What the Show combo offers, in order. Null means "no filter - show everything".</summary>
        private static readonly QuickToolSource?[] SourceFilterChoices =
        {
            null,
            QuickToolSource.AjTools,
            QuickToolSource.Revit
        };

        private static readonly string[] SourceFilterLabels =
        {
            "All",
            "AJ Tools only",
            "Revit commands only"
        };

        private readonly IList<QuickToolEntry> _allTools;
        private readonly List<string> _slotKeys = new List<string>();
        private bool _loading = true;

        /// <summary>The layout as saved. Only meaningful once ShowDialog returned true.</summary>
        internal QuickMenuConfig Result { get; private set; }

        internal QuickMenuSettingsWindow(IList<QuickToolEntry> allTools, QuickMenuConfig config)
        {
            InitializeComponent();

            _allTools = allTools ?? new List<QuickToolEntry>();

            QuickMenuConfig working = config ?? QuickMenuConfig.Load();
            working.Normalize();
            Result = working;

            _slotKeys.AddRange(working.Slots);

            FillChoiceCombos(working);
            FillToolList(string.Empty);
            RefreshSlotList(0);

            ToolList.SelectionChanged += OnListSelectionChanged;
            SlotList.SelectionChanged += OnListSelectionChanged;

            _loading = false;
            UpdateButtonStates();
        }

        #region Setting up the lists

        private void FillChoiceCombos(QuickMenuConfig config)
        {
            foreach (int choice in SlotCountChoices)
            {
                SlotCountCombo.Items.Add(new ComboBoxItem
                {
                    Content = choice.ToString(CultureInfo.InvariantCulture) + " tools",
                    Tag = choice
                });
            }

            SlotCountCombo.SelectedIndex = NearestIndex(SlotCountChoices, config.SlotCount);

            for (int i = 0; i < WheelSizeChoices.Length; i++)
            {
                WheelSizeCombo.Items.Add(new ComboBoxItem
                {
                    Content = WheelSizeLabels[i],
                    Tag = WheelSizeChoices[i]
                });
            }

            WheelSizeCombo.SelectedIndex = NearestIndex(WheelSizeChoices, config.Diameter);

            for (int i = 0; i < SourceFilterLabels.Length; i++)
            {
                SourceFilterCombo.Items.Add(new ComboBoxItem
                {
                    Content = SourceFilterLabels[i],
                    Tag = i
                });
            }

            SourceFilterCombo.SelectedIndex = 0;
        }

        /// <summary>Which source the Show combo is asking for, or null for everything.</summary>
        private QuickToolSource? SelectedSourceFilter()
        {
            var item = SourceFilterCombo.SelectedItem as ComboBoxItem;
            if (item == null || !(item.Tag is int))
            {
                return null;
            }

            int index = (int)item.Tag;
            return index >= 0 && index < SourceFilterChoices.Length
                ? SourceFilterChoices[index]
                : null;
        }

        private void FillToolList(string filter)
        {
            string previousKey = SelectedToolKey();

            ToolList.Items.Clear();

            string needle = (filter ?? string.Empty).Trim();
            QuickToolSource? wantedSource = SelectedSourceFilter();
            int shown = 0;

            foreach (QuickToolEntry entry in _allTools)
            {
                if (wantedSource.HasValue && entry.Source != wantedSource.Value)
                {
                    continue;
                }

                if (needle.Length > 0 && !Matches(entry, needle))
                {
                    continue;
                }

                var item = new ListBoxItem
                {
                    Content = entry.ListLabel,
                    Tag = entry,
                    ToolTip = string.IsNullOrEmpty(entry.Tooltip) ? null : entry.Tooltip
                };

                ToolList.Items.Add(item);
                shown++;

                if (previousKey != null && string.Equals(entry.Key, previousKey, StringComparison.OrdinalIgnoreCase))
                {
                    ToolList.SelectedItem = item;
                }
            }

            ToolCountText.Text = shown == _allTools.Count
                ? shown.ToString(CultureInfo.InvariantCulture) + " tools"
                : shown.ToString(CultureInfo.InvariantCulture) + " of " +
                  _allTools.Count.ToString(CultureInfo.InvariantCulture) + " tools";
        }

        private void RefreshSlotList(int selectIndex)
        {
            SlotList.Items.Clear();

            for (int i = 0; i < _slotKeys.Count; i++)
            {
                QuickToolEntry entry = FindTool(_slotKeys[i]);
                string label = (i + 1).ToString(CultureInfo.InvariantCulture) + ".   " +
                               (entry != null ? entry.DisplayName : "(empty)");

                SlotList.Items.Add(new ListBoxItem
                {
                    Content = label,
                    Tag = i,
                    ToolTip = entry != null ? entry.ListLabel : null
                });
            }

            if (SlotList.Items.Count > 0)
            {
                SlotList.SelectedIndex = Math.Min(Math.Max(selectIndex, 0), SlotList.Items.Count - 1);
            }
        }

        /// <summary>
        /// Search hit test. The label is matched first; a Revit command also matches its own
        /// unspaced name, so typing "visibilitygraphics" finds "Visibility Graphics".
        /// </summary>
        private static bool Matches(QuickToolEntry entry, string needle)
        {
            if (entry.ListLabel.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return !string.IsNullOrEmpty(entry.ItemName) &&
                   entry.ItemName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private QuickToolEntry FindTool(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            foreach (QuickToolEntry entry in _allTools)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private string SelectedToolKey()
        {
            var item = ToolList.SelectedItem as ListBoxItem;
            var entry = item != null ? item.Tag as QuickToolEntry : null;
            return entry != null ? entry.Key : null;
        }

        private static int NearestIndex(int[] choices, int value)
        {
            int bestIndex = 0;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < choices.Length; i++)
            {
                int distance = Math.Abs(choices[i] - value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        #endregion

        #region Events

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            FillToolList(SearchBox.Text);
            UpdateButtonStates();
        }

        private void OnSourceFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            FillToolList(SearchBox.Text);
            UpdateButtonStates();
        }

        private void OnSlotCountChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            int wanted = ChoiceValue(SlotCountCombo, QuickMenuConfig.DefaultSlotCount);

            while (_slotKeys.Count < wanted)
            {
                _slotKeys.Add(string.Empty);
            }

            while (_slotKeys.Count > wanted)
            {
                _slotKeys.RemoveAt(_slotKeys.Count - 1);
            }

            RefreshSlotList(SlotList.SelectedIndex);
            UpdateButtonStates();
        }

        private void OnWheelSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nothing to redraw here - the value is read straight off the combo when saving.
        }

        private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void OnToolListDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            AssignSelectedTool();
        }

        private void OnAssignClick(object sender, RoutedEventArgs e)
        {
            AssignSelectedTool();
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            int slot = SlotList.SelectedIndex;
            if (slot < 0 || slot >= _slotKeys.Count)
            {
                return;
            }

            _slotKeys[slot] = string.Empty;
            RefreshSlotList(slot);
            UpdateButtonStates();
        }

        private void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            SwapSlot(-1);
        }

        private void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            SwapSlot(1);
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            QuickMenuConfig config = QuickMenuConfig.Load();
            config.SlotCount = _slotKeys.Count;
            config.Diameter = ChoiceValue(WheelSizeCombo, QuickMenuConfig.DefaultDiameter);
            config.Normalize();

            for (int i = 0; i < config.Slots.Count && i < _slotKeys.Count; i++)
            {
                config.Slots[i] = _slotKeys[i];
            }

            config.Save();
            Result = config;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        #endregion

        #region Editing the slots

        private void AssignSelectedTool()
        {
            var item = ToolList.SelectedItem as ListBoxItem;
            var entry = item != null ? item.Tag as QuickToolEntry : null;
            int slot = SlotList.SelectedIndex;

            if (entry == null || slot < 0 || slot >= _slotKeys.Count)
            {
                return;
            }

            _slotKeys[slot] = entry.Key;

            // Step to the next slot so filling the wheel is one click per tool, top to bottom.
            int next = slot + 1 < _slotKeys.Count ? slot + 1 : slot;
            RefreshSlotList(next);
            UpdateButtonStates();
        }

        private void SwapSlot(int direction)
        {
            int slot = SlotList.SelectedIndex;
            int target = slot + direction;

            if (slot < 0 || slot >= _slotKeys.Count || target < 0 || target >= _slotKeys.Count)
            {
                return;
            }

            string moved = _slotKeys[slot];
            _slotKeys[slot] = _slotKeys[target];
            _slotKeys[target] = moved;

            RefreshSlotList(target);
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            int slot = SlotList.SelectedIndex;
            bool slotChosen = slot >= 0 && slot < _slotKeys.Count;
            bool toolChosen = SelectedToolKey() != null;

            AssignButton.IsEnabled = slotChosen && toolChosen;
            ClearButton.IsEnabled = slotChosen && !string.IsNullOrEmpty(_slotKeys[slot]);
            MoveUpButton.IsEnabled = slotChosen && slot > 0;
            MoveDownButton.IsEnabled = slotChosen && slot < _slotKeys.Count - 1;
        }

        private static int ChoiceValue(ComboBox combo, int fallback)
        {
            var item = combo.SelectedItem as ComboBoxItem;
            if (item == null || item.Tag == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(item.Tag, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        #endregion
    }
}
