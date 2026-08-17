#region Metadata
/*
 * Tool Name     : AJ Annotation Ribbon Manager
 * File Name     : AnnotationRibbonManager.cs
 * Purpose       : Builds the separate "AJ Annotation" ribbon tab - its panels (Auto Dimension, Dimensions,
 *                 Annotation, Family, Tags, Text) and every dimension, tag, flow, revision-cloud, and text tool.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-05-10
 * Last Updated  : 2026-08-15
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
 * v1.5.0 (2026-08-15) - Auto Dimension panel: the "Auto Duct Dimension" pulldown becomes "Auto MEP
 *                       Dimension" (AddAutoDuctDimensionTool renamed AddAutoMepDimensionTool) with four
 *                       children - pick runs, dimension the selection, dimension the whole view, and
 *                       settings - replacing the two duct-only buttons. Dimensions panel: an
 *                       "Automatic Dimension Settings" child added to the Automatic Dimension pulldown,
 *                       and the three existing tooltips reworded now that the tool is settings-driven.
 *                       The one-icon-reused-across-children pattern is kept and now factored into a
 *                       local AddMepChild helper; the settings child uses settings.png via
 *                       RibbonPanelHelper.ApplyIcons, matching the other settings buttons on this tab.
 * v1.4.0 (2026-07-21) - Section Mark Visibility moved onto the Tags panel here, off the AJ Tools tab's
 *                       View panel (RibbonManager.cs) - same command (CmdSectionMarkVisibility),
 *                       AvailabilityClassName, and icon, just re-homed and now a standalone PushButton
 *                       here instead of a top-level tool there.
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
        private const string DimensionPanelName = "Dimensions";
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

            // One "Dimensions" panel holds every dimension tool - the separate "Auto Dimension" panel
            // was merged into it, so Auto MEP Dimension now sits beside the others.
            RibbonPanel dimensionsPanel = GetOrCreatePanel(DimensionPanelName);
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
                ToolTip = "Force tags to use a right-angle leader. Preselect tags or pick tags (Tab cycles) until Esc."
            };
            RibbonPanelHelper.ApplyIcons(lShapeLeaderData, _iconLoader, "L-ShapeLeader.png");

            var stackedItems = panel.AddStackedItems(smartMepTagData, arrangeTagsData, lShapeLeaderData);

            if (stackedItems.Count >= 2)
            {
                if (stackedItems[0] is PulldownButton smartMepTagPulldown)
                {
                    AddChildPushButton(smartMepTagPulldown, "cmdSmartMepTag", "Smart MEP\nTags", "Analyse the active view and intelligently tag MEP elements (ducts, pipes, equipment, accessories, cable trays) with clash-free placement.", typeof(CmdSmartMepTag).FullName, "Smart MEP TAG.png");
                    AddChildPushButton(smartMepTagPulldown, "cmdSmartMepTagSettings", "Smart MEP Tagging\nSettings", "Choose which categories Smart MEP Tags will tag, set each one's priority, and turn skipping of vertical runs on or off. The vertical-run setting is shared with Create Tags and Stack Tags.", typeof(CmdSmartMepTagSettings).FullName, "settings.png");
                }

                if (stackedItems[1] is PulldownButton arrangeTagsPulldown)
                {
                    AddChildPushButton(arrangeTagsPulldown, "cmdIntelligentTagArranger", "Rearrange\nTags", "Rearrange selected tags into a clean vertical stack. The nearest T1-to-L1 tag position is placed first, then remaining tags stack above or below based on T1 relative to L1.", typeof(CmdIntelligentTagArranger).FullName, "Arrange Tag.png");
                    AddChildPushButton(arrangeTagsPulldown, "cmdIntelligentTagArrangerSettings", "Arrange Tags\nSettings", "Set the default vertical gap between tags stacked by Rearrange Tags, in mm on the printed sheet.", typeof(CmdIntelligentTagArrangerSettings).FullName, "settings.png");
                }
            }

            PulldownButtonData createTagsData = new PulldownButtonData("cmdCreateTagsPulldown", "Create\nTags");
            RibbonPanelHelper.ApplyIcons(createTagsData, _iconLoader, "cursor.png");

            // Stack Tags is its own button (Ajmal, 2026-08-17), not a child of Create Tags - the two
            // do different jobs, and burying one inside the other made it hard to find.
            PulldownButtonData stackTagsData = new PulldownButtonData("cmdStackTagsPulldown", "Stack\nTags");
            RibbonPanelHelper.ApplyIcons(stackTagsData, _iconLoader, "Arrange Tag.png");

            PulldownButtonData fixTagClashData = new PulldownButtonData("cmdFixTagClashPulldown", "Fix Tag\nClash");
            RibbonPanelHelper.ApplyIcons(fixTagClashData, _iconLoader, "Arrange Tag.png");

            // Stacked, not large (Ajmal, 2026-08-18) - the whole panel now reads as columns of three
            // small buttons, matching the Smart MEP Tags / Rearrange Tags / L-Shape Leader group.
            // AddStackedItems is what makes a button small: it puts three rows in the width of one
            // normal button, so Revit draws the 16x16 icon instead of the 32x32. ApplyIcons loads both
            // sizes for every button, so nothing else needs changing to switch a button between the
            // two styles.
            var createStack = panel.AddStackedItems(createTagsData, stackTagsData, fixTagClashData);

            if (createStack.Count >= 3 && createStack[0] is PulldownButton createTagsPulldown)
            {
                AddChildPushButton(createTagsPulldown, "cmdCreateTags", "Create\nTags", "Select the elements first and every one is tagged at once. Run it with nothing selected and you click one element at a time, each tagged the moment you click it - Esc to finish. Either way the tag is placed automatically at the distance set in Create Tags Settings, with an L-shaped leader. Skips elements already tagged in the view, shorter than the minimum length, or a vertical run.", typeof(CmdCreateTags).FullName, "cursor.png");
                AddChildPushButton(createTagsPulldown, "cmdCreateTagsSettings", "Create Tags\nSettings", "Choose which categories Create Tags can pick from, set how far the tag sits from its element and the shortest run worth tagging, and turn skipping of vertical runs on or off. The vertical-run setting is shared with Smart MEP Tags and Stack Tags.", typeof(CmdCreateTagsSettings).FullName, "settings.png");
            }

            if (createStack.Count >= 3 && createStack[1] is PulldownButton stackTagsPulldown)
            {
                AddChildPushButton(stackTagsPulldown, "cmdStackTags", "Stack\nTags", "Select one or more MEP elements, then click one location - a tag is created for every eligible element and the whole batch is arranged into a vertical stack starting there, same as Rearrange Tags. Click again to relocate the whole stack. Uses the same skip rules and settings as Create Tags, plus Arrange Tags Settings' gap. Press Esc when satisfied.", typeof(CmdStackTags).FullName, "Arrange Tag.png");
                AddChildPushButton(stackTagsPulldown, "cmdStackTagsArrangeSettings", "Stack Tags\nSettings", "Set the clear gap left between stacked tags, in mm on the printed sheet. The same setting Rearrange Tags uses - change it in either place and both follow.", typeof(CmdIntelligentTagArrangerSettings).FullName, "settings.png");
            }

            if (createStack.Count >= 3 && createStack[2] is PulldownButton fixTagClashPulldown)
            {
                AddChildPushButton(fixTagClashPulldown, "cmdFixTagClash", "Fix Tag\nClash", "Find every clashing tag in the active view and separate them. The tag closest to its own element keeps its place; the others move. Whatever cannot be separated is coloured and left selected. Works on any tags, however they were placed - run it again to have another go.", typeof(CmdFixTagClash).FullName, "Arrange Tag.png");
                AddChildPushButton(fixTagClashPulldown, "cmdClearTagClashMarks", "Clear Tag\nClash Marks", "Remove the clash colour from the tags in the active view. Resets the graphic override on every tag in this view, so a deliberate manual override here is cleared too.", typeof(CmdClearTagClashMarks).FullName, "Reset Overrides.png");
                AddChildPushButton(fixTagClashPulldown, "cmdFixTagClashSettings", "Fix Tag Clash\nSettings", "Set how many rounds to try, how far a tag may move, the colour used for tags that could not be separated, and what counts as a clash.", typeof(CmdFixTagClashSettings).FullName, "settings.png");
            }

            PushButtonData centerRoomTagsData = new PushButtonData("cmdCenterRoomTags", "Center Room\nTags", _assemblyPath, typeof(CmdCenterRoomTags).FullName)
            {
                ToolTip = "Move every room tag in the active view to the center of its tagged room. Handles local rooms and loaded linked rooms; skips orphaned, pinned, and unreadable tags."
            };
            // Same icon as Center Annotation (Ajmal, 2026-08-17) - both centre something, so they read
            // as a pair. It used to borrow Arrange Tag.png, which is the stacking icon and made it
            // look like another arranging tool.
            RibbonPanelHelper.ApplyIcons(centerRoomTagsData, _iconLoader, "Reset Position.png");

            PushButtonData sectionMarkVisibilityData = new PushButtonData("cmdSectionMarkVisibility", "Section Mark\nVisibility", _assemblyPath, typeof(CmdSectionMarkVisibility).FullName)
            {
                ToolTip = "Automatically manage section visibility based on Sheet Number filters or sheet placement status."
            };
            RibbonPanelHelper.ApplyIcons(sectionMarkVisibilityData, _iconLoader, "SectionMarkVisibility.png");

            // A stack of TWO is a valid stack - the last column simply has an empty third row rather
            // than one large button sitting oddly beside two columns of small ones.
            var lastStack = panel.AddStackedItems(centerRoomTagsData, sectionMarkVisibilityData);

            if (lastStack.Count >= 2 && lastStack[1] is PushButton sectionMarkVisibilityButton)
            {
                sectionMarkVisibilityButton.AvailabilityClassName = typeof(CmdPlanViewAvailability).FullName;
            }
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

            // Auto MEP Dimension leads as the large button, then the three stacked tools.
            AddAutoMepDimensionTool(panel);

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
                AddChildPushButton(autoDimPulldown, "cmdAutoDimensionsGrids", "Automatic Grid\nDimensions", "Create grid dimension rows in plan, section or elevation views. Uses the Automatic Dimension settings for rows, sides, gaps, styles and which grids to include.", typeof(CmdAutoDimensionsGrids).FullName, "Dimensions.png");
                AddChildPushButton(autoDimPulldown, "cmdAutoDimensionsLevels", "Automatic Level\nDimensions", "Create level dimension rows in section or elevation views. Uses the Automatic Dimension settings.", typeof(CmdAutoDimensionsLevels).FullName, "Dimensions.png");
                AddChildPushButton(autoDimPulldown, "cmdAutoDimensions", "Automatic Grid /\nLevel Dimensions", "Plans: dimension grids. Sections and elevations: dimension levels and grids. Uses the Automatic Dimension settings.", typeof(CmdAutoDimensions).FullName, "Dimensions.png");
                AddChildPushButton(autoDimPulldown, "cmdAutoDimensionSettings", "Automatic Dimension\nSettings", "Choose which rows are created, which side they sit on, their gaps and dimension styles, which grids and levels are used, and whether linked models are included.", typeof(CmdAutoDimensionSettings).FullName, "settings.png");

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

        private void AddAutoMepDimensionTool(RibbonPanel panel)
        {
            if (panel == null)
                return;

            PulldownButtonData pulldownData = new PulldownButtonData(
                "cmdAutoMepDimensionPulldown",
                "Auto MEP\nDimension");

            // One icon is loaded here and reused across the pulldown and every child, rather than going
            // through RibbonPanelHelper.ApplyIcons per button.
            var largeIcon = _iconLoader.LoadLarge(QuickDimensionIcon);
            if (largeIcon != null)
                pulldownData.LargeImage = largeIcon;

            var smallIcon = _iconLoader.LoadSmall(QuickDimensionIcon);
            if (smallIcon != null)
                pulldownData.Image = smallIcon;

            if (!(panel.AddItem(pulldownData) is PulldownButton pulldown))
                return;

            AddMepChild(
                pulldown, largeIcon, smallIcon,
                "cmdMepReferenceDimension",
                "Pick runs\nto dimension",
                "Pick ducts, pipes, cable trays or conduits one at a time and dimension each back to the nearest wall, column, beam, floor, grid or level. Press ESC to finish. One Ctrl+Z undoes the whole session.",
                typeof(MepReferenceDimensionCommand).FullName);

            AddMepChild(
                pulldown, largeIcon, smallIcon,
                "cmdMepReferenceDimensionSelection",
                "Dimension the\nselected runs",
                "Dimension the runs already selected, using the Auto MEP Dimension settings.",
                typeof(MepReferenceDimensionSelectionCommand).FullName);

            AddMepChild(
                pulldown, largeIcon, smallIcon,
                "cmdMepReferenceDimensionActiveView",
                "Dimension every run\nin this view",
                "Dimension every eligible run visible in the active view. Skips vertical runs, runs under the minimum length, and runs that already have a dimension - all reported at the end.",
                typeof(MepReferenceDimensionActiveViewCommand).FullName);

            PushButtonData settingsData = new PushButtonData(
                "cmdMepDimensionSettings",
                "Auto MEP Dimension\nSettings",
                _assemblyPath,
                typeof(CmdMepDimensionSettings).FullName)
            {
                ToolTip = "Choose which services are dimensioned, what they are measured to, and whether each reference comes from this model, from linked models, or both."
            };
            RibbonPanelHelper.ApplyIcons(settingsData, _iconLoader, "settings.png");
            pulldown.AddPushButton(settingsData);
        }

        private void AddMepChild(
            PulldownButton pulldown,
            System.Windows.Media.Imaging.BitmapSource largeIcon,
            System.Windows.Media.Imaging.BitmapSource smallIcon,
            string name,
            string text,
            string tooltip,
            string className)
        {
            PushButtonData data = new PushButtonData(name, text, _assemblyPath, className)
            {
                ToolTip = tooltip
            };

            if (largeIcon != null) data.LargeImage = largeIcon;
            if (smallIcon != null) data.Image = smallIcon;

            pulldown.AddPushButton(data);
        }

        private RibbonPanel GetOrCreatePanel(string panelName)
        {
            return RibbonPanelHelper.GetOrCreatePanel(_app, TabName, panelName);
        }
    }
}
