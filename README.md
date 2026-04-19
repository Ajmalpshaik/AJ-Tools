# AJ Tools

AJ Tools is the main development repository for the AJ Tools add-in for Autodesk Revit 2020.

This repository owns the source code, build and packaging scripts, internal release preparation, and developer-facing documentation. Public installer downloads are published separately in [AJ-Tools-Installer](https://github.com/Ajmalpshaik/AJ-Tools-Installer).

## Compatibility

- Autodesk Revit 2020
- .NET Framework 4.7.2
- Windows x64

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
- MEP coordination and duct utilities
- family parameter cleanup and conversion
- standards and data management tools

## Repository Layout

- [src/](src/): add-in source code, UI, services, models, and resources
- [dist/](dist/): package creation, install, uninstall, and tag scripts
- [Addin/](Addin/): add-in manifest template
- [docs/](docs/): supporting product and repository documentation
- `AJ Tools.sln`: Visual Studio solution

## Installation

- End users: download the published installer from [AJ-Tools-Installer Releases](https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases)
- Developers and testers: use [INSTALL.md](INSTALL.md) or the detailed guide in [docs/INSTALLATION.md](docs/INSTALLATION.md)

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
- [SECURITY.md](SECURITY.md)
- [docs/FOLDER_STRUCTURE.md](docs/FOLDER_STRUCTURE.md)
- [docs/USAGE.md](docs/USAGE.md)

## Support

- Source and development work stays in this repository
- Download, checksum, and installation issues belong in [AJ-Tools-Installer Issues](https://github.com/Ajmalpshaik/AJ-Tools-Installer/issues)
