# Install and Build

End users should install AJ Tools from the public installer repository:

https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases

This document is for building, packaging, and testing from source.

AJ Tools is a compiled C# Revit add-in; it installs with its own scripts, described below.

## Prerequisites

- Windows x64
- The .NET 10 SDK (builds every configuration, Revit 2020–2027), or Visual Studio 2026 (18.x). Older
  Visual Studio versions cannot build the Revit 2027 (.NET 10) configuration. No local Revit install is
  needed to *build* — the Revit API is restored from NuGet; Revit is only needed to *run* the add-in.
- Access to restore NuGet packages
- Windows user account with permission to write to `%APPDATA%`
- Administrator rights only if you want all-users install

## Build and Package

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\dist\package.ps1
```

The package script builds all eight configurations (Revit 2020–2027), stages one install payload per
Revit version in `dist\Payload\<year>`, and creates:

- `dist\release\AJ-Tools-vX.Y.Z.zip`

The install scripts install the matching payload for every Revit version found in the package. A
normal single-configuration build (see [README.md](README.md)) also auto-deploys that version into
your own Revit Addins folder unless you pass `-p:SkipAjToolsAutoDeploy=true`.

## Install From GitHub Release

1. Download `AJ-Tools-vX.Y.Z.zip` from the public installer repository releases page.
2. Extract the zip.
3. Run one of these scripts from the extracted folder:
   - `install.cmd` for current user
   - `install-all-users.cmd` for current user + all users (run as Administrator)

The installer unblocks downloaded files automatically to avoid Revit `0x80131515` / `FileLoadException` errors.

## Install From Source

- Current user:

```powershell
.\dist\install.cmd
```

- All users (Administrator):

```powershell
.\dist\install-all-users.cmd
```

## Manual Install

1. Build the configuration for your Revit version, or run `dist\package.ps1`.
2. Create add-in payload directory (paths below say `2020` — use the matching year folder if you
   built a different version's configuration):
   - Current user: `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools`
   - All users, Revit 2020–2026: `%PROGRAMDATA%\Autodesk\Revit\Addins\2020\AJ Tools`
   - All users, Revit 2027 and later: `C:\Program Files\Autodesk\Revit\Addins\2027\AJ Tools`
     (Revit 2027 moved the all-users add-in folder there; it needs Administrator rights)
3. Copy **every file** from the build output (or `Payload\<year>` in the package) into that `AJ Tools`
   folder, plus the `Resources` folder. That includes every dependency DLL and, for Revit 2025 and
   later, `AJ Tools.deps.json` — the add-in will not load without it on .NET 8/10.
4. Create manifest file:
   - Current user path: `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools.addin`
   - All users path: the all-users folder from step 2, one level up (`...\Addins\<year>\AJ Tools.addin`)
   - Use `Addin\AJ Tools.addin` as the template
   - Ensure `<Assembly>` points to the DLL path you copied
5. If files were downloaded from the internet, unblock copied files before launching Revit:

```powershell
Get-ChildItem "$env:APPDATA\Autodesk\Revit\Addins\2020\AJ Tools" -Recurse -File | Unblock-File
Unblock-File "$env:APPDATA\Autodesk\Revit\Addins\2020\AJ Tools.addin"
```

## Uninstall

- Current user:

```powershell
.\dist\uninstall.cmd
```

- All users (Administrator):

```powershell
.\dist\uninstall-all-users.cmd
```
