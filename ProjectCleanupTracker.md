# Project Cleanup Tracker

## 2026-07-18 Third Cleanup Pass - Acting on Remaining Deferred Items

Follow-up to the Second Cleanup Pass below. That pass deliberately deferred a further set of
findings; this pass came back and did the ones that could be done safely from a source-only
environment, and gives an honest final disposition on the rest.

- **Perf - Smart MEP Tag density check**: `SmartMepTagService.MarkDenseZones` scanned every tag
  candidate against every other candidate (O(n^2)) to find "dense zones" within `DensityRadius`.
  Rewrote it to use the existing `AnnotationSpatialIndex`/`AnnotationBox` (already used elsewhere in
  this file for annotation clash detection) as an X/Y coarse pre-filter, keyed off each candidate's
  midpoint. The exact original 3D `DistanceTo(...) <= DensityRadius` check is still applied to every
  candidate the index returns as a possible match, so the result is provably identical to before -
  for any two points, the 2D (X/Y-only) distance can never exceed the full 3D distance, so the index
  can only ever return a superset of the true matches, and the original check filters that superset
  down to the exact same answer. Just faster on models with a lot of MEP tags.
- **Perf - Smart Tag parallel-group check**: `SmartTagPlacementEngine.FindBestTagPosition` had the
  same O(n^2) shape when checking nearby candidates for "is there already a tag placed in a similar
  direction/position nearby" (`ParallelGroupDistance`). Added a small spatial index built once per
  placement run (`BuildCandidateSpatialIndex`/`QueryNearbyCandidatesByPosition`) and used it the same
  way - X/Y pre-filter only, no change to the actual comparison logic that runs on the filtered list.
- **Deduped leader-probing reflection block**: `SmartTagPlacementEngine` and
  `IntelligentTagArrangerService` had each independently reimplemented the same ~150-line reflection
  fallback chain for reading/writing a Revit tag's leader end point and elbow (`TryGetLeaderEnd`,
  `TryGetLeaderEndFromTaggedReference(s)`, `InvokeGetLeaderEnd`, `TryGetXYZProperty`,
  `TrySetLeaderElbow`, leader-end-condition get/set) - almost certainly copy-pasted from one to the
  other at some point. Consolidated the identical leaf logic into `LeaderLogicService` as shared
  static methods (`GetL1` was upgraded from an unused instance method to the shared static entry
  point, after confirming it had zero external callers before the change). Each file's own
  orchestration on top of those leaf calls was left where it was - in particular,
  `IntelligentTagArrangerService.TryApplyLShapeLeader` still does **not** fall back to toggling the
  leader end condition, per its own pre-existing comment ("Keep L1 exactly as-is") - that one
  intentional behavioral difference between the two tools was preserved, not merged away.
- **SharedParamUtils.cs reorganized**: this file mixed genuinely generic helpers (used by Purge, Duct
  Standards, and a Model class) with logic that only the Shared Param to Family Param conversion tool
  actually used (capturing/restoring a family parameter's value, formula, and reporting state across
  a shared-to-non-shared conversion). Moved the feature-specific half into
  `SharedParamToFamilyParamService.cs`, its only real consumer, keeping `SharedParamUtils.cs` down to
  the handful of methods multiple unrelated tools depend on. `IsReporting`/`TryGetSharedGuid` were
  deliberately left in `SharedParamUtils.cs` rather than also moved, specifically because
  `SharedParamToFamilyParamItem.cs` (a Model) depends on them too, and moving them would have created
  a new Model -> Service dependency that didn't exist before.
- **AJ AI safety - closed the using-static/type-alias gap**: `GeneratedCodeSafetyValidator` now also
  blocks `using static X;` and `using X = Y;` type-alias directives in generated scripts - both were
  documented as a known, open gap in the v1.13.6/v1.13.7 passes (a script could rename a blocked call,
  e.g. `Process.Start`, to a bare method name via `using static`, or rename a blocked type, e.g.
  `System.IO.File`, via a type alias, and dodge every name-based check). The alias pattern requires an
  `=`, so it does not match ordinary `using Namespace.Type;` imports. This remains text/regex
  matching, not an AST/semantic scan - see the file's own updated Notes for what's still not covered
  (mainly string-built member names and similar indirection).
