// Tool Name: AJ Annotation Ribbon Manager
// Description: Builds the separate AJ Annotation ribbon tab and tool registration.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-05-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI

using System;
using System.Reflection;
using Autodesk.Revit.UI;
using AJTools.Commands.Annotation;
using AJTools.Utils;

namespace AJTools.App
{
    /// <summary>
    /// Registers tools that must live outside the main AJ Tools ribbon tab.
    /// </summary>
    internal sealed class AnnotationRibbonManager
    {
        private const string TabName = "AJ Annotation";
        private const string DimensionPanelName = "Dimension";
        private const string QuickDimensionIcon = "Dimensions by Line.png";

        private readonly UIControlledApplication _app;
        private readonly string _assemblyPath;
        private readonly IconLoader _iconLoader;

        public AnnotationRibbonManager(UIControlledApplication app)
        {
            _app = app;
            _assemblyPath = Assembly.GetExecutingAssembly().Location;
            _iconLoader = new IconLoader(_assemblyPath);
        }

        public void CreateRibbon()
        {
            try
            {
                _app.CreateRibbonTab(TabName);
            }
            catch (Exception)
            {
                // Tab already exists.
            }

            RibbonPanel dimensionPanel = GetOrCreatePanel(DimensionPanelName);
            AddDuctReferenceDimensionTool(dimensionPanel);
            AddActiveViewDuctReferenceDimensionTool(dimensionPanel);
        }

        private void AddDuctReferenceDimensionTool(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PushButtonData buttonData = new PushButtonData(
                "cmdDuctReferenceDimension",
                "Duct Reference\nDimension",
                _assemblyPath,
                typeof(DuctReferenceDimensionCommand).FullName)
            {
                ToolTip = "Create a chained perpendicular reference dimension for ducts, nearby ducts, walls, structural columns, and structural beams."
            };

            var largeIcon = _iconLoader.LoadLarge(QuickDimensionIcon);
            if (largeIcon != null)
                buttonData.LargeImage = largeIcon;

            var smallIcon = _iconLoader.LoadSmall(QuickDimensionIcon);
            if (smallIcon != null)
                buttonData.Image = smallIcon;

            panel.AddItem(buttonData);
        }

        private void AddActiveViewDuctReferenceDimensionTool(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PushButtonData buttonData = new PushButtonData(
                "cmdDuctReferenceDimensionActiveView",
                "Active View Duct\nDimensions",
                _assemblyPath,
                typeof(DuctReferenceDimensionActiveViewCommand).FullName)
            {
                ToolTip = "Create segmented duct reference dimensions for eligible ducts visible in the active plan view. Skips vertical ducts, ducts shorter than 1000 mm, and ducts already dimensioned."
            };

            var largeIcon = _iconLoader.LoadLarge(QuickDimensionIcon);
            if (largeIcon != null)
                buttonData.LargeImage = largeIcon;

            var smallIcon = _iconLoader.LoadSmall(QuickDimensionIcon);
            if (smallIcon != null)
                buttonData.Image = smallIcon;

            panel.AddItem(buttonData);
        }

        private RibbonPanel GetOrCreatePanel(string panelName)
        {
            foreach (RibbonPanel panel in _app.GetRibbonPanels(TabName))
            {
                if (panel.Name == panelName)
                    return panel;
            }

            return _app.CreateRibbonPanel(TabName, panelName);
        }
    }
}
