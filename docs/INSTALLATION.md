# Installation Guide

AJ Tools supports Revit 2020 on Windows x64.

## Prerequisites
- Autodesk Revit 2020
- Windows user account with permission to write to `%APPDATA%`
- Administrator rights only if you want all-users install

## Option A: Automatic Install (Recommended)

### From GitHub Release
1. Download `AJ-Tools-vX.Y.Z.zip` from the public installer repository Releases page:
   - https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases
2. Extract the zip.
3. Run one of these scripts from the extracted folder:
   - `install.cmd` for current user
   - `install-all-users.cmd` for current user + all users (run as Administrator)

> Note: Installer now unblocks downloaded files automatically to avoid Revit `0x80131515` / `FileLoadException` errors on other systems.

### From Source Repository
1. Open PowerShell in repository root.
2. Generate payload:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\dist\package.ps1 -Configuration Release
   ```
3. Install:
   ```powershell
   .\dist\install.cmd
   ```

## Option B: Manual Install
1. Build Release in Visual Studio or run `dist\package.ps1`.
2. Create add-in payload directory:
   - Current user: `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools`
   - All users: `%PROGRAMDATA%\Autodesk\Revit\Addins\2020\AJ Tools`
3. Copy these into that `AJ Tools` folder:
   - `AJ Tools.dll`
   - dependency DLLs (for example `Newtonsoft.Json.dll`)
   - `Resources` folder
4. Create manifest file:
   - Path: `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools.addin` (or `%PROGRAMDATA%\...` for all users)
   - Use `Addin\AJ Tools.addin` as template
   - Ensure `<Assembly>` points to the DLL path you copied
5. If files were downloaded from internet, unblock copied files before launching Revit:
   ```powershell
   Get-ChildItem "$env:APPDATA\Autodesk\Revit\Addins\2020\AJ Tools" -Recurse -File | Unblock-File
   Unblock-File "$env:APPDATA\Autodesk\Revit\Addins\2020\AJ Tools.addin"
   ```

## Uninstall
- Current user uninstall:
  ```powershell
  .\dist\uninstall.cmd
  ```
- Current user + all users uninstall (Administrator):
  ```powershell
  .\dist\uninstall-all-users.cmd
  ```