- Version bump: suite version 1.13.7 -> 1.13.8 (patch: perf/cleanup fixes, no new tool).
- **Evaluated this pass and deliberately left alone**, each for a specific reason found by actually
  reading and comparing the code, not just skipped for time:
  - `DuctShapeService`'s reflection-based read of a duct type's `Shape` - there's a direct-API example
    elsewhere in the repo (`SmartConnectRouteBuilder.cs`'s `connector.Shape`), but that's a different
    API (`Connector.Shape`, not `DuctType.Shape`) - couldn't confirm `DuctType` exposes a direct
    `.Shape` property on every supported Revit version (2020-2027) without a compiler or API docs to
    check against, and a wrong guess would break compilation for a purely cosmetic gain.
  - `LocationDataAssignerWindow.xaml.cs`'s ~500 lines of embedded business logic (room/HVAC lookups,
    coordinate transforms, a per-element transaction loop) - it updates live UI progress-bar controls
    directly inside that loop for real-time feedback, so a safe extraction would mean inventing a new
    progress-callback abstraction to decouple the algorithm from the UI, which is a real design
    decision, not a mechanical move, and can't be checked visually here.
  - Colorize/FilterPro's near-identical `LoadParameters`/`LoadValues` methods - close comparison found
    real behavioral drift: different status messages, a conditional message branch only Colorize has,
    and critically, only Colorize's `LoadValues` calls `ApplyValueFilters()` immediately after
    loading - FilterPro relies solely on its `TextChanged`/`SelectionChanged` event handlers for that.
    Forcing a single shared method risks silently changing live behavior in one of the two tools.
  - `FilterProState`/`FilterSelection`'s ~20-property overlap - most properties genuinely match, but a
    few differ in type on purpose (`FilterProState` is a lightweight persisted snapshot using IDs/keys;
    `FilterSelection` is a live runtime selection using richer objects), and safely verifying a shared
    base class wouldn't break `FilterProStateTracker`'s conversion logic would need reading that file
    too, which wasn't done this pass - low value for the verification effort given this is cosmetic,
    not a bug or perf issue.
  - `FilterCategoryItem`/`PatternItem`/`GraphicsIdOption` - structurally identical wrapper classes, but
    two use a `Name` property and one uses `DisplayName` - unifying them risks silently breaking an
    XAML `{Binding Name}`/`{Binding DisplayName}` reference that can't be checked visually in this
    environment.
  - The AJ AI API-key `PasswordBox` swap flagged in the very first pass remains skipped, same reason
    as before: WPF's `PasswordBox.Password` isn't bindable the same way a normal `TextBox` is, and
    doing it properly needs a small attached-behavior helper that should be tested visually.
- Revit was not launched or tested - this pass was source-only (no Windows/.NET SDK available).
  Please test loading in Revit, especially Smart MEP Tag, Smart Tag Placement, Intelligent Tag
  Arranger, Shared Param to Family Param, and the AJ AI pane, before relying on this build.

## 2026-07-18 Second Cleanup Pass - Acting on Deferred Items

Follow-up to the 2026-07-17 pass below. That pass deliberately deferred a set of larger/riskier
findings rather than guess at them without a Windows/.NET SDK or Revit available. This pass came
back and did the ones that could be done safely from a source-only environment:

- **AJ AI safety**: `RevitExecutionService.task.Wait()` now has a hard backstop (MaxLoopRuntime +
  20s) instead of no timeout at all. Documented explicitly in the code that this narrows but does
  not fully close the freeze risk for a script that never yields at a loop checkpoint (a goto-loop,
  `Thread.Sleep`, or a single very long Revit API call) - confirming Roslyn's `CSharpScript.RunAsync`
  threading model against the Revit API's single-thread requirement needs a real environment.
- **Gemini API key**: now sent via the `x-goog-api-key` header instead of a URL query param, matching
  `OpenAiApiService`'s existing approach. Moderate confidence only - not verified against a live key
  from this environment; please confirm the AJ AI pane still connects to Gemini.
- **Naming collision fixed**: `AJTools.Utils.DuctSelectionFilter` renamed to
  `DuctCurveOnlySelectionFilter` to stop shadowing `AJTools.Services.DuctReferenceDimension.
  DuctSelectionFilter` (different namespaces, so not a live bug today, but a real trap for a future
  edit).
