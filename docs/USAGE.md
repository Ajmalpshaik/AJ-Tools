# Usage Guide

## Ribbon Groups
- View: View Crop, Unhide All, Toggle Links, Filter Pro
- Graphics: Toggle Links, Unhide All, Reset Graphics
- Links: Linked ID of Selection, View by Linked ID
- 3D Views: 3D Views as per Workset
- Dimensions: Auto Dims, Dim By Line, Copy Dim Text
- Datums: Reset to 3D Extents, Flip Grid Bubble
- MEP: Match Elevation, Duct Flow, Filter Pro
- Annotations: L-Shape Leader, Reset Text, Copy Swap Text
- Info: About

## Typical Workflow
1. Open a Revit 2020 project (non-template view).
2. Use AJ Tools commands from the AJ Tools ribbon tab.
3. For model cleanup, start with Graphics tools.
4. For annotation consistency, use Dimensions/Datums/Annotations tools.
5. For duct workflows, configure Duct Flow settings first.

## Notes
- View Crop supports plan, section, elevation, area plan, engineering plan, and detail/callout views.
- View Crop skips views controlled by scope boxes or view templates that lock crop settings.
- Auto Dims requires Crop View enabled and plan/section/elevation context.
- Some tools are blocked by view template locks.
- Very large models may take longer in Filter Pro value scanning.
