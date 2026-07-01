# Project Cleanup Tracker

## 2026-07-01 Full Audit Pass (this session)

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