- **Config-store dedup**: `LinkWorksetSettings`, `SectionMarkVisibilityConfigStore`,
  `TagArrangeSettings`, `ViewCropConfigStore` all shared an identical `GetConfigPath()` - extracted
  into a new `AppDataConfigStore.GetPath(fileName)` helper.
- **Command -> Service extractions** (the four Commands flagged as having their full tool logic
  inline instead of a Service, per this project's own convention):
  - `CmdCeilingMagnet` (833 lines) -> `Services/CeilingMagnet/CeilingMagnetService.cs`
  - `CmdReassignLevel` (752 lines) -> `Services/ReassignLevel/ReassignLevelService.cs`
  - `CmdForceTagLeaderLShape` (~800 lines) -> `Services/ForceTagLeaderLShape/ForceTagLeaderLShapeService.cs`
    (elbow math itself stays in the existing shared `LeaderLogicService`, untouched)
  - `CmdArrangeTextInBox` (383 lines) -> `Services/ArrangeTextInBox/ArrangeTextInBoxService.cs`
  - Each Command is now a thin wrapper: selection/picking, transaction handling, and result dialogs
    stay in the Command; the algorithm moved to its Service. No behavior change in any of the four -
    same tolerances, same constants, same order of operations, just relocated with a metadata header
    matching this project's convention. One extraction (`ReassignLevelService`) was found already
    half-done and left in a non-compiling state by an interrupted prior attempt (a duplicate
    `OffsetHelper` type conflict) - this pass finished it properly rather than leaving it broken.
  - **Deliberately NOT extracted this pass**: `CmdCeilingMagnet`'s `TryCreateGridDefinition` still
    calls `DialogHelper` directly on its two failure paths (same as the original inline code) rather
    than being restructured to return an error for the Command to show - kept as-is to avoid changing
    its behavior/contract during the move.
- **AnnotationRibbonManager icon dedup**: 28 repeated 4-line icon-loading blocks replaced with calls
  to a new `RibbonPanelHelper.ApplyIcons` (three overloads, mirroring `RibbonManager`'s own already-
  working icon-loading logic rather than guessing at a shared Revit API base type that couldn't be
  verified without a compiler). `AddAutoDuctDimensionTool`'s icon loading was left alone since it
  deliberately reuses one loaded icon across three buttons - a different pattern from the simple
  repeated single-button blocks everywhere else.
- Version bump: suite version 1.13.6 -> 1.13.7 (patch: cleanup/fixes, no new tool).
- **Still deferred** (unchanged from the 2026-07-17 list below, not attempted this pass either): two
  O(n^2) hot loops in `SmartMepTagService.MarkDenseZones` / `SmartTagPlacementEngine.
  FindBestTagPosition`; the ~150-line duplicated reflection leader-probing block between
  `SmartTagPlacementEngine` and `IntelligentTagArrangerService`; Colorize/FilterPro's duplicated
  `LoadParameters`/`LoadValues` methods; `FilterProState`/`FilterSelection`'s ~20-property
  duplication; `FilterCategoryItem`/`PatternItem`/`GraphicsIdOption`'s identical wrapper shape;
  `LocationDataAssignerWindow.xaml.cs`'s embedded business logic (770-1300 lines); `SharedParamUtils.
  cs` living in Helpers/ instead of Services/; `DuctShapeService`'s reflection use where a direct API
  call might exist; a `PasswordBox` swap for the AJ AI API-key text inputs (skipped - WPF's
  `PasswordBox.Password` isn't bindable the same way, higher implementation risk for a minor finding);
  and the AI safety validator's core limitation (still text/regex matching, not an AST/semantic scan -
  `using static` and type-aliasing bypasses remain a known, documented, but unclosed gap).
- Revit was not launched or tested - this pass was source-only (no Windows/.NET SDK available).
  Please test loading in Revit, especially the AJ AI pane, Ceiling Magnet, Reassign Level, Arrange
  Text in Box, and Force Tag Leader L-Shape, before relying on this build.

## 2026-07-17 Structure/Cleanliness Review + Full Code Review Pass

Ran on top of the 2026-07-01 pass below, on the now-multi-version (2020-2027) build. Scope: repo
structure/cleanliness audit, then a full code review split across Core+Helpers, Commands, all
Services (3 sub-passes), Models, UI, and the AJ AI/GeminiShell subsystem (security-focused).

- **Repo structure/cleanliness**: found and fixed a 3-way version-number mismatch (README said
  1.10.0, AssemblyInfo.cs's own header said 1.13.1, the real assembly version was 1.13.5) and a
  stale file-count comment in the `.csproj`. Everything else checked out clean: no tracked build
  artifacts, no orphaned icons, no leaked secrets, `.gitignore` solid. PR #11, merged.
- **Code review - AJ AI safety net**: the `GeneratedCodeSafetyValidator` was found to have a full,
  undetected bypass via Roslyn `#r`/`#load` script directives (never disabled by `RoslynService`),
  plus gaps in reflection-invoke detection and a few missing dangerous APIs (SmtpClient, Dns, Ping,
  Process.Kill, Environment.FailFast). Closed the `#r`/`#load` gap and the reflection-invoke gap,
  added the missing API patterns. Still a text/regex scan, not an AST/semantic one - `using static`
  and type-aliasing bypasses remain a known, documented limitation, not a security boundary.
- **Fixed**: AJ Annotation ribbon typo ("Auto Dimention" -> "Auto Dimension"); a real null-deref
  risk in Revision Cloud By Elements (`Document.ActiveView` can be null); AJ AI's
  `RevitExecutionService` could hang the pane on IsBusy forever if a failure-path `RollBack()`
  itself threw after a failed `Commit()` - now guaranteed to always complete.
- **Removed** (cross-checked repo-wide as unused before deleting, none were referenced anywhere
  else): `RuleTypeItem`, `DuctDimensionBuildResult`, `DuctPipeSelectionFilter`,
  `ValidationHelper.ValidateViewType`/`ValidateCropBoxActive`, two unused
  `TransactionHelper.ExecuteSafe` overloads, `CmdForceTagLeaderLShape.AdjustElbowSide`,
  `CmdCreateMepOpenings.ShouldRunDirectOpenings`, `AutoDimensionService.GetCurveDirection`,
  `LeaderLogicService.ComputeSideElbow`/`DetermineToggleState`,
  `GraphicsSelectionService.GetValidPreselectedElementIds`, `QuickParallelDimensionService`'s dead
  single-arg `Execute` overload, `MepOpeningSourceElement.SourceLabel`, `LinkedSearchWindow`'s dead
  Identify/Reset/Close handlers and their now-orphaned helpers, `FilterProWindow.GetPatternItem`.
- **Consolidated**: `RibbonManager`/`AnnotationRibbonManager`'s duplicate `GetOrCreatePanel` into a
  new shared `RibbonPanelHelper`; `ViewCropExtentsService`'s duplicate `IsFinite` into the existing
  `ViewCropGeometryProjectionHelper.IsFinite`; two duplicated "10mm in feet" literals now use
  `Constants.MM_TO_FEET`.
- **Documented, not silently swallowed**: 6 previously-empty `catch { }` blocks (App.cs's DLL
  preload, `CmdSectionMarkVisibility`'s view refresh, 4 in `CmdForceTagLeaderLShape`'s reflection
  helpers) now have a one-line comment explaining why the failure is safe to ignore there -
  behaviour unchanged, but a future failure there is no longer invisible.
- Version bump: suite version 1.13.5 -> 1.13.6 (patch: cleanup/fixes, no new tool).
- **Explicitly NOT done this pass** - flagged for a follow-up rather than attempted blind (no
  Windows/.NET SDK or Revit available in this environment to compile/test-verify a larger change):
  `CmdCeilingMagnet`, `CmdForceTagLeaderLShape`, `CmdReassignLevel`, and `CmdArrangeTextInBox` still
  have their full tool logic inline in the Command instead of a Service; two O(n^2) hot loops in
  `SmartMepTagService.MarkDenseZones` / `SmartTagPlacementEngine.FindBestTagPosition`; the
  `AnnotationRibbonManager` icon-loading duplication (~150 lines, separate from the small
  `GetOrCreatePanel` fix already done); the four config-store classes' duplicated
  Load/Save-to-AppData pattern; `LocationDataAssignerWindow.xaml.cs`'s embedded business logic;
  `RevitExecutionService`'s `task.Wait()` has no independent hard timeout (needs the Roslyn
  `RunAsync` threading model confirmed against the Revit API single-thread requirement first);
  Gemini API key sent as a URL query param instead of a header (OpenAI's client already uses a
  header); a naming collision between two unrelated `DuctSelectionFilter` classes in different
  namespaces (not currently a bug, just a trap).
