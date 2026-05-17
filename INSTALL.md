# Install and Build

End users should install AJ Tools from the public installer repository:

https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases

This document is for building, packaging, and testing from source.

AJ Tools is a C# Revit add-in. It is not a pyRevit extension and does not install through pyRevit.

## Prerequisites

- Windows x64
- Autodesk Revit 2020 installed in the default path
- Visual Studio 2019 or 2022 with .NET desktop build tools
- Access to restore NuGet packages
- Windows user account with permission to write to `%APPDATA%`
- Administrator rights only if you want all-users install

## Build and Package

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\dist\package.ps1 -Configuration Release
```

The package script builds the add-in, stages the install payload in `dist\`, and creates:

- `dist\release\AJ-Tools-vX.Y.Z.zip`

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

1. Build Release in Visual Studio or run `dist\package.ps1`.
2. Create add-in payload directory:
   - Current user: `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools`
   - All users: `%PROGRAMDATA%\Autodesk\Revit\Addins\2020\AJ Tools`
3. Copy these into that `AJ Tools` folder:
   - `AJ Tools.dll`
   - dependency DLLs, for example `Newtonsoft.Json.dll`
   - `Resources` folder
4. Create manifest file:
   - Current user path: `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools.addin`
   - All users path: `%PROGRAMDATA%\Autodesk\Revit\Addins\2020\AJ Tools.addin`
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
