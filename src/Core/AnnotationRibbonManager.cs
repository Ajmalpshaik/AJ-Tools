#region Metadata
/*
 * Tool Name     : AJ Annotation Ribbon Manager
 * File Name     : AnnotationRibbonManager.cs
 * Purpose       : Builds the separate "AJ Annotation" ribbon tab - its panels (Auto Dimension, Dimensions,
 *                 Annotation, Family, Tags, Text) and every dimension, tag, flow, revision-cloud, and text tool.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.3.0
 *
 * Created Date  : 2026-05-10
 * Last Updated  : 2026-07-17
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Commands, AJTools.Commands.Annotation, AJTools.Utils (IconLoader)
 *
 * Input         : UIControlledApplication (Revit startup).
 * Output        : The AJ Annotation ribbon tab with all panels and buttons registered.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Registers tools that live on their own tab, outside the AJ Tools tab.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.3.0 (2026-07-17) - Replaced 28 repeated 4-line "load large icon, null-check, assign; load small
 *                       icon, null-check, assign" blocks with calls to the shared
 *                       RibbonPanelHelper.ApplyIcons (code review cleanup pass) - same icons, same
 *                       null-safety, no visual change. AddAutoDuctDimensionTool's icon loading was
 *                       left as-is since it deliberately reuses one loaded icon across three buttons,
 *                       a different pattern from the simple repeated single-button blocks elsewhere.
 * v1.0.0 (2026-05-10) - Initial AJ Annotation tab with dimension, tag, flow, cloud, and text tools.
 * v1.1.0 (2026-07-01) - Refactor/audit: standardized metadata block. Ribbon layout unchanged.
 * v1.2.0 (2026-07-05) - Added the "Text" panel with the Arrange Text in Box tool.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Reflection;
using Autodesk.Revit.UI;
using AJTools.Commands;
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
        private const string DimensionPanelName = "Auto Dimension";
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

            RibbonPanel autoDimensionPanel = GetOrCreatePanel(DimensionPanelName);
            AddAutoDuctDimensionTool(autoDimensionPanel);

            RibbonPanel dimensionsPanel = GetOrCreatePanel("Dimensions");
            AddDimensionsPanelTools(dimensionsPanel);

            RibbonPanel annotationPanel = GetOrCreatePanel("Annotation");
            AddAnnotationPanelTools(annotationPanel);

            RibbonPanel familyPanel = GetOrCreatePanel("Family");
            AddFamilyPanelTools(familyPanel);

            RibbonPanel tagsPanel = GetOrCreatePanel("Tags");
            AddTagsPanelTools(tagsPanel);

            RibbonPanel textPanel = GetOrCreatePanel("Text");
            AddTextPanelTools(textPanel);
        }

        private void AddTextPanelTools(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PushButtonData arrangeTextData = new PushButtonData("cmdArrangeTextInBox", "Arrange Text\nin Box", _assemblyPath, typeof(CmdArrangeTextInBox).FullName)
            {
                ToolTip = "Fit selected text notes into a box you drag: each note is resized to the box width and the notes are spread evenly top-to-bottom with left edges aligned. Pick the top-left corner once, then pick bottom-right corners to re-fit live. Press Esc to finish."
            };
            RibbonPanelHelper.ApplyIcons(arrangeTextData, _iconLoader, "copyswaptext.png");

            panel.AddItem(arrangeTextData);
        }

        private void AddTagsPanelTools(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PulldownButtonData smartMepTagData = new PulldownButtonData("cmdSmartMepTagPulldown", "Smart MEP\nTags");
            RibbonPanelHelper.ApplyIcons(smartMepTagData, _iconLoader, "Smart MEP TAG.png");

            PulldownButtonData arrangeTagsData = new PulldownButtonData("cmdArrangeTagsPulldown", "Rearrange\nTags");
            RibbonPanelHelper.ApplyIcons(arrangeTagsData, _iconLoader, "Arrange Tag.png");

            PushButtonData lShapeLeaderData = new PushButtonData("cmdForceTagLeaderLShape", "L-Shape\nLeader", _assemblyPath, typeof(CmdForceTagLeaderLShape).FullName)
            {
                ToolTip = "Force tags to use a right-angle leader. Run again on the same tag to flip the elbow side. Preselect tags or pick tags (Tab cycles) until Esc."
            };
            RibbonPanelHelper.ApplyIcons(lShapeLeaderData, _iconLoader, "L-ShapeLeader.png");

            var stackedItems = panel.AddStackedItems(smartMepTagData, arrangeTagsData, lShapeLeaderData);

            if (stackedItems.Count >= 2)
            {
                if (stackedItems[0] is PulldownButton smartMepTagPulldown)
                {
                    AddChildPushButton(smartMepTagPulldown, "cmdSmartMepTag", "Smart MEP\nTags", "Analyse the active view and intelligently tag MEP elements (ducts, pipes, equipment, accessories, cable trays) with clash-free placement.", typeof(CmdSmartMepTag).FullName, "Smart MEP TAG.png");
                    AddChildPushButton(smartMepTagPulldown, "cmdSmartMepTagSettings", "Smart MEP Tagging\nSettings", "Configure category-wise enable/disable for Smart MEP Tag.", typeof(CmdSmartMepTagSettings).FullName, "settings.png");
                }

                if (stackedItems[1] is PulldownButton arrangeTagsPulldown)
                {
                    AddChildPushButton(arrangeTagsPulldown, "cmdIntelligentTagArranger", "Rearrange\nTags", "Rearrange selected tags into a clean vertical stack. The nearest T1-to-L1 tag position is placed first, then remaining tags stack above or below based on T1 relative to L1.", typeof(CmdIntelligentTagArranger).FullName, "Arrange Tag.png");
                    AddChildPushButton(arrangeTagsPulldown, "cmdIntelligentTagArrangerSettings", "Arrange Tag\nSettings", "Set default vertical spacing for Arrange Tags (tag_spacing_mm).", typeof(CmdIntelligentTagArrangerSettings).FullName, "settings.png");
                }
            }

            PushButtonData centerRoomTagsData = new PushButtonData("cmdCenterRoomTags", "Center Room\nTags", _assemblyPath, typeof(CmdCenterRoomTags).FullName)
            {
                ToolTip = "Move every room tag in the active view to the center of its tagged room. Handles local rooms and loaded linked rooms; skips orphaned, pinned, and unreadable tags."
            };
            RibbonPanelHelper.ApplyIcons(centerRoomTagsData, _iconLoader, "Arrange Tag.png");

            panel.AddItem(centerRoomTagsData);
        }

        private void AddFamilyPanelTools(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PushButtonData centerAnnotationData = new PushButtonData("cmdResetTextPosition", "Center\nAnnotation", _assemblyPath, typeof(CmdResetTextPosition).FullName)
            {
                ToolTip = "Center selected annotations in the active annotation family view."
            };
            RibbonPanelHelper.ApplyIcons(centerAnnotationData, _iconLoader, "Reset Position.png");

            panel.AddItem(centerAnnotationData);
        }

        private void AddAnnotationPanelTools(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PulldownButtonData ductFlowData = new PulldownButtonData("cmdDuctFlowPulldown", "Duct Flow\nAnnotations");
            RibbonPanelHelper.ApplyIcons(ductFlowData, _iconLoader, "Flowdirectioncreate.png");

            PulldownButtonData revisionCloudData = new PulldownButtonData("cmdRevisionCloudPulldown", "Revision\nClouds");
            RibbonPanelHelper.ApplyIcons(revisionCloudData, _iconLoader, "Cloud By Elements.png");

            SplitButtonData textToolsData = new SplitButtonData("cmdTextToolsSplit", "Copy / Swap\nText Notes");
            RibbonPanelHelper.ApplyIcons(textToolsData, _iconLoader, "copyswaptext.png");

            var stackedItems = panel.AddStackedItems(ductFlowData, revisionCloudData, textToolsData);

            if (stackedItems.Count >= 3)
            {
                if (stackedItems[0] is PulldownButton ductFlowPulldown)
                {
                    AddChildPushButton(ductFlowPulldown, "cmdFlowDirectionAnnotations", "Duct Flow\nAnnotations", "Place duct flow annotations along horizontal ducts.", typeof(CmdFlowDirectionAnnotations).FullName, "Flowdirectioncreate.png");
                    AddChildPushButton(ductFlowPulldown, "cmdFlowDirectionSettings", "Duct Flow Annotation\nSettings", "Choose the annotation family and spacing used for duct flow placement.", typeof(CmdFlowDirectionSettings).FullName, "settings.png");
                }

                if (stackedItems[1] is PulldownButton revisionCloudPulldown)
                {
                    AddChildPushButton(revisionCloudPulldown, "cmdRevisionCloudByElements", "Revision Clouds\nby Elements", "Create orthogonal stepped revision cloud boundaries aligned to dominant selected-element angle. Keeps running until Esc.", typeof(CmdRevisionCloudByElements).FullName, "Cloud By Elements.png");
                    AddChildPushButton(revisionCloudPulldown, "cmdRevisionCloudByElementsSettings", "Revision Cloud\nSettings", "Configure offset distance for Cloud By Elements.", typeof(CmdRevisionCloudByElementsSettings).FullName, "settings.png");
                }

                if (stackedItems[2] is PulldownButton textToolsSplit)
                {
                    AddChildPushButton(textToolsSplit, "cmdCopyText", "Copy Text\nNotes", "Copy the text value from one text note to others (click targets until ESC).", typeof(CmdCopyText).FullName, "copyswaptext.png");
                    AddChildPushButton(textToolsSplit, "cmdSwapText", "Swap Text\nNotes", "Swap the text values between two picked text notes (one-time).", typeof(CmdSwapText).FullName, "copyswaptext.png");
                }
            }
        }

        private void AddDimensionsPanelTools(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PulldownButtonData autoDimData = new PulldownButtonData("cmdAutoDimensionsPulldown", "Automatic\nDimension");
            RibbonPanelHelper.ApplyIcons(autoDimData, _iconLoader, "Dimensions.png");

            PulldownButtonData quickDimData = new PulldownButtonData("cmdQuickDimensionPulldown", "Quick\nDimension");
            RibbonPanelHelper.ApplyIcons(quickDimData, _iconLoader, "Dimensions by Line.png");

            PushButtonData copyDimTextData = new PushButtonData("cmdCmdCopyDimensionText", "Copy Dimension\nText", _assemblyPath, typeof(CmdCopyDimensionText).FullName)
            {
                ToolTip = "Copy Above/Below/Prefix/Suffix text from one dimension to others."
            };
            RibbonPanelHelper.ApplyIcons(copyDimTextData, _iconLoader, "Copy Dim Text.png");

            var stackedItems = panel.AddStackedItems(autoDimData, quickDimData, copyDimTextData);

            if (stackedItems.Count >= 2 && stackedItems[0] is PulldownButton autoDimPulldown && stackedItems[1] is PulldownButton quickDimPulldown)
            {
                AddChildPushButton(autoDimPulldown, "cmdAutoDimensionsGrids", "Automatic Grid\nDimensions", "Create horizontal/vertical grid dimension strings in plan views.", typeof(CmdAutoDimensionsGrids).FullName, "Dimensions.png");
                AddChildPushButton(autoDimPulldown, "cmdAutoDimensionsLevels", "Automatic Level\nDimensions", "Create level dimension strings in section or elevation views.", typeof(CmdAutoDimensionsLevels).FullName, "Dimensions.png");
                AddChildPushButton(autoDimPulldown, "cmdAutoDimensions", "Automatic Grid /\nLevel Dimensions", "Plan views: dimension grids. Sections/Elevations: dimension levels and grids.", typeof(CmdAutoDimensions).FullName, "Dimensions.png");

                AddChildPushButton(quickDimPulldown, "cmdQuickParallelCenterLineDimension", "Quick Parallel Dimension\nby Centerline", "Quickly create a dimension string for selected parallel elements using center line references.", typeof(CmdQuickParallelCenterLineDimension).FullName, "Dimensions by Line.png");
                AddChildPushButton(quickDimPulldown, "cmdQuickParallelFaceEdgeDimension", "Quick Parallel Dimension\nby Face / Edge", "Quickly create dimensions using both side faces/edges for each selected parallel element (for ducts/pipes this captures both sides).", typeof(CmdQuickParallelFaceEdgeDimension).FullName, "Dimensions by Line.png");
                AddChildPushButton(quickDimPulldown, "cmdDimensionGridsByLine", "Create Grid Dimensions\nby Picked Line", "Create a dimension string across intersecting grids using a picked line (plan, section, or elevation).", typeof(CmdDimensionGridsByLine).FullName, "Dimensions by Line.png");
                AddChildPushButton(quickDimPulldown, "cmdDimensionLevelsByLine", "Create Level Dimensions\nby Picked Line", "Create a dimension string across levels within the picked vertical range.", typeof(CmdDimensionLevelsByLine).FullName, "Dimensions by Line.png");
            }
        }

        private void AddChildPushButton(PulldownButton pulldown, string name, string text, string tooltip, string className, string iconName)
        {
            PushButtonData btnData = new PushButtonData(name, text, _assemblyPath, className)
            {
                ToolTip = tooltip
            };
            RibbonPanelHelper.ApplyIcons(btnData, _iconLoader, iconName);

            pulldown.AddPushButton(btnData);
        }

        private void AddAutoDuctDimensionTool(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PulldownButtonData pulldownData = new PulldownButtonData(
                "cmdAutoDuctDimensionPulldown",
                "Auto Duct\nDimension");

            var largeIcon = _iconLoader.LoadLarge(QuickDimensionIcon);
            if (largeIcon != null)
                pulldownData.LargeImage = largeIcon;

            var smallIcon = _iconLoader.LoadSmall(QuickDimensionIcon);
            if (smallIcon != null)
                pulldownData.Image = smallIcon;

            if (panel.AddItem(pulldownData) is PulldownButton pulldown)
            {
                PushButtonData btnSingle = new PushButtonData(
                    "cmdDuctReferenceDimension",
                    "single duct to wall",
                    _assemblyPath,
                    typeof(DuctReferenceDimensionCommand).FullName)
                {
                    ToolTip = "Create a chained perpendicular reference dimension for ducts, nearby ducts, walls, structural columns, and structural beams."
                };
                if (largeIcon != null) btnSingle.LargeImage = largeIcon;
                if (smallIcon != null) btnSingle.Image = smallIcon;

                pulldown.AddPushButton(btnSingle);

                PushButtonData btnAll = new PushButtonData(
                    "cmdDuctReferenceDimensionActiveView",
                    "all duct to wall",
                    _assemblyPath,
                    typeof(DuctReferenceDimensionActiveViewCommand).FullName)
                {
                    ToolTip = "Create segmented duct reference dimensions for eligible ducts visible in the active plan view. Skips vertical ducts, ducts shorter than 1000 mm, and ducts already dimensioned."
                };
                if (largeIcon != null) btnAll.LargeImage = largeIcon;
                if (smallIcon != null) btnAll.Image = smallIcon;

                pulldown.AddPushButton(btnAll);
            }
        }

        private RibbonPanel GetOrCreatePanel(string panelName)
        {
            return RibbonPanelHelper.GetOrCreatePanel(_app, TabName, panelName);
        }
    }
}
