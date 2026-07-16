# Usage Guide

## Ribbon Groups

The add-in registers **two** ribbon tabs (panel order as built by `Core/RibbonManager.cs` and
`Core/AnnotationRibbonManager.cs`):

### AJ Tools

- View: View Crop, Unhide All, Toggle Link, Filter Pro, Section Mark Visibility
- Graphics: Apply Graphics, Match Graphics, Reset Graphics
- Datums: Reset Grid/Level Extents to 3D, Modify Level Extents, Flip Grid/Level Bubbles
- Modify: Match MEP Element Elevation, Reassign Reference Level, Pin/Unpin Elements
- MEP: Connect MEP Elements, Elements to Ceiling Grid, HVAC Schematic, Pipe Sizing
- Coordination: Element ID lookup, 3D Views by Workset, Link Workset
- Data: Assign Location, Duct Standard
- Manage: Transfer View Templates, Purge (unplaced 3D views, unplaced sections, family parameters)
- Family: Shared to Family
- AI Assistant: AJ AI (Gemini C# Shell, with a dockable pane)
- About: About

### AJ Annotation

- Auto Dimention: Auto Duct Dimension (single duct to wall, all duct to wall)
- Dimensions: Automatic Dimension, Quick Dimension, Copy Dimension Text
- Annotation: Duct Flow Annotations, Revision Clouds, Copy/Swap Text Notes
- Family: Center Annotation
- Tags: Smart MEP Tags, Rearrange Tags, L-Shape Leader
- Text: Arrange Text in Box

## Typical Workflow
1. Open a Revit project (non-template view). Revit 2020 is the validated version; 2021–2027 builds exist but need Revit-side validation.
2. Use AJ Tools commands from the AJ Tools ribbon tab.
3. For model cleanup, start with View, Graphics, and Datums tools.
4. For annotation consistency, use Dimensions, Annotation, and Tags tools.
5. For MEP workflows, use Smart Connect, Ceiling Grid, HVAC Schematic, and Duct Flow tools as needed.

## Notes
- Toggle Links changes the Revit Links category visibility setting for the active view only.
- Unhide All clears Temporary Hide/Isolate and permanently hidden elements in the active view.
- View Crop supports plan, section, elevation, area plan, engineering plan, and detail/callout views.
- View Crop skips views controlled by scope boxes or view templates that lock crop settings.
- Purge Unplaced 3D Views and Purge Unplaced Sections preview non-template unplaced views separately, skip the active view and default `{3D}` view where applicable, and report purged, skipped, and failed counts.
- Apply Graphics uses one selected-element source for both modes, remembers the last-used settings, and applies either element overrides directly or category overrides from the categories found in those selected elements.
- Reset Category Graphics in View clears all overridable active-view categories, including annotation categories.
- Reset Element Graphics in View clears document element overrides in the active view.
- Duct Reference Dimension tools work in floor, ceiling, and engineering plan views.
- Active View Duct Dimensions skips vertical ducts, ducts shorter than 1000 mm, and ducts already covered by existing dimensions.
- HVAC Schematic creates a new drafting view from selected supported HVAC elements only.
- Ceiling Magnet snaps point-based elements after one ceiling-grid anchor pick.
- Auto Dims requires Crop View enabled and plan/section/elevation context.
- Some tools are blocked by view template locks.
- Very large models may take longer in Filter Pro value scanning.
