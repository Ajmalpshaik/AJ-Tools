# Changelog

This changelog tracks tagged AJ Tools source milestones. Public installer downloads are published separately in `AJ-Tools-Installer`.
Release tags should use `vX.Y.Z`. Older legacy tags with other formats remain in repository history.

## [Unreleased]

- No unreleased changes.

## [1.13.8] - 2026-07-18

Third cleanup pass, acting on the items v1.13.7 had deliberately deferred:

- **Perf**: `SmartMepTagService.MarkDenseZones` and `SmartTagPlacementEngine`'s parallel-group check
  both replaced an O(n^2) full pairwise scan with the existing `AnnotationSpatialIndex` as an X/Y
  coarse pre-filter. The original exact 3D distance check is still applied to every result, so
  behavior is unchanged, just faster on models with many tags.
- **Deduped**: the ~150-line duplicated leader-probing reflection block that `SmartTagPlacementEngine`
  and `IntelligentTagArrangerService` had each reimplemented is now shared via `LeaderLogicService`.
  The one deliberate behavioral difference between the two tools was preserved.
- **Reorganized**: `SharedParamUtils.cs` now holds only genuinely shared helpers; the Shared Param to
  Family Param conversion's own snapshot/restore logic moved into its Service, the only place that
  used it.
- **Fixed**: AJ AI safety validator now also blocks `using static` and `using X = Y;` type-alias
  directives, closing the specific bypass documented in v1.13.6/v1.13.7's notes.
- Evaluated and deliberately left as-is, each for a documented reason (see
  `src/Properties/AssemblyInfo.cs` for detail): `DuctShapeService`'s reflection-based shape read,
  `LocationDataAssignerWindow.xaml.cs`'s embedded business logic, Colorize/FilterPro's near-identical
  load methods (real behavioral drift found between them), `FilterProState`/`FilterSelection`'s
  property overlap, and `FilterCategoryItem`/`PatternItem`/`GraphicsIdOption`'s identical wrapper
  shape.

## [1.13.7] - 2026-07-18

Second cleanup pass, acting on the items v1.13.6 had deliberately deferred:

- **Fixed**: AJ AI's `task.Wait()` now has a hard backstop instead of no timeout at all - narrows
  (does not fully close) the freeze risk for a script that never yields at a loop checkpoint.
- **Fixed**: Gemini API key now sent via a header instead of a URL query param, matching the OpenAI
  client's existing approach.
- **Fixed**: a naming collision between two unrelated `DuctSelectionFilter` classes (not a live bug,
  a future trap) - renamed one to `DuctCurveOnlySelectionFilter`.
- **Extracted**: the four Commands that still had their full tool logic inline instead of a Service
  (Ceiling Magnet, Reassign Level, Arrange Text in Box, Force Tag Leader L-Shape) each now have a
  proper Service backing them; the Commands are thin wrappers.
- **Deduped**: the four config-store classes' identical config-path builder, and
  AnnotationRibbonManager's 28 repeated icon-loading blocks.
- Still deferred (see `src/Properties/AssemblyInfo.cs` for the full list): two O(n^2) hot loops in
  the tag-placement tools, a duplicated leader-probing block between two Services, Colorize/FilterPro
  duplication, `LocationDataAssignerWindow.xaml.cs`'s embedded business logic, and the AI safety
  validator's remaining text-matching (not AST/semantic) limitation.

## [1.13.6] - 2026-07-17

Full repo structure/cleanliness review plus a full code review pass (Core, Helpers, Commands, all
Services, Models, UI, and the AJ AI/GeminiShell subsystem), then acted on the safe/verifiable
findings. See `src/Properties/AssemblyInfo.cs` for the full itemized list. Summary:

- **Fixed**: AJ Annotation ribbon typo ("Auto Dimention" -> "Auto Dimension", visible on the tab).
- **Fixed**: AJ AI safety validator now blocks `#r`/`#load` script directives (previously a full,
  undetected bypass of every other safety check) and reflection-based indirect member access, and
  covers a few more dangerous APIs (SmtpClient, Dns, Ping, Process.Kill, Environment.FailFast).
