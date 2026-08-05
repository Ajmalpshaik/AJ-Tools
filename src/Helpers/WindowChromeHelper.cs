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
 * - Maximize is also corner-aware: a rounded shell must square off while maximized or the desktop
 *   shows through all four corners. Each border's design radius is remembered per element, so a
 *   window with a different radius keeps its own value.
 * - DragMove is only invoked when the window is in the Normal state (DragMove throws while maximized).
 *
 * Changelog     :
 * v1.0.0 (2026-06-28) - Initial release. Extracted from the View Crop Options window so all
 *                       AJ Tools custom-chrome windows share one implementation.
 * v1.1.0 (2026-08-05) - Added ApplyStateChrome: flattens the shell CornerRadius while maximized
 *                       (rounded corners previously showed the desktop through) and folds the
 *                       existing shadow-margin handling into the same call. Behaviour of drag /
 *                       minimize / maximize itself unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System.Windows;
using System.Windows.Controls;
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
        /// Remembers the CornerRadius the XAML designed for a shell border, so it can be restored after
        /// a maximize squares it off. Attached per element rather than held in a static dictionary, so
        /// it is collected with the window and each window keeps its own radius.
        /// </summary>
        private static readonly DependencyProperty DesignCornerRadiusProperty =
            DependencyProperty.RegisterAttached(
                "DesignCornerRadius",
                typeof(object),
                typeof(WindowChromeHelper),
                new PropertyMetadata(null));

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

            // Also square/round the corners. Called here as well as from the window's OnStateChanged
            // override so the button path works even for a window that hasn't added the override.
            ApplyStateChrome(window, rootBorder);
        }

        /// <summary>
        /// Brings the shell border in line with the window's current state: no shadow margin and square
        /// corners while maximized, the designed margin and radius otherwise.
        ///
        /// A rounded shell MUST square off while maximized - the window fills the screen but the
        /// CornerRadius keeps cutting the corners away, so the desktop shows through all four.
        /// Call this from the window's OnStateChanged override, not just from the maximize button:
        /// Win+Up and snapping to the top edge maximize the window without ever going through
        /// ToggleMaximize.
        /// </summary>
        internal static void ApplyStateChrome(Window window, FrameworkElement rootBorder)
        {
            if (window == null || rootBorder == null)
                return;

            bool isMaximized = window.WindowState == WindowState.Maximized;

            rootBorder.Margin = isMaximized ? new Thickness(0) : new Thickness(ShadowMargin);

            var border = rootBorder as Border;
            if (border == null)
                return;

            // Capture the designed radius the first time we touch this border. Safe even if the first
            // call already reports Maximized: we read the radius before overwriting it, and nothing
            // else in the project writes CornerRadius on a shell border.
            object stored = border.GetValue(DesignCornerRadiusProperty);
            if (!(stored is CornerRadius))
            {
                stored = border.CornerRadius;
                border.SetValue(DesignCornerRadiusProperty, stored);
            }

            border.CornerRadius = isMaximized ? new CornerRadius(0) : (CornerRadius)stored;
        }
    }
}
