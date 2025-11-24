# AJ Tools

Revit 2020 add-in that bundles quick helpers. Loading the DLL adds an **AJ Tools** ribbon tab with these panels and tools.

## Panels and commands

### Graphics
- **Toggle Links** - Show or hide every Revit Link category in the active view.
- **Unhide All** - Unhide permanently hidden elements in the active view.
- **Reset Graphics** - Clear per-element override graphics applied in the view.

### Dimensions
- **Auto Dims** (pulldown; requires a cropped plan/section/elevation):
  - **Grids Only** - Plan views; creates individual and overall strings for horizontal and vertical grids.
  - **Levels Only** - Sections/elevations; stacks level strings and an overall string for all visible levels.
  - **Grids + Levels** - Plan views run the grid workflow; sections/elevations place both level and grid strings.
- **Dims by Line** (pulldown):
  - **Grids by Line** - Pick two points to define a line; parallel grids that intersect the line are dimensioned with one string perpendicular to the grids.
  - **Levels by Line** - Section/elevation only; pick start/end points to set the vertical range and place a level dimension string through that range.
- **Copy Dim Text** - Copy Above/Below/Prefix/Suffix from one dimension and paste to many targets (ESC to finish).

### Datums
- **Reset to 3D Extents** (pulldown):
  - **Grids Only** - Reset visible grid datum ends back to 3D extents in the view.
  - **Levels Only** - Reset visible level datum ends (End0/End1/End2/End3 when available) to 3D.
  - **Grids + Levels** - Run both reset routines in one click.
- **Flip Grid Bubble** - Pick grids one by one to flip which end shows the bubble; keeps one bubble visible.

### Views
- **Copy View Range** - Copy the active plan view's range and paste it to selected plan views using a quick picker.

### MEP
- **Match Elevation** - Copy middle elevation from a source MEP curve (pipe/duct/tray/conduit/flex) and apply to others while preserving slope.

### Annotations
- **Reset Text** - Reset selected text notes/tags back to their default text offset.

### Fun
- **Cyber Snake** - Launch a quick Snake mini-game (Windows Forms) for a break.

### Info
- **About** - Shows author info and version details.

## Requirements
- Revit 2020 at runtime.
- .NET Framework 4.7.2.
- RevitAPI.dll and RevitAPIUI.dll available (the project references the default Revit 2020 install path).

## Building
1. Open `AJ Tools/AJ Tools.sln` in Visual Studio 2017 or later.
2. Verify the Revit 2020 API references point to your installation folder.
3. Build the **Release** configuration; the DLL, icons, and install scripts land in `AJ Tools/AJ Tools/bin/Release`.

## Installing
- Option A (one-click): from a built or release ZIP folder, run `install.cmd` (or `install.ps1`). The script copies `AJ Tools.dll`, PNG icons, and generates `AJ Tools.addin` in `%APPDATA%\\Autodesk\\Revit\\Addins\\2020\\AJ Tools`.
- Option B (manual):
  - Copy `AJ Tools.dll` and all PNGs into `%APPDATA%\\Autodesk\\Revit\\Addins\\2020\\AJ Tools`.
  - Copy `AJ Tools.addin` into `%APPDATA%\\Autodesk\\Revit\\Addins\\2020`.
  - Restart Revit 2020 and look for the **AJ Tools** tab.
