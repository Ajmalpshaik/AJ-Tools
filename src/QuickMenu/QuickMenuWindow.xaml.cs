#region Metadata
/*
 * Tool Name     : AJ Quick Menu (wheel)
 * File Name     : QuickMenuWindow.xaml.cs
 * Purpose       : The radial tool wheel itself - the video-game style ring that opens centred on
 *                 the mouse pointer, lights up the wedge the pointer is pointing at, and hands the
 *                 chosen tool back to CmdQuickMenu to run.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-18
 * Last Updated  : 2026-08-18
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF, modal overlay)
 *
 * Dependencies  : QuickToolEntry (what to draw), user32 GetCursorPos (where the pointer is)
 *
 * Input         : The slot list and wheel diameter from QuickMenuConfig.
 * Output        : SelectedEntry (the tool to run) or OpenSettingsRequested. This window never
 *                 touches the Revit model - it only reports the choice back.
 *
 * Notes         :
 * - The whole ring is drawn from code because the slot count is Ajmal's own setting (4 to 12), so
 *   there is no fixed XAML layout to write - each wedge is a donut-segment Path built from two arcs.
 * - Aiming is worked out from the pointer ANGLE, not from which shape is under the pointer, exactly
 *   like a game wheel: point in a direction and that wedge lights up, whether the pointer is near
 *   the hub or way out at the rim. Number keys 1-9 pick a slot directly.
 * - Placement: the pointer is read in real screen pixels (GetCursorPos) and converted to WPF units
 *   through this window's own CompositionTarget, so a 125%/150% display puts the wheel on the
 *   pointer rather than beside it. The result is then clamped so the ring cannot open half off the
 *   screen.
 * - Slot layout was checked by re-implementing this file's wedge maths outside Revit and rendering it,
 *   for every slot count at every wheel size. Known limit: at 12 slots the empty corners of a label's
 *   bounding box graze the divider lines. Centred text puts no ink there, so it is left alone.
 * - Modal on purpose (ShowDialog). CmdQuickMenu has to still be inside its own Execute() when it
 *   posts the chosen tool to Revit, and a modal wheel is what guarantees that.
 * - QuickMenuSettingsWindow draws this same ring, at a smaller size, so the customise window shows
 *   the real thing and can be dragged onto. The wedge maths and palette are duplicated there on
 *   purpose rather than shared - if the geometry or colours change here, change them there too.
 * - Visibility values are fully qualified (System.Windows.Visibility.*) per the house rule about
 *   UIElement.Visibility shadowing the enum type name. No Application.Current anywhere.
 *
 * Changelog     :
 * v1.0.0 (2026-08-18) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using AJTools.Services.QuickMenu;

namespace AJTools.UI.QuickMenu
{
    /// <summary>The AJ Tools quick-tool wheel, opened at the mouse pointer.</summary>
    public partial class QuickMenuWindow : Window
    {
        /// <summary>Blank space left between two wedges, in degrees, per side.</summary>
        private const double WedgeGapDegrees = 1.6;

        /// <summary>Hole in the middle, as a fraction of the outer radius.</summary>
        private const double HubRadiusFraction = 0.42;

        /// <summary>Breathing room between the ring and the window edge, in WPF units.</summary>
        private const double EdgePadding = 16.0;

        /// <summary>
        /// Half the tallest a slot's icon-plus-label block gets (34 icon + 5 gap + two 17pt lines),
        /// used to work out how wide that block may be before its lower corners leave the wedge.
        /// </summary>
        private const double EstimatedContentHalfHeight = 44.0;

        private static readonly Brush WedgeFill = MakeBrush(0xE5, 0x23, 0x28, 0x30);
        private static readonly Brush WedgeFillHover = MakeBrush(0xF2, 0x0D, 0x47, 0x6B);
        private static readonly Brush WedgeFillEmpty = MakeBrush(0x99, 0x1E, 0x1E, 0x22);
        private static readonly Brush WedgeStroke = MakeBrush(0x33, 0xFF, 0xFF, 0xFF);
        private static readonly Brush WedgeStrokeHover = MakeBrush(0xFF, 0x4F, 0xC3, 0xF7);
        private static readonly Brush HubFill = MakeBrush(0xF7, 0x1A, 0x1D, 0x23);
        private static readonly Brush HubStroke = MakeBrush(0x59, 0xFF, 0xFF, 0xFF);
        private static readonly Brush TextPrimary = MakeBrush(0xFF, 0xF2, 0xF7, 0xFA);
        private static readonly Brush TextIdle = MakeBrush(0xFF, 0xC6, 0xD0, 0xD8);
        private static readonly Brush TextSecondary = MakeBrush(0xFF, 0xA8, 0xB4, 0xBE);
        private static readonly Brush TextDisabled = MakeBrush(0xFF, 0x6E, 0x78, 0x82);
        private static readonly Brush TextAccent = MakeBrush(0xFF, 0x6F, 0xD2, 0xFF);

        private readonly IList<QuickToolEntry> _slots;
        private readonly int _slotCount;
        private readonly double _outerRadius;
        private readonly double _innerRadius;
        private readonly double _centre;
        private readonly double _stepDegrees;

        private readonly List<Path> _wedges = new List<Path>();
        private readonly List<TextBlock> _labels = new List<TextBlock>();
        private readonly DropShadowEffect _hoverGlow;

        private TextBlock _hubToolText;
        private TextBlock _hubHintText;
        private int _hoverIndex = -1;
        private bool _choiceMade;
        private bool _autoCloseArmed;

        /// <summary>The tool Ajmal picked, or null if he closed the wheel without choosing.</summary>
        internal QuickToolEntry SelectedEntry { get; private set; }

        /// <summary>True when he asked for the customise window instead of running a tool.</summary>
        internal bool OpenSettingsRequested { get; private set; }

        /// <summary>
        /// Builds the wheel. <paramref name="slots"/> holds one entry per wedge, clockwise from the
        /// top, with null for an empty slot.
        /// </summary>
        internal QuickMenuWindow(IList<QuickToolEntry> slots, int diameter)
        {
            InitializeComponent();

            // A wheel with no slots at all cannot be drawn or aimed at, so an empty list becomes a
            // single empty wedge rather than an index that does not exist.
            _slots = slots != null && slots.Count > 0
                ? slots
                : new List<QuickToolEntry> { null };
            _slotCount = _slots.Count;
            _stepDegrees = 360.0 / _slotCount;

            double size = diameter + (EdgePadding * 2.0);
            Width = size;
            Height = size;
            _centre = size / 2.0;
            _outerRadius = diameter / 2.0;
            _innerRadius = _outerRadius * HubRadiusFraction;

            _hoverGlow = new DropShadowEffect
            {
                Color = Color.FromRgb(0x4F, 0xC3, 0xF7),
                BlurRadius = 22,
                ShadowDepth = 0,
                Opacity = 0.85
            };
            _hoverGlow.Freeze();

            Cursor = Cursors.Arrow;

            BuildWheel();

            Loaded += OnWheelLoaded;
            MouseMove += OnWheelMouseMove;
            MouseLeftButtonUp += OnWheelLeftButtonUp;
            MouseRightButtonUp += OnWheelRightButtonUp;
            PreviewKeyDown += OnWheelKeyDown;
            Deactivated += OnWheelDeactivated;
        }

        #region Placement

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            PlaceOnPointer();
        }

        /// <summary>Centres the window on the mouse pointer, kept fully on screen.</summary>
        private void PlaceOnPointer()
        {
            try
            {
                QuickMenuNative.NativePoint cursor;
                if (!QuickMenuNative.GetCursorPos(out cursor))
                {
                    return;
                }

                // Screen pixels -> WPF units, so 125%/150% displays still land on the pointer.
                var pointer = new Point(cursor.X, cursor.Y);
                PresentationSource source = PresentationSource.FromVisual(this);
                if (source != null && source.CompositionTarget != null)
                {
                    pointer = source.CompositionTarget.TransformFromDevice.Transform(pointer);
                }

                double left = pointer.X - (Width / 2.0);
                double top = pointer.Y - (Height / 2.0);

                double minLeft = SystemParameters.VirtualScreenLeft;
                double minTop = SystemParameters.VirtualScreenTop;
                double maxLeft = minLeft + SystemParameters.VirtualScreenWidth - Width;
                double maxTop = minTop + SystemParameters.VirtualScreenHeight - Height;

                Left = maxLeft > minLeft ? Math.Min(Math.Max(left, minLeft), maxLeft) : minLeft;
                Top = maxTop > minTop ? Math.Min(Math.Max(top, minTop), maxTop) : minTop;
            }
            catch (Exception)
            {
                // Placement is cosmetic - a wheel in the default position still works.
            }
        }

        private void OnWheelLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnWheelLoaded;
            Activate();
            Focus();
            Keyboard.Focus(this);
            PlayPopIn();

            // Close-on-lose-focus is only armed once the window is genuinely up. Arming it in the
            // constructor risks a stray activation change during opening closing the wheel instantly.
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => _autoCloseArmed = true));
        }

        private void PlayPopIn()
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(130));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var grow = new DoubleAnimation(0.86, 1.0, duration) { EasingFunction = ease };
            PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

            var fade = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = ease };
            RootGrid.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        #endregion

        #region Drawing

        private void BuildWheel()
        {
            WheelCanvas.Children.Clear();
            _wedges.Clear();
            _labels.Clear();

            for (int i = 0; i < _slotCount; i++)
            {
                AddWedge(i);
            }

            AddHub();
        }

        private void AddWedge(int index)
        {
            QuickToolEntry entry = _slots[index];
            bool filled = entry != null;

            double centreAngle = -90.0 + (index * _stepDegrees);
            double startAngle = centreAngle - (_stepDegrees / 2.0) + WedgeGapDegrees;
            double endAngle = centreAngle + (_stepDegrees / 2.0) - WedgeGapDegrees;

            var wedge = new Path
            {
                Data = BuildWedgeGeometry(startAngle, endAngle),
                Fill = filled ? WedgeFill : WedgeFillEmpty,
                Stroke = WedgeStroke,
                StrokeThickness = 1.0,
                IsHitTestVisible = false
            };

            WheelCanvas.Children.Add(wedge);
            _wedges.Add(wedge);

            // Icon + label, sitting halfway between the hub and the rim.
            double contentRadius = (_innerRadius + _outerRadius) / 2.0;
            Point contentCentre = Polar(contentRadius, centreAngle);

            // Fit the icon + label inside the wedge. The tight spot is the INNER corners of the text
            // block, not its middle: a wedge narrows towards the hub, so a block measured against
            // the wedge's width at the content radius still spills over the divider lower down.
            // Measure the allowance at the inner corners instead, and never let the block be wider
            // than the ring is thick (that is what bites on a 4-slot wheel, where the wedge is huge
            // but the ring is not).
            double halfWedgeRadians = ((_stepDegrees / 2.0) - WedgeGapDegrees) * Math.PI / 180.0;
            double innerCornerRadius = Math.Max(_innerRadius + 6.0, contentRadius - EstimatedContentHalfHeight);
            double angularAllowance = 2.0 * innerCornerRadius * Math.Tan(Math.Min(halfWedgeRadians, 1.2));
            double radialAllowance = (_outerRadius - _innerRadius) * 0.90;
            double contentWidth = Math.Max(60.0, Math.Min(angularAllowance, radialAllowance));

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
                    Width = 34,
                    Height = 34,
                    Margin = new Thickness(0, 0, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    SnapsToDevicePixels = true
                });
            }

            var label = new TextBlock
            {
                Text = filled ? entry.DisplayName : "empty",
                Foreground = filled ? TextIdle : TextDisabled,
                FontSize = 12.5,
                FontWeight = filled ? FontWeights.SemiBold : FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 46,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            stack.Children.Add(label);
            _labels.Add(label);

            stack.Measure(new Size(stack.Width, double.PositiveInfinity));
            Canvas.SetLeft(stack, contentCentre.X - (stack.Width / 2.0));
            Canvas.SetTop(stack, contentCentre.Y - (stack.DesiredSize.Height / 2.0));
            WheelCanvas.Children.Add(stack);

            // Number key hint, just inside the rim - 1 to 9 only, since those are the keys that work.
            if (index < 9)
            {
                var digit = new TextBlock
                {
                    Text = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Foreground = TextDisabled,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    Width = 16,
                    IsHitTestVisible = false
                };

                Point digitCentre = Polar(_outerRadius - 15.0, centreAngle);
                digit.Measure(new Size(16, double.PositiveInfinity));
                Canvas.SetLeft(digit, digitCentre.X - 8.0);
                Canvas.SetTop(digit, digitCentre.Y - (digit.DesiredSize.Height / 2.0));
                WheelCanvas.Children.Add(digit);
            }
        }

        private Geometry BuildWedgeGeometry(double startAngle, double endAngle)
        {
            bool isLargeArc = (endAngle - startAngle) > 180.0;

            var figure = new PathFigure
            {
                StartPoint = Polar(_outerRadius, startAngle),
                IsClosed = true,
                IsFilled = true
            };

            figure.Segments.Add(new ArcSegment(
                Polar(_outerRadius, endAngle),
                new Size(_outerRadius, _outerRadius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));

            figure.Segments.Add(new LineSegment(Polar(_innerRadius, endAngle), true));

            figure.Segments.Add(new ArcSegment(
                Polar(_innerRadius, startAngle),
                new Size(_innerRadius, _innerRadius),
                0,
                isLargeArc,
                SweepDirection.Counterclockwise,
                true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        private void AddHub()
        {
            var hub = new Ellipse
            {
                Width = _innerRadius * 2.0,
                Height = _innerRadius * 2.0,
                Fill = HubFill,
                Stroke = HubStroke,
                StrokeThickness = 1.0,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(hub, _centre - _innerRadius);
            Canvas.SetTop(hub, _centre - _innerRadius);
            WheelCanvas.Children.Add(hub);

            var stack = new StackPanel
            {
                Width = (_innerRadius * 2.0) - 22.0,
                IsHitTestVisible = false
            };

            stack.Children.Add(new TextBlock
            {
                Text = "QUICK MENU",
                Foreground = TextSecondary,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            _hubToolText = new TextBlock
            {
                Text = "Point at a tool",
                Foreground = TextAccent,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 60,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(_hubToolText);

            _hubHintText = new TextBlock
            {
                Text = "Click to run    S: customise    Esc: close",
                Foreground = TextDisabled,
                FontSize = 10.5,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(_hubHintText);

            stack.Measure(new Size(stack.Width, double.PositiveInfinity));
            Canvas.SetLeft(stack, _centre - (stack.Width / 2.0));
            Canvas.SetTop(stack, _centre - (stack.DesiredSize.Height / 2.0));
            WheelCanvas.Children.Add(stack);
        }

        private Point Polar(double radius, double angleDegrees)
        {
            double radians = angleDegrees * Math.PI / 180.0;
            return new Point(
                _centre + (radius * Math.Cos(radians)),
                _centre + (radius * Math.Sin(radians)));
        }

        #endregion

        #region Aiming and choosing

        private void OnWheelMouseMove(object sender, MouseEventArgs e)
        {
            SetHover(IndexAt(e.GetPosition(WheelCanvas)));
        }

        /// <summary>Which wedge the pointer is aiming at, or -1 for the hub / outside the ring.</summary>
        private int IndexAt(Point point)
        {
            double dx = point.X - _centre;
            double dy = point.Y - _centre;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));

            if (distance < _innerRadius || distance > _outerRadius)
            {
                return -1;
            }

            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double shifted = angle + 90.0 + (_stepDegrees / 2.0);
            shifted = ((shifted % 360.0) + 360.0) % 360.0;

            int index = (int)(shifted / _stepDegrees);
            return index >= _slotCount ? _slotCount - 1 : index;
        }

        private void SetHover(int index)
        {
            if (index == _hoverIndex)
            {
                return;
            }

            if (_hoverIndex >= 0 && _hoverIndex < _wedges.Count)
            {
                bool wasFilled = _slots[_hoverIndex] != null;
                Path previous = _wedges[_hoverIndex];
                previous.Fill = wasFilled ? WedgeFill : WedgeFillEmpty;
                previous.Stroke = WedgeStroke;
                previous.StrokeThickness = 1.0;
                previous.Effect = null;
                _labels[_hoverIndex].Foreground = wasFilled ? TextIdle : TextDisabled;
            }

            _hoverIndex = index;

            if (_hoverIndex >= 0 && _hoverIndex < _wedges.Count)
            {
                bool isFilled = _slots[_hoverIndex] != null;
                Path current = _wedges[_hoverIndex];
                current.Fill = isFilled ? WedgeFillHover : WedgeFillEmpty;
                current.Stroke = WedgeStrokeHover;
                current.StrokeThickness = 2.2;
                current.Effect = _hoverGlow;
                _labels[_hoverIndex].Foreground = isFilled ? TextPrimary : TextDisabled;
            }

            UpdateHubText();
        }

        private void UpdateHubText()
        {
            if (_hubToolText == null || _hubHintText == null)
            {
                return;
            }

            QuickToolEntry entry = _hoverIndex >= 0 && _hoverIndex < _slots.Count
                ? _slots[_hoverIndex]
                : null;

            if (entry != null)
            {
                _hubToolText.Text = entry.DisplayName;
                _hubToolText.Foreground = TextAccent;
                _hubHintText.Text = "Click to run    Esc: close";
                return;
            }

            if (_hoverIndex >= 0)
            {
                _hubToolText.Text = "Empty slot";
                _hubToolText.Foreground = TextDisabled;
                _hubHintText.Text = "Press S to put a tool here";
                return;
            }

            _hubToolText.Text = "Point at a tool";
            _hubToolText.Foreground = TextAccent;
            _hubHintText.Text = "Click to run    S: customise    Esc: close";
        }

        private void OnWheelLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Choose(IndexAt(e.GetPosition(WheelCanvas)));
        }

        private void OnWheelRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CloseWithoutChoice();
        }

        private void OnWheelKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseWithoutChoice();
                return;
            }

            if (e.Key == Key.S)
            {
                e.Handled = true;
                OpenSettingsRequested = true;
                CloseWithoutChoice();
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                e.Handled = true;
                Choose(_hoverIndex);
                return;
            }

            int slot = SlotFromKey(e.Key);
            if (slot >= 0)
            {
                e.Handled = true;
                Choose(slot);
            }
        }

        private static int SlotFromKey(Key key)
        {
            if (key >= Key.D1 && key <= Key.D9)
            {
                return key - Key.D1;
            }

            if (key >= Key.NumPad1 && key <= Key.NumPad9)
            {
                return key - Key.NumPad1;
            }

            return -1;
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= _slots.Count)
            {
                // Aimed at the hub or outside the ring - same as a game, that means "never mind".
                CloseWithoutChoice();
                return;
            }

            QuickToolEntry entry = _slots[index];
            if (entry == null)
            {
                // An empty slot is not a mistake worth a popup - just show what to do and stay open.
                SetHover(index);
                return;
            }

            if (_choiceMade)
            {
                return;
            }

            _choiceMade = true;
            SelectedEntry = entry;
            SetDialogResultSafely(true);
        }

        private void CloseWithoutChoice()
        {
            if (_choiceMade)
            {
                return;
            }

            _choiceMade = true;
            SelectedEntry = null;
            SetDialogResultSafely(false);
        }

        /// <summary>
        /// Setting DialogResult on a window that has already closed throws, and the wheel can be
        /// closed from three places at once (choice, Esc, losing focus). One guarded writer.
        /// </summary>
        private void SetDialogResultSafely(bool result)
        {
            try
            {
                DialogResult = result;
            }
            catch (InvalidOperationException)
            {
                try
                {
                    Close();
                }
                catch (Exception)
                {
                }
            }
        }

        private void OnWheelDeactivated(object sender, EventArgs e)
        {
            // Anything that takes focus away (Alt+Tab, a click that reaches Revit) closes the wheel
            // rather than leaving a floating ring behind.
            if (_autoCloseArmed && !_choiceMade)
            {
                CloseWithoutChoice();
            }
        }

        #endregion

        private static Brush MakeBrush(byte alpha, byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }

        /// <summary>Minimal user32 interop - only to find out where the mouse pointer is.</summary>
        internal static class QuickMenuNative
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct NativePoint
            {
                public int X;
                public int Y;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetCursorPos(out NativePoint point);
        }
    }
}