- Revit was not launched or tested - this pass was source-only (no Windows/.NET SDK available).
  Please test loading in Revit, especially the AJ AI pane, before relying on this build.

## 2026-07-01 Full Audit Pass

Ran on top of the 2026-06-24 pass below plus the panel-by-panel refactor work already completed
this week (v1.5.2 through v1.7.0, see `CHANGELOG.md`). Scope: full file inventory, `.csproj`/`.addin`
correctness, ribbon-to-command wiring, Revit API/transaction spot checks, XAML binding spot checks,
and repo hygiene. Full findings are in the chat report for this session; summary:

- Confirmed clean: all 268 `.cs` and 25 `.xaml` files in `AJ Tools.csproj` match disk exactly (no
  missing/orphaned compile entries); every ribbon icon file referenced by `RibbonManager.cs` /
  `AnnotationRibbonManager.cs` exists on disk; every ribbon-referenced command class exists; the
  `.addin` GUID (`{C14A253D-96C6-42B7-889A-CFE737556BA9}`) is consistent across `Addin/AJ Tools.addin`,
  `dist/AJ Tools.addin`, and both MSBuild deploy targets.
- Fixed: `CmdPurgeUnusedFamilyParametersAvailability` was defined and compiled but never assigned as
  the `Purge Family Parameters` button's `AvailabilityClassName` — now wired in `RibbonManager.cs`.
