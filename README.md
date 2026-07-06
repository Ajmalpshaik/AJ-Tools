# AJ Tools

AJ Tools is the main development repository for the AJ Tools add-in for Autodesk Revit.

Current suite version: **1.11.1**

This repository owns the source code, build and packaging scripts, internal release preparation, and developer-facing documentation. Public installer downloads are published separately in [AJ-Tools-Installer](https://github.com/Ajmalpshaik/AJ-Tools-Installer).

## Repository Type

- C# Revit add-in source repository
- WPF/XAML user interfaces
- Revit 2020-2024 API-specific .NET Framework package targets
- Revit 2025-2026 modern .NET 8 package target
- Revit 2027 modern .NET 10 package target
- No pyRevit `.extension`, `.tab`, `.panel`, or `.pushbutton` structure is present in this repository

## Compatibility

- Autodesk Revit 2020-2027 installer support
- .NET Framework 4.7.2 for Revit 2020
- .NET Framework 4.8 for Revit 2021-2024
- .NET 8 for Revit 2025-2026
- .NET 10 for Revit 2027
- Windows x64

The release package contains versioned payloads so the installer deploys the matching AJ Tools build for each Revit runtime and API generation.

## Technology Stack

- C#
- .NET Framework 4.7.2, .NET Framework 4.8, .NET 8, and .NET 10
- Autodesk Revit API references for Revit 2020-2027 package builds
- WPF/XAML
- `Newtonsoft.Json`
- `AvalonEdit`, `CommunityToolkit.Mvvm`, and Roslyn scripting for the AI shell

## Product Scope

AJ Tools provides commands for:

- view, crop, colorize, filter, and link visibility control
- graphics override apply, match, and reset
- grid and level datum extents and bubble control
- MEP elevation matching, level reassignment, and element pinning
- MEP connection, ceiling-grid snapping, opening coordination, HVAC schematic generation, and domestic water pipe sizing
- element ID lookup across host and linked models, workset 3D views, and link workset assignment
- location data assignment and duct standards calculation
- view template transfer and purge utilities (unplaced views, family parameters)
- shared-to-family parameter conversion
- automatic and quick dimensioning of grids, levels, and ducts
- duct flow annotations, revision clouds, and text-note copy/swap
- intelligent MEP tagging, tag rearranging, and L-shape leaders
- a built-in AI Assistant (Gemini C# Shell)

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
  - **View** - View Crop, Unhide All, Toggle Link, Filter Pro, Colorize, Section Mark Visibility
  - **Graphics** — Apply Graphics, Match Graphics, Reset Graphics
  - **Datums** — Reset Grid/Level Extents to 3D, Modify Level Extents, Flip Grid/Level Bubbles
  - **Modify** — Match MEP Element Elevation, Reassign Reference Level, Pin/Unpin Elements
  - **MEP** - Connect MEP Elements, Elements to Ceiling Grid, MEP Openings, HVAC Schematic, Pipe Sizing
  - **Coordination** — Element ID lookup, 3D Views by Workset, Link Workset
  - **Data** — Assign Location, Duct Standard
  - **Manage** — Transfer View Templates, Purge (unplaced 3D views, unplaced sections, family parameters)
  - **Family** — Shared to Family
  - **AI Assistant** — AJ AI (Gemini C# Shell)
  - **About**
- `AJ Annotation`: separate annotation tab with the following panels:
  - **Auto Dimention** — Auto Duct Dimension (single duct to wall, all duct to wall)
  - **Dimensions** — Automatic Dimension, Quick Dimension, Copy Dimension Text
  - **Annotation** — Duct Flow Annotations, Revision Clouds, Copy/Swap Text Notes
  - **Family** — Center Annotation
  - **Tags** — Smart MEP Tags, Rearrange Tags, L-Shape Leader

The AI shell is delivered as the **AI Assistant** panel on the `AJ Tools` tab plus a dockable **Gemini Shell** pane — it is not a separate ribbon tab.

## Installation

- End users: download the published installer from [AJ-Tools-Installer Releases](https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases)
- Developers and testers: use [INSTALL.md](INSTALL.md)

## Build From Source

1. Install Visual Studio 2022 with the .NET Framework 4.7.2 and 4.8 targeting packs.
2. Install the .NET 8 SDK and .NET 10 SDK, or keep the local `.dotnet-sdk` folder populated for packaging.
3. Install Autodesk Revit 2027, or provide `RevitInstallDir` for Revit 2027 API references.
4. Package the release payload:

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
