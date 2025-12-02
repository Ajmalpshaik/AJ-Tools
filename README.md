# AJ Tools for Autodesk Revit (2020+)

AJ Tools is a productivity pack for Autodesk Revit (.NET Framework 4.7.2). It ships a modern Filter Pro module plus a set of utilities for graphics, dimensions, datums, views, annotations, and MEP workflows.

## Key Features
- **Filter Pro:** create/update view filters from parameter values (multi-category; built-in, shared, project params).
- **Stable ordering:** new filters insert above existing ones while preserving prior order, overrides, and visibility.
- **Color & patterns:** apply projection/cut colors, solid-fill patterns, and halftone per filter; optional random palette.
- **Robust UI:** WPF interface with search, sorting, live name preview, prefix/suffix/separator options.
- **Rules:** 14 rule types (equals/not, contains/not, begins/ends with/not, numeric compares, has value/no value).
- **Other tools (ribbon panels):**
  - **Graphics:** Toggle Revit Links, Unhide All, Reset Graphics.
  - **Dimensions:** Auto Dimensions (grids/levels), Dimension by Line (grids/levels), Copy Dimension Text.
  - **Datums:** Reset Grids/Levels to 3D, Reset Datums combined, Flip Grid Bubble.
  - **Views:** Copy View Range between plan views.
  - **MEP:** Match Elevation, Filter Pro.
  - **Annotations:** Reset Text Position.
  - **Fun/Info:** Snake game, Neon Defender, About.

## Project Structure
- `src/` – source code  
  - `Commands/` – Revit external commands (`CmdFilterPro`, `CmdFilterProAvailability`, etc.)  
  - `FilterProWindow.xaml` / `.xaml.cs` – WPF UI  
  - `FilterProHelper.cs` – filter creation, overrides, ordering  
  - `Images/` – ribbon icons  
  - `AJ Tools.csproj` – project definition  
- `dist/` – addin manifest and deployment scripts

## Requirements
- Autodesk Revit 2020 or later (tested on 2020)  
- .NET Framework 4.7.2  
- Visual Studio 2019/2022

## Build
1. Open `src/AJ Tools.csproj` in Visual Studio.
2. Build Debug or Release. Output: `src/bin/<Config>/AJ Tools.dll`.
3. Optional auto-deploy: the csproj includes a target that copies the DLL/icons to `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools`. Set `SkipRevitAddinDeploy=false` to enable; close Revit to avoid file locks.

## Manual Install
1. Copy `AJ Tools.dll` and the `Images/` folder to `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools`.
2. Add an `.addin` manifest pointing to `AJ Tools.dll` (the build target can generate one).

## Using Filter Pro
1. Launch **Filter Pro** from the AJ Tools ribbon (MEP panel).
2. **Selection:** pick categories (multi-select), choose a common parameter, load values (search/sort supported).
3. **Configuration:** choose a rule; set prefix/suffix/separator; toggle include category/parameter; preview name.
4. **Actions:**
   - **Create Filters:** create only.  
   - **Apply to View:** create and apply to the active/selected views.  
   - **Shuffle Colors:** create/apply with random colors.  
   - **Override Existing:** update existing filters with the same name.

### Ordering & Overrides
- Before creation, the tool snapshots the current filter order per target view.  
- New/updated filters are added to the top; existing filters keep their captured order.  
- Visibility and graphic overrides are preserved for existing filters; new filters default to visible.

### Supported Parameters & Rules
- Parameters: built-in, shared, and project; storage types: String, Integer, Double, ElementId.  
- Rules: Equals, Not Equals, Contains, Does Not Contain, Begins With, Does Not Begin With, Ends With, Does Not End With, Greater, Greater Or Equal, Less, Less Or Equal, Has Value, Has No Value.

## Troubleshooting
- Build fails due to locked files in `%APPDATA%\Autodesk\Revit\Addins\2020`: close Revit or set `SkipRevitAddinDeploy=true`.
- No values load: ensure the parameter exists on the selected categories and has data.
- Filters apply but no colors: verify the view allows graphics overrides and filter visibility is on.

## Contributing
- Fork, create a feature branch, and run builds/tests in Revit.  
- Include clear descriptions and (for UI changes) screenshots in PRs.

## License
Copyright © Ajmal P.S. All rights reserved.
