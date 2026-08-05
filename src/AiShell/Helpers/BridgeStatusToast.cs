#region Metadata
/*
 * Tool Name     : AJ AI Bridge Status Toast
 * File Name     : BridgeStatusToast.cs
 * Purpose       : Small non-blocking confirmation shown after the standalone "AJ AI Bridge" ribbon
 *                 button connects or disconnects - a plain Revit PushButton has no persistent on/off
 *                 visual state, so this is the only feedback the click actually changed something.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-18
 * Last Updated  : 2026-07-18
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in / WPF
 *
 * Dependencies  : WPF, Revit host process
 *
 * Input         : A one-line status message and a connected/disconnected flag (dot color).
 * Output        : A brief top-of-screen toast that auto-closes; no model or sheet changes.
 *
 * Notes         :
 * - Called directly from ToggleAiBridgeCommand.Execute(), which already runs on Revit's UI thread -
 *   no dispatcher marshaling needed (unlike AiTaskWarningBarService, which is driven from the pipe's
 *   background listener thread and must marshal onto a captured UI dispatcher).
 * - Deliberately not reusing AiTaskWarningBarService: that class's BeginTask/EndTask pair tracks
 *   concurrent AI requests with an indeterminate progress sweep; this is a single, static, glanceable
 *   confirmation with no progress concept, from a different call site (a ribbon click, not the bridge
 *   itself). Same visual language (dark rounded card, drop shadow), simpler lifecycle.
 *
 * Changelog     :
 * v1.0.0 (2026-07-18) - Initial release, split out of moving the AJ AI Bridge connect/disconnect
 *                       control from the AJ AI chat panel (instant WPF-bound feedback there) to its
 *                       own ribbon button (no built-in state feedback), per Ajmal's request.
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
using System.Windows.Threading;
using FormsScreen = System.Windows.Forms.Screen;

namespace AJTools.AiShell.Helpers
{
    /// <summary>Shows a brief, non-blocking status toast (auto-closes) after an AJ AI Bridge toggle.</summary>
    internal static class BridgeStatusToast
    {
        private const double ToastWidth = 320;
        private const double ToastHeight = 56;
        private static readonly TimeSpan VisibleDuration = TimeSpan.FromSeconds(1.8);

        /// <summary>Fade in. Short, so the message is readable almost immediately.</summary>
        private const double FadeInMilliseconds = 180.0;

        /// <summary>Fade out. Exits accelerate away, matching the rest of the suite.</summary>
        private const double FadeOutMilliseconds = 220.0;

        /// <summary>Backstop margin past the fade. A toast that never closes would sit over Revit forever.</summary>
        private const double FadeSafetyMarginMilliseconds = 250.0;

        public static void Show(string message, bool connected)
        {
            var text = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Text = message
            };

            var dot = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(connected
                    ? Color.FromRgb(15, 157, 88)     // green - connected
                    : Color.FromRgb(150, 150, 150))  // grey - disconnected
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 0, 16, 0)
            };
            content.Children.Add(dot);
            content.Children.Add(text);

            var contentHost = new Grid();
            contentHost.Children.Add(content);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(43, 43, 43)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = contentHost,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 3,
                    Direction = 270,
                    Opacity = 0.4,
                    Color = Colors.Black
                }
            };

            var toast = new Window
            {
                Width = ToastWidth,
                Height = ToastHeight,
                Content = border,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                FontFamily = new FontFamily("Segoe UI"),
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            IntPtr revitWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
            if (revitWindowHandle != IntPtr.Zero)
            {
                new WindowInteropHelper(toast).Owner = revitWindowHandle;
                var workArea = FormsScreen.FromHandle(revitWindowHandle).WorkingArea;
                toast.Left = workArea.Left + (workArea.Width - ToastWidth) / 2.0;
                toast.Top = workArea.Top + 12;
            }
            else
            {
                toast.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // This toast sets AllowsTransparency, so Window.Opacity genuinely works here - unlike most
            // AJ Tools windows, where WindowMotionHelper has to animate the root content instead.
            toast.Opacity = 0.0;
            toast.Show();

            toast.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(FadeInMilliseconds))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            var timer = new DispatcherTimer { Interval = VisibleDuration };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                FadeOutAndClose(toast);
            };
            timer.Start();
        }

        /// <summary>
        /// Fades the toast away and then closes it. Nothing else owns this window's lifetime and the
        /// user cannot click it, so there is no DialogResult or veto to preserve - only the guarantee
        /// that it definitely goes away.
        /// </summary>
        private static void FadeOutAndClose(Window toast)
        {
            bool closed = false;
            Action close = delegate
            {
                if (closed)
                {
                    return;
                }

                closed = true;
                try { toast.Close(); }
                catch { /* Already gone - nothing to do. */ }
            };

            try
            {
                // Backstop armed BEFORE the animation: the close must never depend on a fade completing.
                var safety = new DispatcherTimer(DispatcherPriority.Normal, toast.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(FadeOutMilliseconds + FadeSafetyMarginMilliseconds)
                };
                safety.Tick += delegate
                {
                    safety.Stop();
                    close();
                };
                safety.Start();

                var fade = new DoubleAnimation(toast.Opacity, 0.0, TimeSpan.FromMilliseconds(FadeOutMilliseconds))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                fade.Completed += delegate
                {
                    safety.Stop();
                    close();
                };

                toast.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            catch
            {
                close();
            }
        }
    }
}
