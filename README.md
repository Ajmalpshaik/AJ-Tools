# AJ Tools – Revit & MEP Automation Toolkit

A focused set of Revit 2020 add-ins that streamline graphics cleanup, dimensions, datums, view setup, and MEP production work. Built by Ajmal P.S. for BIM modellers, coordinators, and MEP teams who want fast, consistent tools inside Revit.

## Overview
- Full C# Revit add-in (x64, .NET Framework 4.7.2) packaged for Revit 2020 and compatible with later versions when the Revit API path is updated.
- Tools include graphics resets, linked-model ID helpers, auto dimensions, datum utilities, view range copying, MEP elevation matching, annotation cleanup, and the Filter Pro filter builder.
- pyRevit/Dynamo content is not currently included; the repo structure leaves room for future extensions (`PythonReference/` is a placeholder).
- Intended for BIM modellers, coordinators, and MEP detailers looking for repeatable day-to-day automation.

## Tools Overview

| Tool Name | Type | Description | Revit Version |
|-----------|------|-------------|---------------|
| Filter Pro | Revit Add-in | Multi-category parameter filter builder with 14 rules, naming controls, and optional color overrides or shuffle, applied to active or selected views. | 2020 |
| Element ID Tools (Linked ID Viewer & Search) | Revit Add-in | Inspect a picked host/linked element’s ID and source, or search by Element ID across host and loaded links with zoom/highlight. | 2020 |
| Auto Dimensions | Revit Add-in | Pulldown with Grids Only, Levels Only, or Grids + Levels; places strings based on view crop/scale in plan, section, or elevation. | 2020 |
| Dims by Line | Revit Add-in | Dimensions grids or levels along a user-picked line/range for ad-hoc strings. | 2020 |
| Copy Dim Text | Revit Add-in | Copies Above/Below/Prefix/Suffix text from one dimension to multiple targets. | 2020 |
| Reset to 3D Extents | Revit Add-in | Resets grid/level datum extents back to model (3D) for grids, levels, or both in the active view. | 2020 |
| Flip Grid Bubble | Revit Add-in | Toggle which end of a grid shows the bubble via single picks or window selection. | 2020 |
| Copy View Range | Revit Add-in | Copy the active plan’s view range, cache it, and paste to selected plan views. | 2020 |
| Match Elevation | Revit Add-in | Match the middle elevation from a source pipe/duct/tray/conduit to selected targets. | 2020 |
| Reset Text | Revit Add-in | Reset selected text notes/tags to their default offsets. | 2020 |
| Toggle Links | Revit Add-in | Toggle Revit link visibility in the active view. | 2020 |
| Unhide All | Revit Add-in | Unhide temporarily hidden and hidden elements in the active view. | 2020 |
| Reset Graphics | Revit Add-in | Clear per-element graphic overrides in the active view. | 2020 |
| About | Utility | About dialog with project info and contact links. | N/A |

> Temporarily removed: the Cyber Snake and Neon Defender mini-games have been disabled until their issues are resolved.

## Installation
### Revit add-in (recommended)
1. Close Revit.
2. Use the built `dist/` payload (or build Release yourself).
3. Copy `dist/AJ Tools.dll` and the `dist/Resources/` folder to `%APPDATA%\\Autodesk\\Revit\\Addins\\2020\\AJ Tools`.
4. Place `dist/AJ Tools.addin` in `%APPDATA%\\Autodesk\\Revit\\Addins\\2020` (optionally also `%PROGRAMDATA%\\Autodesk\\Revit\\Addins\\2020` for all users).
5. Launch Revit and look for the **AJ Tools** tab (icons load from `Resources/`).

### Scripted install/uninstall
- Run `dist/install.ps1` from PowerShell to copy the DLL, icons, and manifest (also writes the manifest to ProgramData).
- Run `dist/uninstall.ps1` to remove the files and manifest (cleans legacy `AJ Tools 2020.addin` if present).

### Build and auto-deploy from source
1. Open `AJ Tools.sln` in Visual Studio 2019/2022 (x64, .NET Framework 4.7.2).
2. Verify the Revit 2020 API references point to your install (e.g., `C:\\Program Files\\Autodesk\\Revit 2020\\RevitAPI.dll`).
3. Build Debug/Release. The post-build target copies the DLL/PDB and `Resources/` to `%APPDATA%\\Autodesk\\Revit\\Addins\\2020\\AJ Tools` and writes `AJ Tools.addin`. Set `SkipRevitAddinDeploy=true` if you want to skip the copy.

