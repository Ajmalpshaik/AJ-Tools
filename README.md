# AJ Tools - Revit Add-in Toolkit

AJ Tools is a Revit 2020 add-in that provides ribbon tools for graphics cleanup, dimensioning, datums, and MEP production workflows.

## Supported Versions
- Revit 2020 (built and tested)
- .NET Framework 4.7.2
- Windows x64
- For newer Revit versions, update RevitAPI/RevitAPIUI references and rebuild.

## Installation
### Option A: Manual install
1. Build a Release DLL (or use a release package that includes `AJ Tools.dll` and `Resources`).
2. Create `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools`.
3. Copy `AJ Tools.dll` and the `Resources` folder into that folder.
4. Copy `dist/AJ Tools.addin` to `%APPDATA%\Autodesk\Revit\Addins\2020`.
5. Open the `.addin` file and make sure `<Assembly>` points to `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools\AJ Tools.dll`.
6. For all users, use `%PROGRAMDATA%\Autodesk\Revit\Addins\2020` instead of `%APPDATA%`.

### Option B: Scripted install
- Place a built `AJ Tools.dll` (and optional `AJ Tools.pdb`) in `dist/`.
- Run `dist/install.ps1` (or `dist/install.cmd`) to copy files to both user and all-users add-in folders.
- Run `dist/uninstall.ps1` (or `dist/uninstall.cmd`) to remove the add-in.

## Build
1. Open `AJ Tools.sln` in Visual Studio 2019 or 2022.
2. Confirm Revit API references point to `C:\Program Files\Autodesk\Revit 2020\RevitAPI.dll` and `C:\Program Files\Autodesk\Revit 2020\RevitAPIUI.dll`.
3. Build x64 Debug or Release.
4. A post-build target deploys to `%PROGRAMDATA%\Autodesk\Revit\Addins\2020\AJ Tools` by default. Set `SkipRevitAddinDeploy=true` to skip deployment.

## Tool List (AJ Tools Ribbon)
### Graphics
- Toggle Links - Toggle visibility of Revit links in the active view.
- Unhide All - Clear temporary hide/isolate and unhide hidden elements in the active view.
- Reset Graphics - Clear per-element graphic overrides in the active view.

### Links
- Linked ID of Selection - Inspect Element ID and source for a picked host or linked element.
- View by Linked ID - Search by Element ID across host and loaded links and zoom to the element.

### Dimensions
- Auto Dims - Grids Only, Levels Only, or Grids + Levels.
- Dim By Line - Grid Only or Level Only along a picked line.
- Copy Dim Text - Copy Above/Below/Prefix/Suffix text between dimensions.

### Datums
- Reset to 3D Extents - Grids Only, Levels Only, or Grids + Levels.
- Flip Grid Bubble - Toggle which end of a grid shows the bubble.

### MEP
- Match Elevation - Match middle elevation from a source pipe, duct, cable tray, conduit, flex duct, or flex pipe to targets.
- Flow Direction - Place flow direction annotations and manage settings.
- Filter Pro - Build parameter filters and apply them to the active view.

### Annotations
- L-Shape Leader - Force right-angle tag leaders and flip elbow direction.
- Reset Text - Reset text notes/tags to default offsets.
- Copy Swap Text - Copy or swap text values between text notes.

### Info
- About - Add-in info and contact.

## Notes and Limitations
- Commands run only in non-template project views.
- Auto Dims requires Crop View to be enabled and works only in plan, section, or elevation views.
- Flow Direction supports ducts and pipes with valid connectors; select an annotation family in Settings first.
- Filter Pro only exposes Revit filterable categories/parameters; large models may limit value scanning.
- View templates or locked view settings can block visibility/override changes.

## Repository Layout
- `src/` contains the add-in source code. See `src/README.md` for the folder map.
- `dist/` contains install scripts and packaging assets.

## Versioning and Changelog
- Assembly version is defined in `src/Properties/AssemblyInfo.cs` (current: 1.2.0.0).
- No `CHANGELOG.md` is included yet.

## Credits and Contact
- Developed by Ajmal P.S.
- LinkedIn: https://www.linkedin.com/in/ajmalps/
- Email: ajmalnattika@gmail.com
