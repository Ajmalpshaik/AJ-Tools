#region Metadata
/*
 * Tool Name     : AJ AI Temporary Activity Banner
 * File Name     : AiTaskWarningBarService.cs
 * Purpose       : Shows a non-blocking, pyRevit WarningBar-style banner only while an AutoDebugger
 *                 request is executing against Revit.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.4.0
 *
 * Created Date  : 2026-07-12
 * Last Updated  : 2026-07-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in / WPF
 *
 * Dependencies  : WPF, Windows Forms, captured Revit UI dispatcher, Revit host process
 *
 * Input         : BeginTask / EndTask notifications from McpBridgeService.
 * Output        : Temporary top-of-screen activity banner; no model or sheet changes.
 *
 * Notes         :
 * - Uses the Revit UI dispatcher captured at bridge construction, because Revit does not populate
 *   System.Windows.Application.Current.
 * - The banner has no taskbar entry and remains visible long enough to paint before it closes.
 *
 * Changelog     :
 * v1.4.0 (2026-07-16) - The progress bar was a fixed-width static Border, never animated — fixed with
 *                       an indeterminate sweeping-highlight Storyboard (start on show, stop on close)
 *                       since BeginTask/EndTask carry no real percentage to display honestly.
 * v1.3.0 (2026-07-12) - Restyled as an AJ Tools dark activity card with blue progress accent.
 * v1.2.0 (2026-07-12) - Use the captured Revit UI dispatcher; Application.Current is null in Revit.
 * v1.1.0 (2026-07-12) - Keep the banner visible briefly so fast bridge tasks do not close before paint.
 * v1.0.0 (2026-07-12) - Initial temporary AI activity banner.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using FormsScreen = System.Windows.Forms.Screen;

namespace AJTools.GeminiShell.Services
{
    /// <summary>
    /// Displays a short-lived, non-modal warning bar while AI bridge work is active.
    /// </summary>
    internal sealed class AiTaskWarningBarService
    {
        private const double BannerWidth = 468;
        private const double BannerHeight = 76;
        private static readonly TimeSpan MinimumVisibleDuration = TimeSpan.FromMilliseconds(800);

        private const double ProgressHighlightWidth = 120;
        private static readonly TimeSpan ProgressSweepDuration = TimeSpan.FromSeconds(1.1);

        private readonly Dispatcher _revitDispatcher;
        private Window _banner;
        private TextBlock _message;
        private int _activeTaskCount;
        private DateTime _bannerShownUtc;
        private DispatcherTimer _closeTimer;
        private Storyboard _progressStoryboard;

        public AiTaskWarningBarService(Dispatcher revitDispatcher)
        {
            _revitDispatcher = revitDispatcher;
        }

        /// <summary>Shows the banner, or increments the active-task count if it is already visible.</summary>
        public void BeginTask()
        {
            RunOnRevitUi(() =>
            {
                _activeTaskCount++;
                StopPendingClose();
                EnsureBanner();
                if (_message != null)
                    _message.Text = "AJ AI is working";
            });
        }

        /// <summary>Closes the banner once the last active bridge task has completed.</summary>
        public void EndTask()
        {
            RunOnRevitUi(() =>
            {
                if (_activeTaskCount > 0)
                    _activeTaskCount--;

                if (_activeTaskCount != 0 || _banner == null)
                    return;

                CloseAfterMinimumVisibleDuration();
            });
        }

        private void CloseAfterMinimumVisibleDuration()
        {
            TimeSpan remaining = MinimumVisibleDuration - (DateTime.UtcNow - _bannerShownUtc);
            if (remaining <= TimeSpan.Zero)
            {
                CloseBanner();
                return;
            }

            if (_closeTimer == null)
            {
                _closeTimer = new DispatcherTimer(DispatcherPriority.Background);
                _closeTimer.Tick += OnCloseTimerTick;
            }

            _closeTimer.Stop();
            _closeTimer.Interval = remaining;
            _closeTimer.Start();
        }

        private void OnCloseTimerTick(object sender, EventArgs args)
        {
            StopPendingClose();
            if (_activeTaskCount == 0)
                CloseBanner();
        }

        private void StopPendingClose()
        {
            if (_closeTimer != null)
                _closeTimer.Stop();
        }

        private void CloseBanner()
        {
            StopPendingClose();
            if (_banner != null)
                _banner.Close();
        }

        private void RunOnRevitUi(Action action)
        {
            Dispatcher dispatcher = _revitDispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return;

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
        }

        private void EnsureBanner()
        {
            if (_banner != null && _banner.IsVisible)
                return;

            _message = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(242, 247, 250)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Text = "AJ AI is working"
            };

            var subtitle = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(168, 180, 190)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0),
                Text = "Processing your Revit task..."
            };

            var icon = new Border
            {
                Width = 40,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                CornerRadius = new CornerRadius(20),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "AI"
                }
            };

            var copy = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(13, 0, 0, 0)
            };
            copy.Children.Add(_message);
            copy.Children.Add(subtitle);

            var status = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            status.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromRgb(129, 199, 132)),
                VerticalAlignment = VerticalAlignment.Center
            });
            status.Children.Add(new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(168, 180, 190)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(6, 0, 0, 0),
                Text = "LIVE"
            });

            var content = new Grid
            {
                Margin = new Thickness(18, 0, 18, 0)
            };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(icon);
            Grid.SetColumn(copy, 1);
            content.Children.Add(copy);
            Grid.SetColumn(status, 2);
            content.Children.Add(status);

            var shell = new Grid();
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });
            shell.Children.Add(content);

            // BeginTask/EndTask carry no real percentage (the banner wraps any bridge call, ping or
            // script, with no progress data), so an indeterminate sweeping highlight is the honest
            // signal here rather than a fake percentage — same idea as a standard WPF indeterminate
            // ProgressBar, built by hand since this bar is otherwise plain WPF shapes.
            var progressTrack = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 65, 96)),
                CornerRadius = new CornerRadius(0, 0, 12, 12),
                ClipToBounds = true
            };
            var progressHighlight = new Border
            {
                Width = ProgressHighlightWidth,
                Height = 3,
                Background = new SolidColorBrush(Color.FromRgb(0, 200, 255)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            var progressCanvas = new Canvas { Height = 3 };
            progressCanvas.Children.Add(progressHighlight);
            progressTrack.Child = progressCanvas;
            Grid.SetRow(progressTrack, 1);
            shell.Children.Add(progressTrack);

            var sweep = new DoubleAnimation
            {
                From = -ProgressHighlightWidth,
                To = BannerWidth,
                Duration = new Duration(ProgressSweepDuration),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(sweep, progressHighlight);
            Storyboard.SetTargetProperty(sweep, new PropertyPath("(Canvas.Left)"));
            _progressStoryboard = new Storyboard();
            _progressStoryboard.Children.Add(sweep);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(43, 43, 43)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Child = shell,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 4,
                    Direction = 270,
                    Opacity = 0.45,
                    Color = Colors.Black
                }
            };

            _banner = new Window
            {
                Width = BannerWidth,
                Height = BannerHeight,
                Content = border,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                AllowsTransparency = false,
                Background = Brushes.Transparent,
                FontFamily = new FontFamily("Segoe UI"),
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            IntPtr revitWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
            if (revitWindowHandle != IntPtr.Zero)
            {
                new WindowInteropHelper(_banner).Owner = revitWindowHandle;
                var workArea = FormsScreen.FromHandle(revitWindowHandle).WorkingArea;
                _banner.Left = workArea.Left + (workArea.Width - BannerWidth) / 2.0;
                _banner.Top = workArea.Top + 12;
            }
            else
            {
                _banner.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            _banner.Closed += (sender, args) =>
            {
                StopPendingClose();
                _progressStoryboard?.Stop();
                _progressStoryboard = null;
                _banner = null;
                _message = null;
                _bannerShownUtc = DateTime.MinValue;
            };
            _banner.Show();
            _progressStoryboard.Begin();
            _bannerShownUtc = DateTime.UtcNow;
        }
    }
}