### pyRevit / Dynamo (future)
- No pyRevit extensions or Dynamo scripts are included yet. When added, copy the extension folder to your pyRevit extensions directory or place `.dyn` files in your Dynamo user folder and open them from Dynamo.

## Usage
### Filter Pro
- **Location:** AJ Tools tab → MEP panel → Filter Pro.
- **Purpose:** Build parameter-based filters quickly, name them consistently, and apply with optional color overrides.
- **Basic Use:** Pick categories → pick a common parameter (values auto-load with search/sort) → choose a rule and naming options → target active or selected views → run **Create Filters**, **Apply to View**, or **Shuffle Colors**.

### Element ID tools
- **Location:** AJ Tools tab → Links panel → Element ID pulldown.
- **Linked ID of Selection:** Pick an element (host or linked) to see its Element ID and source model.
- **View by Linked ID:** Enter an Element ID, search host and/or loaded links, zoom to the result, and optionally highlight it in the active view.

### Dimensions
- **Auto Dims:** AJ Tools tab → Dimensions panel → Auto Dims. Choose Grids Only (plan), Levels Only (section/elevation), or Grids + Levels (plan/section). Requires an active crop box; places strings offset by view scale.
- **Dims by Line:** AJ Tools tab → Dimensions panel → Dims by Line. Pick two points; creates a dimension string across intersecting grids (plan) or levels within the picked range (section/elevation).
- **Copy Dim Text:** AJ Tools tab → Dimensions panel → Copy Dim Text. Pick one source dimension, then targets to copy Above/Below/Prefix/Suffix text.

### Datums
- **Reset to 3D Extents:** AJ Tools tab → Datums panel → Reset to 3D Extents (Grids, Levels, or both). Resets visible datums back to model extents in the active view.
- **Flip Grid Bubble:** AJ Tools tab → Datums panel → Flip Grid Bubble. Pick datums one-by-one or window-select to toggle which end shows the bubble.

### Views and graphics
- **Copy View Range:** AJ Tools tab → Views panel → Copy View Range. Copy the active plan’s view range, then paste to selected plan views from the cached range.
- **Toggle Links / Unhide All / Reset Graphics:** AJ Tools tab → Graphics panel. Toggle link visibility, unhide hidden/temporary elements, or clear per-element overrides in the active view.

### MEP and annotations
- **Match Elevation:** AJ Tools tab → MEP panel. Pick a source pipe/duct/tray/conduit, then targets to match the middle elevation.
- **Reset Text:** AJ Tools tab → Annotations panel. Select text notes/tags first, then run to reset offsets.

### Info
- **About:** AJ Tools tab → Info panel. Shows project details and LinkedIn contact link.

## Requirements
- Revit 2020 (built and tested); works on Windows x64. Update RevitAPI references for newer versions.
- .NET Framework 4.7.2.
- RevitAPI and RevitAPIUI assemblies available from the Revit install.
- pyRevit/Dynamo: not required (no extensions included yet).

## Development
- Solution: `AJ Tools.sln` with a single C# Revit add-in project `src/AJ Tools.csproj` (WPF/WinForms UI mixed).
- Key folders: `src/Commands` (commands and forms), `src/Services` (logic like Auto Dimensions, Filter Pro helpers, ribbon setup), `src/AJTools/LinkedTools` (linked-model utilities), `src/Resources` (icons), `dist` (packaged output and scripts).
- Build with Visual Studio 2019/2022 (x64). Respect the Revit API version, and keep metadata headers (Tool Name, Description, etc.) current.
- Follow existing style, keep transactions scoped, and write clear commit messages. Use `SkipRevitAddinDeploy=true` if you do not want MSBuild to copy into your Revit add-ins folder during development.

## Versioning
- Each source file carries metadata headers with tool name, description, author, version, last updated date, and Revit version. Assembly version lives in `src/Properties/AssemblyInfo.cs` (currently `1.2.0.0`).
- Major updates noted here:
  - **v1.2.0 (Dec 2025):** Metadata headers standardized across the codebase, linked ID/search UI refreshed, Filter Pro marked production-ready, packaging/scripts retained.

## License & Credits
- License: Add your preferred license here (for example, MIT).
- Developed by **Ajmal P.S.** – Mechanical/MEP BIM Specialist based in Qatar.

## Contact
- LinkedIn: [Ajmal P.S.](https://www.linkedin.com/in/ajmalps/)
- Email: ajmalnattika@gmail.com

This README was last updated programmatically to reflect the current structure and tools in this repository.
