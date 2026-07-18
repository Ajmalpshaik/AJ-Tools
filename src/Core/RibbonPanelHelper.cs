using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.App
{
    /// <summary>
    /// Shared ribbon-panel lookup and icon-application helpers used by both RibbonManager (AJ Tools
    /// tab) and AnnotationRibbonManager (AJ Annotation tab).
    /// </summary>
    internal static class RibbonPanelHelper
    {
        /// <summary>
        /// Finds an existing panel by name on the given tab or creates it if missing.
        /// </summary>
        public static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string tabName, string panelName)
        {
            foreach (RibbonPanel panel in app.GetRibbonPanels(tabName))
            {
                if (panel.Name == panelName)
                    return panel;
            }

            return app.CreateRibbonPanel(tabName, panelName);
        }

        /// <summary>
        /// Loads and applies the large/small icon files to a push button, skipping any icon that
        /// fails to load (matches every call site's existing null-check-before-assign behavior).
        /// </summary>
        internal static void ApplyIcons(PushButtonData data, IconLoader iconLoader, string iconFileName)
        {
            var largeIcon = iconLoader.LoadLarge(iconFileName);
            if (largeIcon != null)
                data.LargeImage = largeIcon;

            var smallIcon = iconLoader.LoadSmall(iconFileName);
            if (smallIcon != null)
                data.Image = smallIcon;
        }

        /// <summary>Same as the PushButtonData overload, for pulldown buttons.</summary>
        internal static void ApplyIcons(PulldownButtonData data, IconLoader iconLoader, string iconFileName)
        {
            var largeIcon = iconLoader.LoadLarge(iconFileName);
            if (largeIcon != null)
                data.LargeImage = largeIcon;

            var smallIcon = iconLoader.LoadSmall(iconFileName);
            if (smallIcon != null)
                data.Image = smallIcon;
        }

        /// <summary>Same as the PushButtonData overload, for split buttons.</summary>
        internal static void ApplyIcons(SplitButtonData data, IconLoader iconLoader, string iconFileName)
        {
            var largeIcon = iconLoader.LoadLarge(iconFileName);
            if (largeIcon != null)
                data.LargeImage = largeIcon;

            var smallIcon = iconLoader.LoadSmall(iconFileName);
            if (smallIcon != null)
                data.Image = smallIcon;
        }
    }
}
