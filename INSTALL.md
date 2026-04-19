# Install and Build

End users should install AJ Tools from the public installer repository:

https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases

This document is for building, packaging, and testing from source.

## Prerequisites

- Windows x64
- Autodesk Revit 2020 installed in the default path
- Visual Studio 2019 or 2022 with .NET desktop build tools
- Access to restore NuGet packages

## Build and Package

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\dist\package.ps1 -Configuration Release
```

The package script builds the add-in, stages the install payload in `dist\`, and creates:

- `dist\release\AJ-Tools-vX.Y.Z.zip`

## Install for Testing

- Current user:

```powershell
.\dist\install.cmd
```

- All users (Administrator):

```powershell
.\dist\install-all-users.cmd
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

## Manual Install

For manual copy/install steps, see [docs/INSTALLATION.md](docs/INSTALLATION.md).
