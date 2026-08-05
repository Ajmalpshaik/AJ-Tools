#region Metadata
/*
 * Tool Name     : AJ Tools - Shared UI
 * File Name     : ProgressReporter.cs
 * Purpose       : Lets a long-running tool show real progress instead of freezing behind an hourglass,
 *                 without moving any Revit work off Revit's UI thread.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
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
 * Input         : A ProgressBar and/or a status TextBlock from the window. Both optional.
 * Output        : A bar and a line of text that actually move while the tool works.
 *
 * Notes         :
 * - WHY THIS IS NOT A BACKGROUND THREAD. The Revit API must be called on Revit's own UI thread. Pushing
 *   a scan or a delete onto a worker thread to "keep the UI responsive" is not an option here - it
 *   throws, or corrupts the document. So the work stays exactly where it was and this only makes the
 *   window repaint part-way through it.
 * - WHY A PLAIN PROPERTY UPDATE IS NOT ENOUGH. A loop running on the UI thread starves WPF's render
 *   pass, so setting ProgressBar.Value inside it changes the number but paints nothing - the window
 *   still looks frozen. The fix, already established in this project, is to pump the dispatcher at
 *   DispatcherPriority.Render after setting the values, which forces the repaint to happen now.
 * - WHY Render PRIORITY SPECIFICALLY. Input priority is LOWER than Render, so a Render-priority pump
 *   repaints without processing mouse or keyboard input. That matters: the user cannot click a button
 *   half way through a delete loop and re-enter it. A DoEvents-style pump at Input priority WOULD allow
 *   that, and is exactly the re-entrancy bug this avoids.
 * - THROTTLED ON PURPOSE. Repainting on every one of several thousand items would cost more than the
 *   work itself, so a repaint happens at most every 33ms (about 30 a second, which reads as smooth).
 *   The first and last items always paint, so the bar visibly starts at 0 and finishes full.
 * - Purely a display aid: it never changes what the tool does, how many elements it touches, or what it
 *   returns. Every failure path is swallowed - a progress bar must never be able to break a purge.
 *
 * Changelog     :
 * v1.0.0 (2026-08-05) - Initial release. First used by Purge Unused Elements, whose scan probes a
 *                       trial delete for every candidate and could sit silent for several seconds.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AJTools.Utils
{
    /// <summary>
    /// Drives a progress bar and status line from a loop running on Revit's UI thread.
    /// </summary>
    internal sealed class ProgressReporter
    {
        /// <summary>Minimum gap between repaints. ~30 a second reads as smooth without costing real time.</summary>
        private const long MinimumRepaintIntervalMs = 33L;

        private readonly ProgressBar _bar;
        private readonly TextBlock _status;
        private readonly Dispatcher _dispatcher;
        private readonly Stopwatch _clock = new Stopwatch();

        private long _lastRepaintMs = -1L;

        /// <summary>
        /// Either argument may be null - a window with only a status line, or only a bar, still works.
        /// </summary>
        internal ProgressReporter(ProgressBar bar, TextBlock status)
        {
            _bar = bar;
            _status = status;

            // Never System.Windows.Application.Current - it is always null inside Revit. Take the
            // dispatcher from a control that was created on Revit's UI thread.
            if (bar != null)
                _dispatcher = bar.Dispatcher;
            else if (status != null)
                _dispatcher = status.Dispatcher;
            else
                _dispatcher = Dispatcher.CurrentDispatcher;

            _clock.Start();
        }

        /// <summary>
        /// Shows the progress row and resets it. Call once before the loop starts.
        /// </summary>
        internal void Begin(int total, string message)
        {
            try
            {
                _lastRepaintMs = -1L;

                if (_bar != null)
                {
                    _bar.Visibility = System.Windows.Visibility.Visible;
                    _bar.Minimum = 0;
                    _bar.Maximum = Math.Max(1, total);
                    _bar.Value = 0;
                }

                if (_status != null)
                {
                    _status.Visibility = System.Windows.Visibility.Visible;
                    _status.Text = message ?? string.Empty;
                }

                Repaint();
            }
            catch
            {
                // A progress bar must never be able to stop the real work.
            }
        }

        /// <summary>
        /// Call once per item. Cheap: it only repaints when enough time has passed, or on the last item.
        /// </summary>
        internal void Report(int done, int total, string message)
        {
            try
            {
                if (_bar != null)
                {
                    _bar.Maximum = Math.Max(1, total);
                    _bar.Value = Math.Min(done, _bar.Maximum);
                }

                if (_status != null && message != null)
                    _status.Text = message;

                long now = _clock.ElapsedMilliseconds;
                bool isLast = done >= total;

                if (!isLast && _lastRepaintMs >= 0 && (now - _lastRepaintMs) < MinimumRepaintIntervalMs)
                    return;

                _lastRepaintMs = now;
                Repaint();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Hides the progress row again. Call in a finally block so it runs even if the work throws.
        /// </summary>
        internal void End()
        {
            try
            {
                if (_bar != null)
                {
                    _bar.Value = 0;
                    _bar.Visibility = System.Windows.Visibility.Collapsed;
                }

                if (_status != null)
                {
                    _status.Text = string.Empty;
                    _status.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Forces WPF to paint now. An empty Invoke at Render priority is enough: the dispatcher must
        /// finish everything of higher-or-equal priority - which includes the render pass - before it
        /// returns. Input priority sits BELOW Render, so no clicks or keystrokes are processed here and
        /// the loop cannot be re-entered.
        /// </summary>
        private void Repaint()
        {
            try
            {
                if (_dispatcher == null)
                    return;

                _dispatcher.Invoke(new Action(delegate { }), DispatcherPriority.Render);
            }
            catch
            {
            }
        }
    }
}
