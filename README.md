# AJ Tools

AJ Tools is the main development repository for the AJ Tools add-in for Autodesk Revit. One codebase
builds for Revit 2020 through 2027; Revit 2020 is the tested baseline and the version the published
installer currently ships.

Current suite version: **1.23.1** (as reported by `src/Properties/AssemblyInfo.cs` — see
[CHANGELOG.md](CHANGELOG.md) for the release-numbering note)

This repository owns the source code, build and packaging scripts, internal release preparation, and developer-facing documentation. Public installer downloads are published separately in [AJ-Tools-Installer](https://github.com/Ajmalpshaik/AJ-Tools-Installer).

## Repository Type

- C# Revit add-in source repository
- WPF/XAML user interfaces
- Multi-version Revit API target: 2020 → 2027 from the single `src/` codebase
- No pyRevit `.extension`, `.tab`, `.panel`, or `.pushbutton` structure is present in this repository

## Compatibility

One codebase, one build configuration per Revit version (`Directory.Build.props` maps them):

| Build configuration | Revit version | Framework |
| --- | --- | --- |
| `Release` / `Debug` | 2020 (tested baseline) | .NET Framework 4.7.2 |
| `Release R21` … `R24` | 2021–2024 | .NET Framework 4.8 |
| `Release R25` / `R26` | 2025–2026 | .NET 8 |
| `Release R27` | 2027 | .NET 10 |

Windows x64 in all cases. Revit 2020 is the build the published installer ships and the one validated
inside Revit; the 2021–2027 builds compile clean but require Revit-side validation before publishing a
compatible installer.

## Technology Stack

- C#
- .NET Framework 4.7.2 / 4.8, .NET 8, .NET 10 (per Revit version — see Compatibility)
- Autodesk Revit API via NuGet (`Nice3point.Revit.Api.RevitAPI` / `RevitAPIUI`, version-matched per configuration)
- WPF/XAML
- `Newtonsoft.Json`
- `AvalonEdit`, `CommunityToolkit.Mvvm`, and Roslyn scripting for the AI shell

## Product Scope

AJ Tools provides commands for:

- view, crop, filter, and link visibility control
- graphics override apply, match, and reset
- grid and level datum extents and bubble control
- MEP elevation matching, level reassignment, and element pinning
- MEP connection, ceiling-grid snapping, HVAC schematic generation, and domestic water pipe sizing
- element ID lookup across host and linked models, workset 3D views, and link workset assignment
- location data assignment and duct standards calculation
- view template transfer and purge utilities (unplaced views, family parameters)
- shared-to-family parameter conversion
- automatic and quick dimensioning of grids, levels, and ducts
- duct flow annotations, revision clouds, and text-note copy/swap
- intelligent MEP tagging, tag rearranging, and L-shape leaders
- a built-in AI Assistant (AJ AI)

## Repository Layout

- [src/](src/): add-in source code, UI, services, models, and resources
- [dist/](dist/): package creation, install, uninstall, and tag scripts
- [Addin/](Addin/): add-in manifest template
- [docs/](docs/): supporting product and repository documentation
- `AJ Tools.sln`: Visual Studio solution

Generated build outputs such as `src/bin`, `src/obj`, `dist/release`, packaged DLLs, and release zip files are intentionally ignored and should not be committed.

## Revit Ribbon Tabs

The add-in registers **two** ribbon tabs:

- `AJ Tools`: main tool tab with the following panels:
  - **View** — View Crop, Unhide All, Toggle Link, Filter Pro, Section Mark Visibility
  - **Graphics** — Apply Graphics, Match Graphics, Reset Graphics
  - **Datums** — Reset Grid/Level Extents to 3D, Modify Level Extents, Flip Grid/Level Bubbles
  - **Modify** — Match MEP Element Elevation, Reassign Reference Level, Pin/Unpin Elements
  - **MEP** — Connect MEP Elements, Elements to Ceiling Grid, HVAC Schematic, Pipe Sizing
  - **Coordination** — Element ID lookup, 3D Views by Workset, Link Workset
  - **Data** — Assign Location, Duct Standard
  - **Manage** — Transfer View Templates, Purge (unplaced 3D views, unplaced sections, family parameters)
  - **Family** — Shared to Family
  - **AI Assistant** — AJ AI
  - **About**
- `AJ Annotation`: separate annotation tab with the following panels:
  - **Auto Dimention** — Auto Duct Dimension (single duct to wall, all duct to wall)
  - **Dimensions** — Automatic Dimension, Quick Dimension, Copy Dimension Text
  - **Annotation** — Duct Flow Annotations, Revision Clouds, Copy/Swap Text Notes
  - **Text** — Arrange Text in Box
  - **Family** — Center Annotation
  - **Tags** — Smart MEP Tags, Rearrange Tags, L-Shape Leader

The AI shell is delivered as the **AI Assistant** panel on the `AJ Tools` tab plus a dockable **AJ AI** pane — it is not a separate ribbon tab.

## Installation

- End users: download the published installer from [AJ-Tools-Installer Releases](https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases)
- Developers and testers: use [INSTALL.md](INSTALL.md)

## Build From Source

No local Revit installation is needed to compile — the Revit API comes from NuGet and is restored per
configuration.

1. Open `AJ Tools.sln` in Visual Studio 2022 (the .NET 9 SDK covers the 2020–2026 configs; the
   `Release R27` config needs the .NET 10 SDK, shipped with VS 2022 17.12+).
2. Pick the configuration for the Revit version you want (see Compatibility) and build.
   Note: **a normal build auto-deploys the add-in into the local Revit Addins folders** for that
   version — pass `-p:SkipAjToolsAutoDeploy=true` for a compile-only build.
3. Package the release payload (packages the `Release` / Revit 2020 build):

```powershell
powershell -ExecutionPolicy Bypass -File .\dist\package.ps1 -Configuration Release
```

Expected output:

- `dist\release\AJ-Tools-vX.Y.Z.zip`

## Release Ownership

- `AJ Tools`: source code, assembly version, package creation, source changelog
- `AJ-Tools-Installer`: public zip download, checksum, release page, installer support

## Repository Docs

- [INSTALL.md](INSTALL.md)
- [RELEASE_PROCESS.md](RELEASE_PROCESS.md)
- [CHANGELOG.md](CHANGELOG.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [SUPPORT.md](SUPPORT.md)
- [SECURITY.md](SECURITY.md)
- [docs/USAGE.md](docs/USAGE.md)

## Support

- Source defects, feature requests, and development work stay in this repository
- Download, checksum, installation, and uninstall issues belong in [AJ-Tools-Installer Issues](https://github.com/Ajmalpshaik/AJ-Tools-Installer/issues)
- Support routing and reporting rules: [SUPPORT.md](SUPPORT.md)
