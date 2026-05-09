# Changelog

This changelog tracks tagged AJ Tools source milestones. Public installer downloads are published separately in `AJ-Tools-Installer`.
Release tags should use `vX.Y.Z`. Older legacy tags with other formats remain in repository history.

## [1.4.4] - 2026-05-09

- Rebuilt `Apply Graphics` around a dark, compact settings manager with separate apply actions for element and category overrides.
- Added best-effort last-used settings memory for colors, patterns, line weights, transparency, halftone, cut-link state, and selected categories.
- Added active-view graphics override validation across Graphics commands and aligned `Reset Element Graphics in View` with the shared Graphics transaction flow.

## [1.4.3] - 2026-05-07

- Restored preset color buttons in `Apply Graphics`, but scoped each preset row to its own color field so preset clicks never spill into other targets.
- Changed `Apply as Category Graphics` to use the same selected-element source as `Apply as Element Graphics`, then derive selectable categories only from those selected elements.
- Kept direct Projection / Surface and Cut editing, and preserved linked-cut behavior when `Use Projection / Surface settings for Cut` is enabled.

## [1.4.2] - 2026-05-07

- Removed the unused `Preset Target` UI and quick-preset dependency from `Apply Graphics`.
- Renamed the combined Apply Graphics modes to `Apply as Element Graphics` and `Apply as Category Graphics`, and fixed apply-mode label visibility in the dark theme.
- Kept direct editable projection/surface and cut color controls, while preserving linked-cut behavior when `Use Projection / Surface settings for Cut` is enabled.

## [1.4.1] - 2026-05-07

- Combined Element Graphics and Category Graphics into one `Apply Graphics` tool with a shared UI mode switch.
- Added category selection inside the Apply Graphics window and removed the separate element/category apply commands from the ribbon.
- Fixed the `Use Projection / Surface settings for Cut` behavior so linked cut settings mirror line, pattern, weight, and fill settings correctly and unlink cleanly for manual editing.

## [1.4.0] - 2026-05-07

- Added the HVAC Schematic tool to create drafting-view schematics from selected ducts, air terminals, and mechanical equipment.
- Refined Ceiling Magnet selection and transaction flow for ceiling-grid snapping, including linked-ceiling support handling.
- Reorganized the AJ Tools ribbon layout and standardized metadata headers for the HVAC schematic and related touched files.

## [1.3.9] - 2026-05-06

- Cleaned the Graphics Tools command group: Apply Graphics, Match Graphics, and Reset Graphics.
- Added shared command context validation and summary transaction handling for graphics apply/reset flows.
- Standardized production metadata headers for Graphics Tools commands, services, models, and WPF files.
- Kept normal-success runs quiet and preserved the existing Revit graphics override behavior.

## [1.3.8] - 2026-05-06

- Cleaned the Toggle Link command and added standardized production metadata.
- Added validation before changing Revit Links category visibility in the active view.
- Kept Toggle Link normal-success runs quiet and scoped to Revit 2020 / .NET Framework 4.7.2.

## [1.3.7] - 2026-05-06

- Standardized production metadata headers for all View Crop C# and XAML files.
- Standardized production metadata header for the Unhide All command.
- Kept View Crop and Unhide All runtime logic unchanged from the previous releases.

## [1.3.6] - 2026-05-06

- Cleaned the Unhide All command and removed old debug-style comments.
- Fixed Unhide All to pass only elements permanently hidden in the active view to Revit's `UnhideElements` API.
- Kept Temporary Hide/Isolate reset behavior and normal-success runs quiet.

## [1.3.5] - 2026-05-06

- Cleaned the View Crop command flow, shared target-view selection path, and command result handling.
- Improved View Crop WPF labels, spacing, validation feedback, and normal-success dialog behavior.
- Confirmed the View Crop cleanup remains scoped to Revit 2020 and .NET Framework 4.7.2.

## [1.3.4] - 2026-04-19

- Refactored the About command to use a dedicated WPF About window.

## [1.3.3] - 2026-04-19

- Added pin tools and family parameter purge tools.
- Updated related UI and supporting code paths.

## [1.3.2] - 2026-04-13

- Added manual Smart MEP tag priority controls in the settings UI.

## [1.3.1] - 2026-04-13

- Improved Smart MEP tag priority placement behavior.
- Added telemetry support for Smart MEP tag priority handling.

## [1.3.0] - 2026-04-12

- Added a new tool suite.
- Retired the floor-plan import module.

## [1.2.1] - 2026-04-07

- Updated the About tool to a dedicated About window.
- Added clickable LinkedIn and email links.
- Added support for `Resources/AboutPhoto.png` in the packaged payload.

## [1.2.0] - 2026-04-07

- Added the Smart MEP Tag workflow.
- Added the Arrange Tags workflow.
- Updated shared leader logic and related ribbon assets.
