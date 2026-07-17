using Autodesk.Revit.UI;

namespace AJTools.App
{
    /// <summary>
    /// Shared ribbon-panel lookup used by both RibbonManager (AJ Tools tab) and
    /// AnnotationRibbonManager (AJ Annotation tab).
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
    }
}
