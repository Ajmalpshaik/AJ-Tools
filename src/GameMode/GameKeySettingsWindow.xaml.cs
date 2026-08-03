#region Metadata
/*
 * Tool Name     : AJ Game Mode (key settings window)
 * File Name     : GameKeySettingsWindow.xaml.cs
 * Purpose       : Remap every Game Mode shortcut: click an action's key button, press the new key,
 *                 Save. Opened from the game's pause pill (S). Pure UI - no Revit API, so a plain
 *                 modal ShowDialog is safe while the game is paused.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-29
 * Last Updated  : 2026-07-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF)
 *
 * Dependencies  : GameKeyBindings. Self-contained styling (no shared resource dictionaries), so a
 *                 missing StaticResource can never crash it at runtime.
 *
 * Input         : The live GameKeyBindings instance. Cancel restores the state it opened with.
 * Output        : Bindings saved to %AppData%\AJTools\ajgame-keys.txt on Save.
 *
 * Notes         :
 * - Validation is inline per the house rule - a reserved or duplicate key shows a message and the
 *   window stays open; nothing is thrown away.
 *
 * Changelog     :
 * v1.0.0 (2026-07-29) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AJTools.Services.GameMode;

namespace AJTools.UI.GameMode
{
    /// <summary>Click-a-button-then-press-a-key remapping for all Game Mode actions.</summary>
    public partial class GameKeySettingsWindow : Window
    {
        private readonly GameKeyBindings _bindings;
        private readonly List<KeyValuePair<string, Key>> _snapshot = new List<KeyValuePair<string, Key>>();
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private string _capturingActionId;
        private bool _saved;

        private static readonly HashSet<Key> ReservedKeys = new HashSet<Key>
        {
            Key.Escape, Key.Enter, Key.Return, Key.Tab, Key.System,
            Key.Left, Key.Right, Key.Up, Key.Down,
            Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.D0,
            Key.NumPad0, Key.NumPad1, Key.NumPad2, Key.NumPad3, Key.NumPad4,
            Key.NumPad5, Key.NumPad6, Key.NumPad7, Key.NumPad8, Key.NumPad9,
            Key.LWin, Key.RWin, Key.Apps
        };

        internal GameKeySettingsWindow(GameKeyBindings bindings)
        {
            _bindings = bindings;
            InitializeComponent();

            foreach (GameKeyAction action in GameKeyBindings.Actions)
            {
                _snapshot.Add(new KeyValuePair<string, Key>(action.Id, bindings.Get(action.Id)));
            }

            BuildRows();
            PreviewKeyDown += OnCaptureKeyDown;
            Closed += OnClosedRestoreIfNotSaved;
        }

        private void BuildRows()
        {
            RowsPanel.Children.Clear();
            _buttons.Clear();

            foreach (GameKeyAction action in GameKeyBindings.Actions)
            {
                var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

                var label = new TextBlock
                {
                    Text = action.Display,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xD4, 0xE0)),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var button = new Button
                {
                    Content = _bindings.Get(action.Id).ToString(),
                    Tag = action.Id,
                    Padding = new Thickness(10, 6, 10, 6),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x22, 0x30)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0xFF)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x40, 0x52)),
                    BorderThickness = new Thickness(1)
                };
                button.Click += OnKeyButtonClick;
                Grid.SetColumn(button, 1);
                grid.Children.Add(button);

                _buttons[action.Id] = button;
                RowsPanel.Children.Add(grid);
            }
        }

        private void OnKeyButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                return;
            }

            // Leave any previous capture cleanly.
            CancelCapture();

            _capturingActionId = (string)button.Tag;
            button.Content = "press a key...";
            button.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC5, 0x3D));
            ErrorText.Text = "";
            Keyboard.Focus(this);
        }

        private void OnCaptureKeyDown(object sender, KeyEventArgs e)
        {
            if (_capturingActionId == null)
            {
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            e.Handled = true;

            if (key == Key.Escape)
            {
                CancelCapture();
                return;
            }

            if (ReservedKeys.Contains(key))
            {
                ErrorText.Text = "That key is reserved by the game (Esc, Enter, arrows, numbers). Try another key.";
                return;
            }

            string holder = _bindings.FindAction(key);
            if (holder != null && holder != _capturingActionId)
            {
                string holderName = holder;
                foreach (GameKeyAction action in GameKeyBindings.Actions)
                {
                    if (action.Id == holder) { holderName = action.Display; break; }
                }
                ErrorText.Text = "\"" + key + "\" is already used by \"" + holderName + "\". Try another key.";
                return;
            }

            _bindings.TrySet(_capturingActionId, key);
            Button button = _buttons[_capturingActionId];
            button.Content = key.ToString();
            button.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0xFF));
            _capturingActionId = null;
            ErrorText.Text = "";
        }

        private void CancelCapture()
        {
            if (_capturingActionId == null)
            {
                return;
            }

            Button button = _buttons[_capturingActionId];
            button.Content = _bindings.Get(_capturingActionId).ToString();
            button.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0xFF));
            _capturingActionId = null;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            CancelCapture();
            _bindings.Save();
            _saved = true;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnClosedRestoreIfNotSaved(object sender, EventArgs e)
        {
            if (_saved)
            {
                return;
            }

            foreach (KeyValuePair<string, Key> pair in _snapshot)
            {
                _bindings.ForceSet(pair.Key, pair.Value);
            }
        }
    }
}
