# AJ Tools

AJ Tools is the main development repository for the AJ Tools add-in for Autodesk Revit. One codebase
builds for Revit 2020 through 2027, and the published installer carries a build for each of those
versions. Revit 2020 is the most-tested baseline.

Current suite version: **1.57.1** (as reported by `src/Properties/AssemblyInfo.cs` — see
[CHANGELOG.md](CHANGELOG.md))

This repository owns the source code, build and packaging scripts, internal release preparation, and developer-facing documentation. Public installer downloads are published separately in [AJ-Tools-Installer](https://github.com/Ajmalpshaik/AJ-Tools-Installer).

## Repository Type

- C# Revit add-in source repository
- WPF/XAML user interfaces
- Multi-version Revit API target: 2020 → 2027 from the single `src/` codebase

## Compatibility

One codebase, one build configuration per Revit version (`Directory.Build.props` maps them):

| Build configuration | Revit version | Framework |
| --- | --- | --- |
| `Release` / `Debug` | 2020 (tested baseline) | .NET Framework 4.7.2 |
| `Release R21` … `R24` | 2021–2024 | .NET Framework 4.8 |
| `Release R25` / `R26` | 2025–2026 | .NET 8 |
| `Release R27` | 2027 | .NET 10 |

Windows x64 in all cases. The published installer ships all eight builds (one `Payload\<year>` folder
each) and installs whichever Revit versions are on the machine. Revit 2020 is the version used day to day
and the most tested inside Revit; the other versions build clean and are checked against each version's
Revit API, but get less hands-on testing.

## Technology Stack

- C#
- .NET Framework 4.7.2 / 4.8, .NET 8, .NET 10 (per Revit version — see Compatibility)
- Autodesk Revit API via NuGet (`Nice3point.Revit.Api.RevitAPI` / `RevitAPIUI`, version-matched per configuration)
- WPF/XAML
- `Newtonsoft.Json`
- `AvalonEdit`, `CommunityToolkit.Mvvm`, and Roslyn scripting for the C# pane

## Product Scope

AJ Tools provides commands for:

- view, crop, filter, and link visibility control
- graphics override apply, match, and reset
- grid and level datum extents and bubble control
- MEP elevation matching, level reassignment, and element pinning
- MEP connection, ceiling-grid snapping, HVAC schematic generation, and domestic water pipe sizing
- element ID lookup across host and linked models, workset 3D views, and link workset assignment
- location data assignment and duct standards calculation
- transfer of view templates, schedules, legends, and drafting views between open projects
- purge utilities for unplaced views (3D, sections, schedules, legends, drafting views), unused
  view templates, filters, and group types, and family parameters
- shared-to-family parameter conversion
- automatic and quick dimensioning of grids, levels, and ducts
- duct flow annotations, revision clouds, and text-note copy/swap
- intelligent MEP tagging, tag creation and stacking, tag clash fixing, tag rearranging, and L-shape
  leaders
- a built-in "C#" pane: script editor and runner with an AI chat that writes C# for the open model
- Game Mode: a first-person walkthrough inside a real Revit perspective view, with element
  identification, temporary hiding, snag marking, and selection carried back into Revit
- Quick Menu: a customisable radial tool wheel that opens around the mouse pointer and runs any
  AJ Tools ribbon button or Revit built-in command

## Repository Layout

- [src/](src/): add-in source code, UI, services, models, and resources
- [dist/](dist/): package creation, install, uninstall, and tag scripts
- [Addin/](Addin/): add-in manifest template
- [docs/](docs/): supporting product and repository documentation
- `AJ Tools.sln`: Visual Studio solution

Generated build outputs such as `src/bin`, `src/obj`, `dist/release`, packaged DLLs, and release zip files are intentionally ignored and should not be committed.

## Revit Ribbon Tabs

The add-in registers **two** ribbon tabs: `AJ Tools` (view, graphics, datum, MEP, coordination, data,
manage, family, AI Assistant, Game and Quick Menu panels) and `AJ Annotation` (dimensions, annotation,
text, family and tags panels). The full panel-by-panel tool list lives in one place only,
[docs/USAGE.md](docs/USAGE.md), so it cannot drift out of step with a second copy here.

The C# pane is opened from the **AI Assistant** panel on the `AJ Tools` tab and docks inside Revit — it
is not a separate ribbon tab.

## Installation

- End users: download the published installer from [AJ-Tools-Installer Releases](https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases)
- Developers and testers: use [INSTALL.md](INSTALL.md)

## Build From Source

No local Revit installation is needed to compile — the Revit API comes from NuGet and is restored per
configuration.

1. Install the .NET 10 SDK (it builds every configuration, 2020–2027). Visual Studio 2026 (18.x) works
   too; older Visual Studio versions cannot build the `Release R27` (.NET 10) configuration.
2. Build the configuration for the Revit version you want (see Compatibility), for example:

   ```powershell
   dotnet build "AJ Tools.sln" -c "Release R24" -p:Platform=x64
   ```

   Note: **a normal build auto-deploys the add-in into your own Revit Addins folder** (AppData) for
   that version — add `-p:SkipAjToolsAutoDeploy=true` for a compile-only build.
3. Package the release payload (builds all eight configurations into `dist\Payload\<year>`):

```powershell
powershell -ExecutionPolicy Bypass -File .\dist\package.ps1
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
