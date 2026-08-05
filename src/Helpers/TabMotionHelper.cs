#region Metadata
/*
 * Tool Name     : AJ Tools - Shared UI
 * File Name     : TabMotionHelper.cs
 * Purpose       : One shared transition for switching tabs, so a tab change reads as the panel arriving
 *                 rather than the window hard-cutting to different content.
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
 * Input         : The owning Window. Call once, after InitializeComponent().
 * Output        : Every TabControl in the window fades and rises its content on a tab change.
 *                 No layout, no selection behaviour, no result changes.
 *
 * Notes         :
 * - Companion to WindowMotionHelper and deliberately shorter than it: a window opens once, but a tab
 *   gets clicked over and over in one sitting, so this has to stay well under the entrance timing.
 *   180ms fade / 220ms rise over 8px, same CubicEase EaseOut as everything else in the suite.
 * - THE TRAP THIS EXISTS TO AVOID: Selector.SelectionChanged is a ROUTED event, so a ComboBox or ListBox
 *   sitting INSIDE a tab bubbles its own selection change up to the TabControl. Hooking the event
 *   naively replays the whole tab transition every time the user picks a value from a dropdown inside
 *   the tab. The handler below only reacts when the TabControl itself is the original source.
 * - Attaches by walking the visual tree on Loaded rather than by x:Name, so no XAML has to change and a
 *   window with two TabControls gets both. Walking on Loaded is what makes Template.FindName reliable -
 *   the template is guaranteed applied by then.
 * - PART_SelectedContentHost is the content host in both the default WPF TabControl template and in
 *   GraphicsOverrideWindow's own custom one, which is why one helper covers every tabbed window here.
 *   If a future window templates its TabControl differently, the helper simply finds nothing and does
 *   nothing - it never throws and never blocks the tab from switching.
 * - The handler never sets e.Handled, so any existing SelectionChanged logic in a window keeps running
 *   exactly as before.
 * - Purely cosmetic: every failure path leaves the content fully visible.
 *
 * Changelog     :
 * v1.0.0 (2026-08-05) - Initial release. Wired into the five tabbed windows: Colorize, Duct Standards
 *                       Manager, Filter Pro, Graphics Settings Manager, Location Data Assigner.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AJTools.Utils
{
    /// <summary>
    /// Shared tab-change transition for AJ Tools windows.
    /// </summary>
    internal static class TabMotionHelper
    {
        /// <summary>Opacity ramp for the incoming panel.</summary>
        private const double FadeMilliseconds = 180.0;

        /// <summary>Travel ramp. Slightly longer than the fade so the panel settles after it is readable.</summary>
        private const double RiseMilliseconds = 220.0;

        /// <summary>How far below its resting place the panel starts, in device-independent pixels.</summary>
        private const double RiseDistance = 8.0;

        /// <summary>The template part holding the selected tab's content.</summary>
        private const string ContentHostPartName = "PART_SelectedContentHost";

        /// <summary>
        /// Gives every TabControl in the window a fade-and-rise when the user switches tabs.
        /// Call once from the constructor, after InitializeComponent(). Safe on any window - a window
        /// with no tabs simply gets nothing.
        /// </summary>
        internal static void AttachTabTransitions(Window window)
        {
            if (window == null)
                return;

            try
            {
                RoutedEventHandler onLoaded = null;
                onLoaded = delegate
                {
                    window.Loaded -= onLoaded;
                    AttachToTabControls(window);
                };

                window.Loaded += onLoaded;
            }
            catch
            {
                // Motion is cosmetic - never let it stop a window from opening.
            }
        }

        private static void AttachToTabControls(DependencyObject root)
        {
            try
            {
                int count = VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < count; i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(root, i);

                    var tabControl = child as TabControl;
                    if (tabControl != null)
                        tabControl.SelectionChanged += OnTabSelectionChanged;

                    AttachToTabControls(child);
                }
            }
            catch
            {
                // A partially-walked tree is fine: whatever was reached keeps its transition.
            }
        }

        private static void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var tabControl = sender as TabControl;
                if (tabControl == null)
                    return;

                // Selector.SelectionChanged is routed. A ComboBox/ListBox inside the tab raises it too,
                // and animating on those would replay the transition on every dropdown pick.
                if (!ReferenceEquals(e.OriginalSource, tabControl))
                    return;

                if (tabControl.Template == null)
                    return;

                var host = tabControl.Template.FindName(ContentHostPartName, tabControl) as FrameworkElement;
                if (host == null)
                    return;

                PlayTransition(host);
            }
            catch
            {
                // Never let a cosmetic transition interfere with the tab actually changing.
            }
        }

        private static void PlayTransition(FrameworkElement host)
        {
            try
            {
                // Reuse our own transform if this is a repeat switch; never fight one the window owns.
                var translate = host.RenderTransform as TranslateTransform;
                if (translate == null)
                {
                    if (host.RenderTransform != null && !host.RenderTransform.Value.IsIdentity)
                        return;

                    translate = new TranslateTransform();
                    host.RenderTransform = translate;
                }

                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(FadeMilliseconds))
                {
                    EasingFunction = ease
                };

                var rise = new DoubleAnimation(RiseDistance, 0.0, TimeSpan.FromMilliseconds(RiseMilliseconds))
                {
                    EasingFunction = ease
                };

                host.BeginAnimation(UIElement.OpacityProperty, fade);
                translate.BeginAnimation(TranslateTransform.YProperty, rise);
            }
            catch
            {
                host.Opacity = 1.0;
                host.RenderTransform = null;
            }
        }
    }
}
