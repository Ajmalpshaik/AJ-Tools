# AJ Tools

AJ Tools is the main development repository for the AJ Tools add-in for Autodesk Revit 2020.

This repository owns the source code, build and packaging scripts, internal release preparation, and developer-facing documentation. Public installer downloads are published separately in [AJ-Tools-Installer](https://github.com/Ajmalpshaik/AJ-Tools-Installer).

## Compatibility

- Autodesk Revit 2020
- .NET Framework 4.7.2
- Windows x64

The add-in is built against the Revit 2020 API. Later Revit versions require Revit-side validation before publishing a separate compatible installer.

## Technology Stack

- C#
- .NET Framework 4.7.2
- Autodesk Revit 2020 API (`RevitAPI`, `RevitAPIUI`)
- WPF/XAML
- `Newtonsoft.Json`

## Product Scope

AJ Tools provides commands for:

- graphics cleanup and overrides
- linked model lookup and workset utilities
- dimension and datum workflows
- annotation and tagging helpers
- AJ Annotation duct reference dimension tools
- MEP coordination and duct utilities
- family parameter cleanup and conversion
- standards and data management tools

## Repository Layout

- [src/](src/): add-in source code, UI, services, models, and resources
- [dist/](dist/): package creation, install, uninstall, and tag scripts
- [Addin/](Addin/): add-in manifest template
- [docs/](docs/): supporting product and repository documentation
- `AJ Tools.sln`: Visual Studio solution

## Revit Ribbon Tabs

- `AJ Tools`: main tool tab for view, graphics, dimensions, annotation, MEP, data, purge, and family utilities
- `AJ Annotation`: separate annotation tab for duct reference dimension workflows

## Installation

- End users: download the published installer from [AJ-Tools-Installer Releases](https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases)
- Developers and testers: use [INSTALL.md](INSTALL.md)

## Build From Source

1. Install Autodesk Revit 2020 on the build machine.
2. Open `AJ Tools.sln` in Visual Studio 2019 or 2022.
3. Confirm the Revit API DLLs exist at:
   - `C:\Program Files\Autodesk\Revit 2020\RevitAPI.dll`
   - `C:\Program Files\Autodesk\Revit 2020\RevitAPIUI.dll`
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
