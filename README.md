# AJ Tools - Revit 2020 Add-in

AJ Tools is a Revit 2020 add-in that adds productivity tools for graphics cleanup, dimensions, datums, links, and MEP workflows.

![AJ Tools Ribbon Preview](docs/images/aj-tools-ribbon-preview.png)

## Compatibility
- Revit: 2020
- .NET Framework: 4.7.2
- OS: Windows x64

## Quick Start
1. Download `AJ-Tools-vX.Y.Z.zip` from GitHub Releases.
2. Extract the zip.
3. Run `install.cmd` for current user install.
4. Open Revit 2020 and verify the AJ Tools ribbon tab is visible.

For full install options, see [docs/INSTALLATION.md](docs/INSTALLATION.md).

## Installation Options
- Automatic (current user): `install.cmd`
- Automatic (all users): `install-all-users.cmd` (run as Administrator)
- Manual copy/install: documented in [docs/INSTALLATION.md](docs/INSTALLATION.md)

## Build and Package
1. Open `AJ Tools.sln` in Visual Studio 2019/2022.
2. Verify Revit API references:
   - `C:\Program Files\Autodesk\Revit 2020\RevitAPI.dll`
   - `C:\Program Files\Autodesk\Revit 2020\RevitAPIUI.dll`
3. Generate release payload:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\dist\package.ps1 -Configuration Release
   ```
4. Generated zip output:
   - `dist\release\AJ-Tools-vX.Y.Z.zip`

To include debug symbols in package output:
```powershell
powershell -ExecutionPolicy Bypass -File .\dist\package.ps1 -Configuration Release -IncludeSymbols
```

## Project Docs
- Installation: [docs/INSTALLATION.md](docs/INSTALLATION.md)
- Folder map: [docs/FOLDER_STRUCTURE.md](docs/FOLDER_STRUCTURE.md)
- Usage summary: [docs/USAGE.md](docs/USAGE.md)
- Source map: [src/README.md](src/README.md)

## Release Workflow
1. Update assembly version in `src/Properties/AssemblyInfo.cs`.
2. Run `dist/package.ps1` and verify release zip output.
3. Commit and push.
4. Create/push tag in `vX.Y.Z` format:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\dist\create-tag.ps1 -Version X.Y.Z -Push
   ```
5. Create GitHub Release from that tag and upload the generated zip.

## Contact
- Developer: Ajmal P.S.
- LinkedIn: https://www.linkedin.com/in/ajmalps/
- Email: ajmalnattika@gmail.com