- **Fixed**: AJ AI script execution now always completes its Task even if the failure-path
  transaction rollback itself throws (previously could hang the AJ AI pane on "busy" forever).
- **Fixed**: a real null-reference risk in Revision Cloud By Elements when no view is active.
- **Removed**: ~15 confirmed-unused classes/methods (verified unused repo-wide before deletion).
- **Cleaned up**: a couple of small duplicated helpers (ribbon panel lookup, a ViewCrop geometry
  check) consolidated into their existing shared helpers; 6 previously-silent empty catch blocks
  now document why the failure is safe to ignore instead of swallowing it invisibly.
- Not done this pass (needs a Revit/Visual Studio environment to verify safely, so left for a
  follow-up rather than guessed at blind): the larger structural duplication in a few tools
  (Ceiling Magnet, Force Tag Leader L-Shape, Reassign Level, Arrange Text in Box all still have
  their full logic inline instead of in a Service), a couple of O(n^2) hot loops in Smart MEP Tag /
  Intelligent Tag Arranger, and moving the AI safety validator from text-matching to a real
  AST/semantic scan.

## [1.13.5] - 2026-07-16

Catch-up release: everything built in the working source tree since v1.11.3, pushed to GitHub in one
batch (nothing here was released one version at a time — the working folder had moved on to 1.13.5
before this sync).

- **Multi-version build**: one codebase now builds Revit **2020 through 2027** from a single project,
  via root `Directory.Build.props`/`.targets` (configs `Release`/`Debug` for 2020 through `Release
  R27`/`Debug R27`, frameworks net472 / net48 / net8.0-windows / net10.0-windows, per-version `obj`
  isolation so builds don't clash). 2020 remains the tested baseline.
- **AJ AI (GeminiShell) can now run live against Revit**: a local named-pipe bridge
  (`mcp-server/`, `tools/invoke-revit-bridge.ps1`) lets an AI session run C# directly against the open
  Revit document, with reflection/assembly-loading and destructive operations blocked by design.
  Includes a non-modal "AJ AI is working" activity banner, an append-only audit log of every request
  at `%AppData%/AJTools/autodebugger-audit.jsonl`, a compiled-script cache, connection speed-ups
  (persistent pipe, Roslyn pre-warm), an instant handoff so a second chat window can take over from an
  idle one instead of waiting out a timeout, and locked-DLL-safe deployment (each build publishes to a
  fresh AppData payload folder so it can deploy while Revit still has the previous build loaded).
- Fixed two frozen progress bars in the AI shell: the pane's own execution bar (was bound to
  `Application.Current`, always null inside Revit) and the floating activity popup's bar (was a static
  fixed-width element with nothing driving it) — both now genuinely animate while a script runs.
- **Version-safe API hardening** for 2024+/2026+/2027 builds: `ElementIdHelper.FromInt` (the
  `ElementId(int)` constructor is gone in real Revit 2027), `IsDefinedBuiltInCategory` /
  `IsDefinedBuiltInParameter` (Int64 enum widening in 2024+), and category-based dimension collectors
  (2025+ `LinearDimension`).
- Ceiling Magnet: on Revit 2025.3+ now reads the ceiling's real grid lines
  (`Ceiling.GetCeilingGridLines`) for exact tile size and anchor, with a safe fallback to the original
  pattern-based method everywhere else.
- Added the **Arrange Text in Box** tool (AJ Annotation tab, new "Text" panel), ported from the pyRevit
  "Text Box Arrange Loop" script.
- The version-numbering mismatch noted in earlier entries (working tree at 1.10.0 vs GitHub tag
  v1.11.3) is resolved as of this release — the working tree's own version (1.13.5) is now the
  reconciled number going forward.

## [1.11.3] - 2026-07-07

