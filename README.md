# AJ Tools for Autodesk Revit (2020+)

AJ Tools is a collection of productivity commands for Autodesk Revit targeting .NET Framework 4.7.2. It includes numerous utilities plus a new Filter Pro module to create, manage, and apply parameter-based view filters with modern UI.

## Highlights
- Filter Pro: create filters from parameter values across categories
- Apply filters to the active view with line and pattern color overrides
- Supports built-in, shared, and project parameters
- 14 rule types: equals, not equals, contains, begins/ends with, comparisons, has value/no value
- Robust UI (WPF) with search, sorting, and real-time name preview

## Project Structure
- `src/` – Source code and WPF window
  - `Commands/` – Revit external commands (including `CmdFilterPro`)
  - `FilterProWindow.xaml` / `FilterProWindow.xaml.cs` – WPF UI for Filter Pro
  - `FilterProHelper.cs` – Shared logic for filter creation and view overrides
  - `Images/` – Icons and assets
  - `AJ Tools.csproj` – Project file
- `dist/` – Addin packaging scripts and manifest

## Requirements
- Autodesk Revit 2020 or later (tested with 2020)
- .NET Framework 4.7.2
- Visual Studio 2019/2022

## Build
1. Open `src/AJ Tools.csproj` in Visual Studio.
2. Build in Debug or Release.
3. Output: `src/bin/<Config>/AJ Tools.dll`.

Note: The project includes an MSBuild target to deploy the addin to the Revit addins folder. To avoid file locks when Revit is running, deployment is guarded. If you want automatic deployment:
- Edit the project to set `SkipRevitAddinDeploy=false` or remove the target condition.

## Manual Install (Addin)
1. Copy `AJ Tools.dll` and `Images/` to `%APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools`.
2. Place an `.addin` manifest pointing to `AJ Tools.dll`. Sample is generated in the build target.

## Filter Pro – Usage
- Launch `Filter Pro` from the AJ Tools ribbon (or external command list).
- Selection Tab:
  - Select Categories (multi-select)
  - Select a common Parameter
  - Load and select Values (search + sort available)
- Configuration Tab:
  - Choose rule type (14 options)
  - Configure naming: prefix/suffix/separator, include category/parameter
- Footer:
  - Override Existing toggle
  - Apply to View: creates filters and applies overrides in the active view
  - Shuffle Colors: creates filters and applies random colors
  - Create Filters: creates filters only

### Rule Types
- Equals, Not Equals
- Contains, Does Not Contain
- Begins With, Does Not Begin With
- Ends With, Does Not End With
- Greater, Greater Or Equal, Less, Less Or Equal (numeric)
- Has Value, Has No Value

### Parameter Support
- Built-in parameters via `BuiltInParameter`
- Shared/Project parameters by matching parameter IDs
- Storage types: String, Integer, Double, ElementId

### View Overrides
- Projection and Cut line colors
- Foreground surface/cut patterns (solid fill) and colors
- Halftone option

## Development Notes
- Target framework: .NET Framework 4.7.2
- Revit API: uses `Autodesk.Revit.DB` and `Autodesk.Revit.UI`
- WPF UI in `FilterProWindow`
- Logic separated in `FilterProHelper`
- Avoids newer API methods not present in Revit 2020

## Troubleshooting
- Build fails with file lock in `%APPDATA%\Autodesk\Revit\Addins\2020`:
  - Close Revit; or disable deployment target; or set `SkipRevitAddinDeploy=true`.
- No values loaded:
  - Ensure parameter is common across categories and has values.
- Custom/shared parameter not found:
  - Confirm parameter exists on elements/types in selected categories.

## Contributing
- Fork the repo and create a feature branch
- Run builds and test in Revit
- Submit PRs with clear description and screenshots for UI changes

## License
Copyright © Ajmal P.S. All rights reserved.
