# Usage Guide

## Ribbon Groups
- View: View Crop, Unhide All, Toggle Links, Filter Pro
- Graphics: Apply Graphics, Match Graphics, Reset Graphics
- Datums: Reset Datums, Level Extents, Flip Grid Bubble
- Dimensions: Auto Dims, Dim By Line, Copy Dim Text
- Annotation: Duct Flow Annotations, Revision Clouds, Copy/Swap Text Notes
- Tags: Smart MEP Tags, Rearrange Tags, L-Shape Leader
- Modify: Match Elevation, Reassign Level, Pin / Unpin Elements
- MEP: Smart Connect, Elements to Ceiling Grid, HVAC Schematic
- Coordination: Linked ID of Selection, 3D Views as per Workset, Set Link Workset
- Data: Location Data, Duct Standard
- Manage: Transfer View Templates, Purge
- Family: Shared Parameters to Family, Center Annotations
- About: About

## Typical Workflow
1. Open a Revit 2020 project (non-template view).
2. Use AJ Tools commands from the AJ Tools ribbon tab.
3. For model cleanup, start with View, Graphics, and Datums tools.
4. For annotation consistency, use Dimensions, Annotation, and Tags tools.
5. For MEP workflows, use Smart Connect, Ceiling Grid, HVAC Schematic, and Duct Flow tools as needed.

## Notes
- Toggle Links changes the Revit Links category visibility setting for the active view only.
- Unhide All clears Temporary Hide/Isolate and permanently hidden elements in the active view.
- View Crop supports plan, section, elevation, area plan, engineering plan, and detail/callout views.
- View Crop skips views controlled by scope boxes or view templates that lock crop settings.
- Graphics tools apply, match, or reset active-view override graphics only.
- HVAC Schematic creates a new drafting view from selected supported HVAC elements only.
- Ceiling Magnet snaps point-based elements after one ceiling-grid anchor pick.
- Auto Dims requires Crop View enabled and plan/section/elevation context.
- Some tools are blocked by view template locks.
- Very large models may take longer in Filter Pro value scanning.