- Fixed Revit startup dependency resolution so bundled DLLs such as `CommunityToolkit.Mvvm.dll` load from the AJ Tools install folder.
- This prevents the Revit 2024 `OnStartup` failure seen when the Gemini Shell dockable pane asks for `CommunityToolkit.Mvvm`.
- Fixed modern Revit packaging so Revit 2025-2027 payloads include copied NuGet dependency DLLs and `.deps.json` companion files.

## [1.11.2] - 2026-07-06

- Optimized `IndependentTag` compatibility so Revit 2022 and newer use the cleaner reference-based tag and leader APIs.
- Updated the L-Shape Leader command to use direct `IndependentTag` APIs first and keep reflection only as a fallback.
- Revit 2020-2021 keep the legacy tag API path required by their older Revit API surface.

## [1.11.1] - 2026-07-06

- Split Revit 2020-2024 into separate .NET Framework package payloads built against matching Revit API reference packages.
- Revit 2020 now targets .NET Framework 4.7.2, while Revit 2021-2024 target .NET Framework 4.8.
- The installer now prefers exact per-year payload folders before the old shared `2020-2024` fallback.
- Release packaging now produces API-specific payloads for all supported Revit versions from 2020 through 2027.

## [1.11.0] - 2026-07-06

- Added modern Revit builds for Revit 2025-2026 on .NET 8 and Revit 2027 on .NET 10.
- Added versioned installer payload folders for the modern Revit runtimes.
- Updated installer packaging to deploy the matching payload for Revit 2020-2027.

## [1.10.1] - 2026-07-06

- Updated the installer to stage AJ Tools folders and `.addin` manifests for Revit 2020-2027.
- Revit 2025-2027 now receive installer entries, while still reporting `NEEDS_REVIEW` until the separate modern .NET/Revit API build is completed.

## [1.10.0] - 2026-07-03

- Added the **MEP Openings** split-button workflow in the MEP panel.
- Added Opening Settings for element-specific shape, buffer, insulation, and merge-distance rules.
- Added Create Openings for selected pipes, ducts, cable trays, and conduits in current-model walls, floors/slabs, and beams.
- Verified a clean Release build against the Revit 2020 API.

## [1.9.1] - 2026-07-02

- Fixed Colorize shuffle behavior so repeated shuffles stay in the window and apply immediately.
- Removed the Colorize rule-type step so selected values are matched with Equals.
- Fixed shared fill-pattern visibility handling used by Colorize and Filter Pro shuffle colors.

## [1.9.0] - 2026-07-02

- Added the **Colorize** tool in the View panel for per-view element overrides by category or parameter values.
- Reused Filter Pro category, parameter, value, rule, and override engines without creating persistent view filters.
- Ported and hardened the retired pyRevit Colorize workflow.

## [1.8.0] - 2026-07-01

- Full project audit: added the **Pipe Sizing** tool (MEP panel) for domestic water pipe sizing from fixture units, system type, pipe material, and velocity limit.
- Hardened the AJ AI shell with `GeneratedCodeSafetyValidator` (blocks process/registry/network/reflection/unmanaged/file-delete calls in AI-generated scripts and flags destructive Revit operations for user confirmation before running), plus activity logging.
- Fixed a dead ribbon wiring gap: `CmdPurgeUnusedFamilyParametersAvailability` existed but was never assigned to its button, so "Purge Family Parameters" stayed clickable outside the Family Editor. It is now wired in.
- Fixed the About panel's inconsistent "Aj tool" ribbon label.
- Removed 8 orphaned icon resources and a stray local dev script/screenshot that had no code references.
- Verified a clean Release/x64 build (zero errors, zero warnings) against the Revit 2020 API.

## [1.7.0] - 2026-07-01

- AJ Annotation tab refactor/audit: full metadata blocks across every Dimensions, Auto Duct Dimension, Tags, Duct Flow, Revision Cloud, and Text tool; single-undo grouping for Copy Dimension Text, Copy Text, and continuous Revision Clouds; About and both ribbon-builder files standardized. All tool behaviour unchanged.