- Fixed: About panel label `"Aj tool"` renamed to `"About"`.
- Removed: 8 orphaned icon PNGs with zero code/XAML references (`AboutPhoto.png`, `Flowdirection.png`,
  `cancel.png`, `close.png`, `done.png`, `githublogo.png`, `information.png`, `linkedin.png`), the
  local-only `src/show_pipesizing_ui.ps1` preview script, and a stray `src/UI/ViewCrop/preview.png`
  screenshot.
- Verified build: MSBuild Release/x64, `SkipRevitAddinDeploy=true`, `SkipAjToolsAutoDeploy=true` —
  zero errors, zero warnings.
- Version bump: suite version 1.7.0 → 1.8.0 (new tool added: Pipe Sizing). `CHANGELOG.md` backfilled
  with the 1.5.2–1.7.0 entries that existed only in `AssemblyInfo.cs`'s own changelog.
- Known accepted debt (not changed this pass): most non-2024-flagged files still call
  `ElementId.IntegerValue` directly instead of `ElementIdHelper`. Not a live bug — the project builds
  Revit 2020 only right now — but will need a pass once a real 2021+ build target exists. Do not expand
  a future single-tool refactor into a project-wide `ElementIdHelper` migration without Ajmal's sign-off.
- Revit was not launched or tested. Please test loading in Revit before relying on this build.

---

Project path checked: `D:\Ajmal\Revit Addins`

Tool type found: C# Revit add-in source repository plus separate public installer repository. No pyRevit `.extension`, `.tab`, `.panel`, or `.pushbutton` structure was found.

Checked on: 2026-06-24

## Summary

Initial scan excluding Git internals: 727 files, 128 folders.

Current clean scan excluding Git internals: 367 files, 71 folders.

| Area | Status | Notes |
| --- | --- | --- |
| Workspace root | CLEANED | Removed local-only caches/tools and stale build log. |
| `AJ Tools` source repo | CLEANED | Removed generated outputs, stale package payloads, Visual Studio state, root probe files, and broken unused Gemini OAuth files. |
| `AJ-Tools-Installer` repo | CHECKED | Kept release ZIP and checksum as intentional public installer payload; corrected README version label. |
| pyRevit structure | VERIFIED_STRUCTURE | No pyRevit bundles present. |
| C# add-in structure | VERIFIED_STRUCTURE | Solution, project, manifest template, source resources, ribbon managers, and package scripts checked. |
| Build verification | VERIFIED_STRUCTURE | MSBuild Debug build passed with deployment disabled; one existing warning remains. |

## Folders Checked

