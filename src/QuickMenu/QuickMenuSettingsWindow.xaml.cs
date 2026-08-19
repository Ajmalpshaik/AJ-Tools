#region Metadata
/*
 * Tool Name     : AJ Quick Menu (customise)
 * File Name     : QuickMenuSettingsWindow.xaml.cs
 * Purpose       : Lets Ajmal decide what the quick wheel holds - which AJ Tools button sits in each
 *                 slot, how many slots there are (4 to 12) and how big the wheel opens.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
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
 * - The tool list is filled with real ListBoxItem objects rather than data binding, because
 *   QuickToolEntry is an internal type and WPF binding wants public ones - the entry itself rides
 *   along in the item's Tag.
 * - The slots are shown as the wheel itself, drawn here in code with the same geometry and colours
 *   as QuickMenuWindow (slot 1 at the top, the rest clockwise), so the customise window shows what
 *   will actually open rather than a list standing in for it. The wedge shapes are
 *   IsHitTestVisible=false and the canvas hit-tests by angle, exactly as the real wheel does.
 * - Validation is inline (the Set button simply does nothing useful without both a tool and a slot
 *   selected, and both buttons enable/disable with the selection) - never a popup after the fact,
 *   per the house rule.
 * - Visibility values would be fully qualified (System.Windows.Visibility.*) per the house rule -
 *   none are needed in this file.
 *
 * Changelog     :
 * v1.2.0 (2026-08-19) - The slot list is now the wheel itself, and tools are dragged onto it: list
 *                       to slot fills, slot to slot swaps, slot back to the list empties. The Set /
 *                       Clear / Move buttons still work, now driven by the slot picked on the wheel.
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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

        /// <summary>Blank space left between two wedges, in degrees, per side. Matches the real wheel.</summary>
        private const double WedgeGapDegrees = 1.6;

        /// <summary>Hole in the middle, as a fraction of the outer radius. Matches the real wheel.</summary>
        private const double HubRadiusFraction = 0.42;

        /// <summary>Drag payload carrying a tool's key, dragged out of the tool list.</summary>
        private const string ToolKeyFormat = "AJTools.QuickMenu.ToolKey";

        /// <summary>Drag payload carrying a slot number, dragged off the wheel itself.</summary>
        private const string SlotIndexFormat = "AJTools.QuickMenu.SlotIndex";

        // The wheel is painted in the same colours as the real one, so this preview is not a diagram
        // of the wheel - it is what Ajmal is about to get.
        private static readonly Brush WedgeFill = MakeBrush(0xE5, 0x23, 0x28, 0x30);
        private static readonly Brush WedgeFillEmpty = MakeBrush(0x99, 0x1E, 0x1E, 0x22);
        private static readonly Brush WedgeFillSelected = MakeBrush(0xF2, 0x0D, 0x47, 0x6B);
        private static readonly Brush WedgeFillDrop = MakeBrush(0xF2, 0x12, 0x5E, 0x3A);
        private static readonly Brush WedgeStroke = MakeBrush(0x33, 0xFF, 0xFF, 0xFF);
        private static readonly Brush WedgeStrokeSelected = MakeBrush(0xFF, 0x4F, 0xC3, 0xF7);
        private static readonly Brush WedgeStrokeDrop = MakeBrush(0xFF, 0x5C, 0xD6, 0x8A);
        private static readonly Brush HubFill = MakeBrush(0xF7, 0x1A, 0x1D, 0x23);
        private static readonly Brush HubStroke = MakeBrush(0x59, 0xFF, 0xFF, 0xFF);
        private static readonly Brush WheelTextPrimary = MakeBrush(0xFF, 0xF2, 0xF7, 0xFA);
        private static readonly Brush WheelTextIdle = MakeBrush(0xFF, 0xC6, 0xD0, 0xD8);
        private static readonly Brush WheelTextDisabled = MakeBrush(0xFF, 0x6E, 0x78, 0x82);

        private readonly IList<QuickToolEntry> _allTools;
        private readonly List<string> _slotKeys = new List<string>();
        private readonly List<Path> _wedges = new List<Path>();
        private bool _loading = true;

        /// <summary>Which slot the middle buttons act on. -1 when nothing is picked.</summary>
        private int _selectedSlot = -1;

        /// <summary>Which slot a drag is currently hovering over. -1 when none.</summary>
        private int _dropSlot = -1;

        private Point _dragOrigin;
        private int _dragFromSlot = -1;

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
            RefreshWheel(0);

            ToolList.SelectionChanged += OnListSelectionChanged;

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

        /// <summary>
        /// Redraws the wheel and picks a slot. The geometry deliberately mirrors QuickMenuWindow -
        /// slot 1 at the top, the rest clockwise - so what Ajmal arranges here is what opens.
        /// </summary>
        private void RefreshWheel(int selectIndex)
        {
            _selectedSlot = _slotKeys.Count == 0
                ? -1
                : Math.Min(Math.Max(selectIndex, 0), _slotKeys.Count - 1);

            WheelCanvas.Children.Clear();
            _wedges.Clear();

            for (int i = 0; i < _slotKeys.Count; i++)
            {
                AddWedge(i);
            }

            AddHub();
            UpdateWheelVisuals();
        }

        private double WheelCentre
        {
            get { return WheelCanvas.Width / 2.0; }
        }

        private double OuterRadius
        {
            get { return WheelCentre - 4.0; }
        }

        private double InnerRadius
        {
            get { return OuterRadius * HubRadiusFraction; }
        }

        private double StepDegrees
        {
            get { return _slotKeys.Count > 0 ? 360.0 / _slotKeys.Count : 360.0; }
        }

        private void AddWedge(int index)
        {
            QuickToolEntry entry = FindTool(_slotKeys[index]);
            bool filled = entry != null;

            double step = StepDegrees;
            double centreAngle = -90.0 + (index * step);
            double startAngle = centreAngle - (step / 2.0) + WedgeGapDegrees;
            double endAngle = centreAngle + (step / 2.0) - WedgeGapDegrees;

            var wedge = new Path
            {
                Data = BuildWedgeGeometry(startAngle, endAngle),
                Fill = filled ? WedgeFill : WedgeFillEmpty,
                Stroke = WedgeStroke,
                StrokeThickness = 1.0,

                // The canvas does the hit testing by angle, exactly like the real wheel, so the
                // shapes themselves must stay out of the way.
                IsHitTestVisible = false
            };

            WheelCanvas.Children.Add(wedge);
            _wedges.Add(wedge);

            double contentRadius = (InnerRadius + OuterRadius) / 2.0;
            Point contentCentre = Polar(contentRadius, centreAngle);

            // Same reasoning as the real wheel: measure the room at the wedge's inner corners, where
            // it is narrowest, and never let the block outgrow the thickness of the ring.
            double halfWedgeRadians = ((step / 2.0) - WedgeGapDegrees) * Math.PI / 180.0;
            double innerCornerRadius = Math.Max(InnerRadius + 4.0, contentRadius - 26.0);
            double angularAllowance = 2.0 * innerCornerRadius * Math.Tan(Math.Min(halfWedgeRadians, 1.2));
            double radialAllowance = (OuterRadius - InnerRadius) * 0.90;
            double contentWidth = Math.Max(42.0, Math.Min(angularAllowance, radialAllowance));

            var stack = new StackPanel
            {
                Width = contentWidth,
                IsHitTestVisible = false
            };

            if (filled && entry.Icon != null)
            {
                stack.Children.Add(new Image
                {
                    Source = entry.Icon,
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(0, 0, 0, 3),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    SnapsToDevicePixels = true
                });
            }

            stack.Children.Add(new TextBlock
            {
                Text = filled ? entry.DisplayName : "empty",
                FontSize = 10.5,
                MaxHeight = 30,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = filled ? WheelTextPrimary : WheelTextDisabled,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Measure(new Size(stack.Width, double.PositiveInfinity));
            Canvas.SetLeft(stack, contentCentre.X - (stack.Width / 2.0));
            Canvas.SetTop(stack, contentCentre.Y - (stack.DesiredSize.Height / 2.0));
            WheelCanvas.Children.Add(stack);

            // Slot number out by the rim, the same place the real wheel puts its 1-9 keys.
            Point digitCentre = Polar(OuterRadius - 11.0, centreAngle);
            var digit = new TextBlock
            {
                Text = (index + 1).ToString(CultureInfo.InvariantCulture),
                FontSize = 10.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = WheelTextIdle,
                IsHitTestVisible = false
            };

            digit.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(digit, digitCentre.X - (digit.DesiredSize.Width / 2.0));
            Canvas.SetTop(digit, digitCentre.Y - (digit.DesiredSize.Height / 2.0));
            WheelCanvas.Children.Add(digit);
        }

        private void AddHub()
        {
            double inner = InnerRadius;

            var hub = new Ellipse
            {
                Width = inner * 2.0,
                Height = inner * 2.0,
                Fill = HubFill,
                Stroke = HubStroke,
                StrokeThickness = 1.0,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(hub, WheelCentre - inner);
            Canvas.SetTop(hub, WheelCentre - inner);
            WheelCanvas.Children.Add(hub);

            var caption = new TextBlock
            {
                Width = (inner * 2.0) - 16.0,
                Text = _slotKeys.Count.ToString(CultureInfo.InvariantCulture) + " slots",
                FontSize = 11.0,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = WheelTextIdle,
                IsHitTestVisible = false
            };

            caption.Measure(new Size(caption.Width, double.PositiveInfinity));
            Canvas.SetLeft(caption, WheelCentre - (caption.Width / 2.0));
            Canvas.SetTop(caption, WheelCentre - (caption.DesiredSize.Height / 2.0));
            WheelCanvas.Children.Add(caption);
        }

        private Geometry BuildWedgeGeometry(double startAngle, double endAngle)
        {
            double outer = OuterRadius;
            double inner = InnerRadius;
            bool largeArc = (endAngle - startAngle) > 180.0;

            var figure = new PathFigure
            {
                StartPoint = Polar(outer, startAngle),
                IsClosed = true,
                IsFilled = true
            };

            figure.Segments.Add(new ArcSegment(
                Polar(outer, endAngle),
                new Size(outer, outer),
                0.0,
                largeArc,
                SweepDirection.Clockwise,
                true));

            figure.Segments.Add(new LineSegment(Polar(inner, endAngle), true));

            figure.Segments.Add(new ArcSegment(
                Polar(inner, startAngle),
                new Size(inner, inner),
                0.0,
                largeArc,
                SweepDirection.Counterclockwise,
                true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        private Point Polar(double radius, double angleDegrees)
        {
            double radians = angleDegrees * Math.PI / 180.0;
            double centre = WheelCentre;
            return new Point(
                centre + (radius * Math.Cos(radians)),
                centre + (radius * Math.Sin(radians)));
        }

        /// <summary>Which slot the pointer is over, or -1 for the hub / outside the ring.</summary>
        private int IndexAt(Point point)
        {
            if (_slotKeys.Count == 0)
            {
                return -1;
            }

            double centre = WheelCentre;
            double dx = point.X - centre;
            double dy = point.Y - centre;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));

            if (distance < InnerRadius || distance > OuterRadius)
            {
                return -1;
            }

            double step = StepDegrees;
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double shifted = angle + 90.0 + (step / 2.0);
            shifted = ((shifted % 360.0) + 360.0) % 360.0;

            int index = (int)(shifted / step);
            return index >= 0 && index < _slotKeys.Count ? index : -1;
        }

        /// <summary>Repaints the selected and drop-target wedges without rebuilding the whole wheel.</summary>
        private void UpdateWheelVisuals()
        {
            for (int i = 0; i < _wedges.Count; i++)
            {
                bool filled = FindTool(_slotKeys[i]) != null;
                Brush fill = filled ? WedgeFill : WedgeFillEmpty;
                Brush stroke = WedgeStroke;
                double thickness = 1.0;

                if (i == _selectedSlot)
                {
                    fill = WedgeFillSelected;
                    stroke = WedgeStrokeSelected;
                    thickness = 1.6;
                }

                // A live drop target wins over the selection, so the landing spot is never in doubt.
                if (i == _dropSlot)
                {
                    fill = WedgeFillDrop;
                    stroke = WedgeStrokeDrop;
                    thickness = 2.0;
                }

                _wedges[i].Fill = fill;
                _wedges[i].Stroke = stroke;
                _wedges[i].StrokeThickness = thickness;
            }

            UpdateWheelHint();
        }

        private void UpdateWheelHint()
        {
            if (_selectedSlot < 0 || _selectedSlot >= _slotKeys.Count)
            {
                WheelHintText.Text = "Drag a tool from the list onto any slot.";
                return;
            }

            QuickToolEntry entry = FindTool(_slotKeys[_selectedSlot]);
            string slotName = "Slot " + (_selectedSlot + 1).ToString(CultureInfo.InvariantCulture);

            WheelHintText.Text = entry != null
                ? slotName + ": " + entry.DisplayName
                : slotName + " is empty - drop a tool on it.";
        }

        private static Brush MakeBrush(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
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

            RefreshWheel(_selectedSlot);
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
            int slot = _selectedSlot;
            if (slot < 0 || slot >= _slotKeys.Count)
            {
                return;
            }

            _slotKeys[slot] = string.Empty;
            RefreshWheel(slot);
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
            int slot = _selectedSlot;

            if (entry == null || slot < 0 || slot >= _slotKeys.Count)
            {
                return;
            }

            _slotKeys[slot] = entry.Key;

            // Step to the next slot so filling the wheel is one click per tool, right round it.
            int next = slot + 1 < _slotKeys.Count ? slot + 1 : slot;
            RefreshWheel(next);
            UpdateButtonStates();
        }

        private void SwapSlot(int direction)
        {
            int slot = _selectedSlot;
            int target = slot + direction;

            if (slot < 0 || slot >= _slotKeys.Count || target < 0 || target >= _slotKeys.Count)
            {
                return;
            }

            string moved = _slotKeys[slot];
            _slotKeys[slot] = _slotKeys[target];
            _slotKeys[target] = moved;

            RefreshWheel(target);
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            int slot = _selectedSlot;
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

        #region Drag and drop

        // Three drags are supported, and they are all the same two steps - pick something up, decide
        // what the wedge under the pointer should become:
        //   tool list -> wedge   fills that slot
        //   wedge     -> wedge   swaps the two slots
        //   wedge     -> list    empties that slot

        private void OnToolListMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragOrigin = e.GetPosition(null);
        }

        private void OnToolListMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !PastDragThreshold(e))
            {
                return;
            }

            string key = SelectedToolKey();
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            DragDrop.DoDragDrop(ToolList, new DataObject(ToolKeyFormat, key), DragDropEffects.Copy);
        }

        private void OnWheelMouseDown(object sender, MouseButtonEventArgs e)
        {
            int index = IndexAt(e.GetPosition(WheelCanvas));

            _dragOrigin = e.GetPosition(null);
            _dragFromSlot = index;

            if (index >= 0)
            {
                RefreshWheel(index);
                UpdateButtonStates();
            }
        }

        private void OnWheelMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragFromSlot < 0 || !PastDragThreshold(e))
            {
                return;
            }

            int from = _dragFromSlot;

            // An empty slot has nothing to pick up.
            if (from >= _slotKeys.Count || string.IsNullOrEmpty(_slotKeys[from]))
            {
                return;
            }

            // Cleared before the call, because DoDragDrop blocks until the drop finishes and the
            // mouse-up that would normally reset this never reaches us.
            _dragFromSlot = -1;

            DragDrop.DoDragDrop(WheelCanvas, new DataObject(SlotIndexFormat, from), DragDropEffects.Move);
        }

        private void OnWheelMouseLeave(object sender, MouseEventArgs e)
        {
            _dragFromSlot = -1;
        }

        private void OnWheelDragOver(object sender, DragEventArgs e)
        {
            int index = IndexAt(e.GetPosition(WheelCanvas));
            bool carryingTool = e.Data.GetDataPresent(ToolKeyFormat);
            bool carryingSlot = e.Data.GetDataPresent(SlotIndexFormat);

            if (index < 0 || (!carryingTool && !carryingSlot))
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = carryingTool ? DragDropEffects.Copy : DragDropEffects.Move;
            }

            if (index != _dropSlot)
            {
                _dropSlot = index;
                UpdateWheelVisuals();
            }

            e.Handled = true;
        }

        private void OnWheelDragLeave(object sender, DragEventArgs e)
        {
            if (_dropSlot != -1)
            {
                _dropSlot = -1;
                UpdateWheelVisuals();
            }
        }

        private void OnWheelDrop(object sender, DragEventArgs e)
        {
            int index = IndexAt(e.GetPosition(WheelCanvas));

            _dropSlot = -1;
            _dragFromSlot = -1;

            if (index < 0 || index >= _slotKeys.Count)
            {
                UpdateWheelVisuals();
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(ToolKeyFormat))
            {
                var key = e.Data.GetData(ToolKeyFormat) as string;
                if (!string.IsNullOrEmpty(key))
                {
                    _slotKeys[index] = key;
                }
            }
            else if (e.Data.GetDataPresent(SlotIndexFormat))
            {
                int from = SlotIndexFrom(e.Data);
                if (from >= 0 && from < _slotKeys.Count && from != index)
                {
                    string moved = _slotKeys[from];
                    _slotKeys[from] = _slotKeys[index];
                    _slotKeys[index] = moved;
                }
            }

            RefreshWheel(index);
            UpdateButtonStates();
            e.Handled = true;
        }

        private void OnToolListDragOver(object sender, DragEventArgs e)
        {
            // Only a slot can be dropped here, and doing so empties it. A tool dragged back to its
            // own list is a cancelled drag, not an instruction.
            e.Effects = e.Data.GetDataPresent(SlotIndexFormat)
                ? DragDropEffects.Move
                : DragDropEffects.None;

            e.Handled = true;
        }

        private void OnToolListDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(SlotIndexFormat))
            {
                return;
            }

            int from = SlotIndexFrom(e.Data);
            if (from >= 0 && from < _slotKeys.Count)
            {
                _slotKeys[from] = string.Empty;
                RefreshWheel(from);
                UpdateButtonStates();
            }

            e.Handled = true;
        }

        private bool PastDragThreshold(MouseEventArgs e)
        {
            Vector moved = e.GetPosition(null) - _dragOrigin;

            return Math.Abs(moved.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(moved.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        private static int SlotIndexFrom(IDataObject data)
        {
            object raw = data.GetData(SlotIndexFormat);
            return raw is int ? (int)raw : -1;
        }

        #endregion
    }
}