## [1.6.0] - 2026-07-01

- Modify / MEP / Coordination / Data / Manage / Family panels refactor/audit: full metadata blocks across every tool in these panels; Match Elevation now a single undo step; Reassign Level gains a Full-Project bulk-edit confirmation; version-safe ElementId access (Linked ID Viewer, Reassign Level); Duct Standards no-document path cancels cleanly with a project guard; removed loose scratch scripts from src. All tool behaviour unchanged.

## [1.5.4] - 2026-06-30

- Datums panel refactor/audit: full metadata blocks across all datum tools, removed success popups (silent success), single-undo batch for window-select Flip Bubbles, Family-Editor guards, and de-duplicated reset logic. Datum behaviour unchanged.

## [1.5.3] - 2026-06-30

- Graphics panel refactor/audit: single-undo TransactionGroup for both Match tools, view-scoped Reset Element Graphics in View, full metadata blocks, and 2024+ ElementId readiness. Graphics behaviour unchanged.

## [1.5.2] - 2026-06-27

- View Crop tool refactor/audit pass: shared helpers, bulk-edit confirmation, ElementId helper for 2024+ readiness. Behaviour of View Crop unchanged.

## [1.5.1] - 2026-06-24

- Integrated the AJ AI (Gemini Shell) tool into the main AJ Tools ribbon under a new "AI Assistant" panel, fixing visibility issues caused by an empty standalone tab.
- Fixed MSBuild compilation errors related to legacy PackageReference restores in the zero-warnings 2020 project configuration.

## [1.5.0] - 2026-05-30

- Added Search and Sort functionality for Categories and Parameters in the Filter Pro tool.
- Modernized ListBox and ListBoxItem styling in the shared UI components.
- Removed `CaseSensitive` checkbox logic from `FilterProWindow` in favor of more robust search/sort.
- Various minor stability fixes in `AutoDimensionService`, `LeaderLogicService`, and `SectionMarkVisibilityService`.

## [1.4.9] - 2026-05-25

- Added new **Section Mark Visibility** tool to automatically manage section visibility in plan views based on Sheet Number filters or placement status.
- Upgraded **View Crop** tool with persistent settings memory, custom diagnostics windows, support for coordination models, and integrated annotation crop configuration.
- Standardized namespaces, project files, and references for a zero-warnings compile on Revit 2020.


## [1.4.8] - 2026-05-17

- Fixed `Transfer View Templates` so the `Copy From` and `Copy To` document dropdowns show readable Revit document names instead of the internal `DocumentOption` type name.
- Verified the repository as a C# Revit add-in source repo with no pyRevit extension structure present.
- Cleaned local generated build outputs and confirmed the Release build succeeds with Revit 2020 API references and .NET Framework 4.7.2.

## [1.4.7] - 2026-05-14

- Added separate `Purge Unplaced 3D Views` and `Purge Unplaced Sections` tools under the AJ Tools Purge menu with preview, selection, confirmation, delete probing, transaction rollback, and final purge reporting.
- Added the separate `AJ Annotation` ribbon tab with `Duct Reference Dimension` and `Active View Duct Dimensions` tools.
- Updated Reset Graphics behavior so category reset uses all overridable active-view categories and element reset scans document elements safely.
- Cleaned startup logging so AJ Tools writes a temp log only when ribbon startup fails.
- Fixed generated `.addin` XML in the shared build target.

## [1.4.6] - 2026-05-10

- Reduced the `Apply Graphics` startup window size again for smaller screens.
- Added a compact tabbed layout so graphics settings and category selection are separated without increasing the default window height.
- Kept a visible custom title-bar close button, cancel behavior, and resize support for the WPF settings window.

## [1.4.5] - 2026-05-10

- Reduced the `Apply Graphics` default window size for smaller screens while keeping the settings area scrollable.
- Restored native Windows close and resize behavior so the standard title-bar close button remains visible.
- Clamped the startup window size to the available screen work area before showing the dialog.

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
