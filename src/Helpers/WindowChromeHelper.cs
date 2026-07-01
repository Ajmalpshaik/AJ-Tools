#region Metadata
/*
 * Tool Name     : AJ Tools - Shared UI
 * File Name     : WindowChromeHelper.cs
 * Purpose       : Shared custom-title-bar behaviour (drag, minimize, maximize) for borderless AJ Tools windows.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-06-28
 * Last Updated  : 2026-06-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF)
 *
 * Dependencies  : WPF (PresentationFramework)
 *
 * Input         : The owning Window and (for maximize) its outer chrome Border.
 * Output        : Window drag / minimize / maximize-restore behaviour.
 *
 * Notes         :
 * - For WindowStyle=None + AllowsTransparency windows. Keeps all custom-chrome windows behaving identically.
 * - Maximize is margin-aware: the outer border's shadow margin is removed while maximized so the
 *   content fills cleanly, and restored on return to normal.
 * - DragMove is only invoked when the window is in the Normal state (DragMove throws while maximized).
 *
 * Changelog     :
 * v1.0.0 (2026-06-28) - Initial release. Extracted from the View Crop Options window so all
 *                       AJ Tools custom-chrome windows share one implementation.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System.Windows;
using System.Windows.Input;

namespace AJTools.Utils
{
    /// <summary>
    /// Shared behaviour for borderless (WindowStyle=None) AJ Tools windows that draw their own title bar.
    /// </summary>
    internal static class WindowChromeHelper
    {
        private const double ShadowMargin = 10.0;

        /// <summary>
        /// Drags the window when the user presses the left mouse button on the custom title bar.
        /// Ignored while maximized (DragMove is invalid in that state).
        /// </summary>
        internal static void HandleTitleBarDrag(Window window, MouseButtonEventArgs e)
        {
            if (window == null || e == null)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (window.WindowState == WindowState.Maximized)
                return;

            try
            {
                window.DragMove();
            }
            catch
            {
                // DragMove can throw if the mouse capture state changes mid-drag; ignore.
            }
        }

        /// <summary>Minimizes the window.</summary>
        internal static void Minimize(Window window)
        {
            if (window != null)
                window.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Toggles between maximized and normal, adjusting the outer border's shadow margin so the
        /// content fills the screen cleanly while maximized.
        /// </summary>
        internal static void ToggleMaximize(Window window, FrameworkElement rootBorder)
        {
            if (window == null)
                return;

            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
                if (rootBorder != null)
                    rootBorder.Margin = new Thickness(ShadowMargin);
            }
            else
            {
                if (rootBorder != null)
                    rootBorder.Margin = new Thickness(0);

                window.WindowState = WindowState.Maximized;
            }
        }
    }
}
