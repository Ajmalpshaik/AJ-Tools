#region Metadata
/*
 * Tool Name     : AJ Tools - Shared UI
 * File Name     : WindowMotionHelper.cs
 * Purpose       : One shared entrance animation for AJ Tools working dialogs, so every window opens
 *                 with the same motion instead of snapping into place.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-08-05
 * Last Updated  : 2026-08-05
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF)
 *
 * Dependencies  : WPF (PresentationFramework)
 *
 * Input         : The owning Window. Call once, after InitializeComponent().
 * Output        : A fade + short rise when the window is first shown. No layout or behaviour change.
 *
 * Notes         :
 * - TIMING IS DELIBERATELY SHORT. This is the "working dialog" tier: a settings window opened many
 *   times a day must feel instant. The staged ~750ms reveal on AboutWindow is the "showcase" tier and
 *   is intentionally NOT reused here - copying it onto a everyday dialog turns polish into waiting.
 * - Entrance easing decelerates (CubicEase EaseOut): fast start, soft landing, so the window is
 *   readable well before it finishes settling.
 * - Animates the window's ROOT CONTENT element, never Window.Opacity: Window.Opacity only has a visual
 *   effect when AllowsTransparency="True", which most AJ Tools windows do not set. Animating the content
 *   works on every window regardless of chrome style.
 * - Skips any window whose root already uses a RenderTransform, so a window doing its own transform
 *   work is never fought over.
 * - Purely cosmetic: every failure path restores the window to its normal, fully visible state.
 *
 * EXIT MOTION - READ THIS BEFORE TOUCHING AttachStandardExit:
 * An exit animation cannot just play and then close. It has to CANCEL the window's own Closing, animate,
 * and re-issue the close. That has two traps, both of which are handled below and both of which were
 * measured on real WPF dialogs (2026-08-05), not assumed:
 *
 * 1. WPF THROWS DialogResult AWAY WHEN A CLOSE IS CANCELLED. Measured: a dialog that sets
 *    DialogResult = true and then has its Closing cancelled returns FALSE from ShowDialog() once it
 *    finally closes. Every AJ Tools command does `if (window.ShowDialog() == true) { ...do the work... }`,
 *    so the naive version would make every Run button silently behave like Cancel - the tool would open,
 *    close, and do nothing. The fix is to CAPTURE window.DialogResult before cancelling and RESTORE it
 *    afterwards; restoring it re-issues the close by itself. Verified to match the no-animation control
 *    exactly for Run(true), Cancel(false), plain Close(), and the double-close case below.
 *
 * 2. A Button that is BOTH Click="Handler" (handler closes) AND IsCancel="True" raises Closing TWICE per
 *    click, because WPF's cancel logic then sets DialogResult = false, whose setter calls Close() again.
 *    A single "already handled" flag lets the second pass through and kills the window mid-animation.
 *    Hence THREE pieces of state: IsExitPlaying (a close is in flight - keep cancelling), IsReadyToClose
 *    (our own completion callback asked for the real close - let it through) and IsFinished (the close
 *    is issued once and only once). GameKeySettingsWindow's Cancel button is exactly this shape.
 *
 * A third hazard has no clever fix, only a backstop: if the animation never completes, the window can
 * never close, and a dialog the user cannot dismiss is far worse than no animation. So a DispatcherTimer
 * is armed BEFORE the animation starts and forces the close regardless. Whichever fires first wins.
 *
 * Windows with their own Closing logic will run it once per pass (2-3 times). Audited 2026-08-05: only
 * PipeSizingWindow has any, and it is a full-overwrite SaveState() - idempotent, so repeats are harmless.
 * Check this again before attaching the exit to any NEW window whose Closing does something with a side
 * effect (appending to a log, prompting, counting).
 *
 * Changelog     :
 * v1.1.0 (2026-08-05) - Added AttachStandardExit: content fades out over 150ms while sinking 6px,
 *                       CubicEase EaseIn, so closing accelerates away where opening decelerates in.
 *                       Shorter than the entrance on purpose - users care more about what appears than
 *                       what leaves. Carries the DialogResult capture/restore and the double-Closing
 *                       guard described above, plus the timer backstop.
 * v1.0.0 (2026-08-05) - Initial release. Rolled out across the AJ Tools dialogs so window entrances are
 *                       consistent; AboutWindow keeps its own showcase motion and Game Mode's HUD is
 *                       excluded (real-time overlay with its own animation).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AJTools.Utils
{
    /// <summary>
    /// Shared entrance motion for AJ Tools working dialogs.
    /// </summary>
    internal static class WindowMotionHelper
    {
        /// <summary>Opacity ramp. Short enough that the window reads as instant.</summary>
        private const double FadeMilliseconds = 220.0;

        /// <summary>Travel ramp. Slightly longer than the fade so the window settles after it is readable.</summary>
        private const double RiseMilliseconds = 280.0;

        /// <summary>How far below its resting place the content starts, in device-independent pixels.</summary>
        private const double RiseDistance = 12.0;

        /// <summary>Exit ramp. Shorter than the entrance - users care about what appears, not what leaves.</summary>
        private const double ExitMilliseconds = 150.0;

        /// <summary>How far the content sinks on the way out, in device-independent pixels.</summary>
        private const double ExitSinkDistance = 6.0;

        /// <summary>
        /// Extra time the backstop timer waits past the animation before forcing the close. A dialog that
        /// cannot be dismissed is far worse than a missing animation, so the close never depends solely
        /// on an animation completing.
        /// </summary>
        private const double ExitSafetyMarginMilliseconds = 250.0;

        /// <summary>
        /// Gives the window a fade-and-rise entrance the first time it is shown.
        /// Call once from the constructor, after InitializeComponent(). Safe to call on any window;
        /// it quietly does nothing if the window has no content or already drives its own transform.
        /// </summary>
        internal static void AttachStandardEntrance(Window window)
        {
            if (window == null)
                return;

            try
            {
                var root = window.Content as FrameworkElement;
                if (root == null)
                    return;

                // Never fight a window that is already transforming its own root.
                if (root.RenderTransform != null && !root.RenderTransform.Value.IsIdentity)
                    return;

                var translate = new TranslateTransform(0.0, RiseDistance);
                root.RenderTransform = translate;
                root.Opacity = 0.0;

                // Start on Loaded (fires before the first paint, so there is no flash of the
                // finished state) and unsubscribe immediately - this is a one-shot entrance.
                RoutedEventHandler onLoaded = null;
                onLoaded = delegate
                {
                    window.Loaded -= onLoaded;
                    PlayEntrance(root, translate);
                };

                window.Loaded += onLoaded;
            }
            catch
            {
                // Motion is cosmetic - never let it stop a window from opening.
                RestoreToRest(window);
            }
        }

        private static void PlayEntrance(FrameworkElement root, TranslateTransform translate)
        {
            try
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(FadeMilliseconds))
                {
                    EasingFunction = ease
                };

                var rise = new DoubleAnimation(RiseDistance, 0.0, TimeSpan.FromMilliseconds(RiseMilliseconds))
                {
                    EasingFunction = ease
                };

                // Applied straight to the element and the transform: no Storyboard, so there is no
                // namescope to resolve and no frozen-resource problem to work around.
                root.BeginAnimation(UIElement.OpacityProperty, fade);
                translate.BeginAnimation(TranslateTransform.YProperty, rise);
            }
            catch
            {
                root.Opacity = 1.0;
                root.RenderTransform = null;
            }
        }

        /// <summary>
        /// Per-window state for the exit. Held in a closure rather than an attached property so two
        /// windows can never share it, and so nothing survives the window itself.
        /// </summary>
        private sealed class ExitState
        {
            /// <summary>A close is in flight: keep cancelling every further Closing.</summary>
            internal bool IsExitPlaying;

            /// <summary>Our own completion callback asked for the real close: let it through.</summary>
            internal bool IsReadyToClose;

            /// <summary>The real close has been issued once. Guards animation-vs-timer double firing.</summary>
            internal bool IsFinished;

            /// <summary>
            /// The dialog's result, captured BEFORE the close is cancelled. WPF discards DialogResult on a
            /// cancelled close, so without this every Run button would come back as Cancel.
            /// </summary>
            internal bool? SavedDialogResult;

            /// <summary>Backstop, in case the animation never completes.</summary>
            internal DispatcherTimer Timer;
        }

        /// <summary>
        /// Gives the window a short fade-and-sink as it closes. Call once from the constructor, after
        /// InitializeComponent(). The window's result, validation and close behaviour are unchanged -
        /// see the exit notes in this file's header before altering any of this.
        /// </summary>
        internal static void AttachStandardExit(Window window)
        {
            if (window == null)
                return;

            try
            {
                var state = new ExitState();
                window.Closing += delegate(object sender, CancelEventArgs e)
                {
                    HandleClosing(window, state, e);
                };
            }
            catch
            {
                // Motion is cosmetic - never let it stop a window from closing.
            }
        }

        private static void HandleClosing(Window window, ExitState state, CancelEventArgs e)
        {
            try
            {
                // The close we re-issued ourselves once the animation finished.
                if (state.IsReadyToClose)
                    return;

                // Another handler has already vetoed this close - a busy guard, an unsaved-changes
                // prompt, a validation refusal. Respect it and do nothing: without this the exit
                // animation would run and then force the window shut, overriding the veto. Window's
                // own handlers run first (they subscribe during InitializeComponent), so by the time
                // this runs their decision is already on the event args.
                if (e.Cancel)
                    return;

                // Any further Closing while the exit plays - most commonly the second one raised by a
                // button that is both Click= and IsCancel="True".
                if (state.IsExitPlaying)
                {
                    e.Cancel = true;
                    return;
                }

                var root = window.Content as FrameworkElement;
                if (root == null)
                {
                    // Nothing to animate. Close normally rather than inventing a reason to interfere.
                    state.IsReadyToClose = true;
                    return;
                }

                state.IsExitPlaying = true;

                // MUST be read before the cancel below: WPF throws DialogResult away when a close is
                // cancelled, so this is the only moment the real value is still available.
                try { state.SavedDialogResult = window.DialogResult; }
                catch { state.SavedDialogResult = null; }

                e.Cancel = true;
                PlayExit(window, root, state);
            }
            catch
            {
                // If anything at all goes wrong, let the window close the normal way immediately.
                e.Cancel = false;
                state.IsReadyToClose = true;
                state.IsFinished = true;
            }
        }

        private static void PlayExit(Window window, FrameworkElement root, ExitState state)
        {
            try
            {
                // Backstop armed FIRST, so the close never depends on the animation completing.
                var timer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(ExitMilliseconds + ExitSafetyMarginMilliseconds)
                };
                timer.Tick += delegate { FinishClose(window, state); };
                state.Timer = timer;
                timer.Start();

                // Exits accelerate away, where entrances decelerate in.
                var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

                var translate = root.RenderTransform as TranslateTransform;
                if (translate == null && (root.RenderTransform == null || root.RenderTransform.Value.IsIdentity))
                {
                    translate = new TranslateTransform();
                    root.RenderTransform = translate;
                }

                if (translate != null)
                {
                    var sink = new DoubleAnimation(translate.Y, translate.Y + ExitSinkDistance,
                                                   TimeSpan.FromMilliseconds(ExitMilliseconds))
                    {
                        EasingFunction = ease
                    };
                    translate.BeginAnimation(TranslateTransform.YProperty, sink);
                }

                var fade = new DoubleAnimation(root.Opacity, 0.0, TimeSpan.FromMilliseconds(ExitMilliseconds))
                {
                    EasingFunction = ease
                };
                fade.Completed += delegate { FinishClose(window, state); };
                root.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            catch
            {
                FinishClose(window, state);
            }
        }

        private static void FinishClose(Window window, ExitState state)
        {
            // Animation completion and the backstop timer both land here; only the first one counts.
            if (state.IsFinished)
                return;

            state.IsFinished = true;
            state.IsReadyToClose = true;

            try
            {
                if (state.Timer != null)
                {
                    state.Timer.Stop();
                    state.Timer = null;
                }
            }
            catch
            {
                // A timer that cannot be stopped is harmless - FinishClose is single-shot.
            }

            try
            {
                if (state.SavedDialogResult.HasValue)
                {
                    // Restoring the captured result re-issues the close by itself, and hands the command
                    // the same answer it would have got with no animation at all.
                    window.DialogResult = state.SavedDialogResult;
                }
                else
                {
                    window.Close();
                }
            }
            catch
            {
                // Modeless windows throw when DialogResult is set. Close plainly instead.
                try { window.Close(); }
                catch { }
            }
        }

        private static void RestoreToRest(Window window)
        {
            try
            {
                var root = window.Content as FrameworkElement;
                if (root == null)
                    return;

                root.Opacity = 1.0;
                root.RenderTransform = null;
            }
            catch
            {
                // Nothing further to do - the window is already in whatever state it can reach.
            }
        }
    }
}