| Folder | Status | Files removed | Files moved | Files kept | Items needing review | Build/load risk |
| --- | --- | --- | --- | --- | --- | --- |
| `D:\Ajmal\Revit Addins` | CLEANED | `build_log.txt`, `.claude/`, `.vscode/`, `.tools/` | None | `AJ Tools/`, `AJ-Tools-Installer/` | None | Root is not a Git repo. |
| `AJ Tools/.github` | CHECKED | None | None | Issue templates and PR template | None | None. |
| `AJ Tools/Addin` | CHECKED | None | None | `AJ Tools.addin` manifest template | None | Uses Revit 2020 add-in path and `AJTools.App.App`. |
| `AJ Tools/dist` | CLEANED | Generated DLL/addin/resources/release output | None | Installer, uninstaller, package, and tag scripts | None | Run `dist/package.ps1` to regenerate payloads. |
| `AJ Tools/docs` | CLEANED | None | None | Usage guide and ribbon preview image | None | Docs now include `AJ AI`. |
| `AJ Tools/src` | CLEANED | `bin/`, `obj/` | None | C# source, WPF UI, services, models, resources | Existing many modified source files predated this cleanup | Build passes; Revit runtime not tested. |
| `AJ Tools/src/Core` | VERIFIED_STRUCTURE | None | None | App and ribbon managers | None | Added AI pane registration. |
| `AJ Tools/src/GeminiShell` | CLEANED | `Services/AuthenticationService.cs`, `Services/GeminiService.cs` | None | Active API-key based Gemini/OpenAI shell files | AI shell needs Revit runtime validation | Build passes after project inclusion. |
| `AJ Tools/src/Resources` | VERIFIED_STRUCTURE | None | None | Canonical source icons/resources including `AJ_AI.png` | None | Resources copied by build/package. |
| `AJ-Tools-Installer` | CHECKED | None | None | Public installer docs, workflow, `releases/AJ-Tools-v1.5.0.zip`, checksum | GitHub release page should match repo contents | No build project here. |

## Files Removed

- Workspace local: `build_log.txt`, `.claude/`, `.vscode/`, `.tools/`.
- Source repo local/generated: `.vs/`, `src/bin/`, `src/obj/`.
- Source repo generated package payload: `dist/AJ Tools.addin`, `dist/AJ Tools.dll`, `dist/Newtonsoft.Json.dll`, `dist/Resources/`, `dist/release/`.
- Source repo probe files: `test.cs`, `test2.cs`.
- Broken unused AI shell files: `src/GeminiShell/Services/AuthenticationService.cs`, `src/GeminiShell/Services/GeminiService.cs`.

## Files Moved Or Renamed

- Files moved: none.
- Files renamed: none.

## Structure Updates

- Added `src/Core/AIRibbonManager.cs`, `src/GeminiShell/**/*.cs`, `src/GeminiShell/Views/GeminiShellView.xaml`, and `src/Resources/AJ_AI.png` to the old-style `.csproj`.
- Added package references required by the AI shell: `AvalonEdit`, `CommunityToolkit.Mvvm`, and `Microsoft.CodeAnalysis.CSharp.Scripting`.
- Added `System.Security` reference for DPAPI key protection.
- Registered the Gemini Shell dockable pane during `OnStartup`.
- Updated `AJ Tools` docs to include the `AJ AI` tab.
- Updated installer README from `v1.4.8` to `v1.5.0`.
- Updated `.gitignore` rules for `desktop.ini`; expanded installer ignore rules for common build/cache items.

## Verification

- Command: `MSBuild.exe AJ Tools.sln /t:Restore;Build /p:Configuration=Debug /p:Platform="Any CPU" /p:SkipRevitAddinDeploy=true /p:SkipAjToolsAutoDeploy=true /v:minimal /m`
- Result: passed.
- Warning remaining: `src/Services/LevelExtents/LevelExtentsService.cs(141,33)` unused variable `sourceType`.
- Revit was not launched or tested. Please test loading in Revit.

## Expected Revit Tabs

- `AJ Tools`
- `AJ Annotation`
- `AJ AI`

## Manual Review

- Existing modified source files were preserved and not refactored.
- AI shell compiles, but API-key flow, generated code execution, and dockable-pane behavior need Revit-side testing.
- Public installer release page should be checked to confirm it matches `releases/AJ-Tools-v1.5.0.zip` and `SHA256SUMS.txt`.

## Intentionally Not Changed

- No individual command business logic was rewritten.
- No source icons were removed; the duplicates removed were generated copies.
- The public installer release ZIP was kept because it is the intentional payload for the installer repository.
