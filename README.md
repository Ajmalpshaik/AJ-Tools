# AJ Tools

Revit 2020 plug-in focused on everyday documentation workflows. The add-in currently provides three major groups of commands that live on the **AJ Tools** ribbon tab once the DLL is loaded.

## Panels & Commands

### Graphics
- **Toggle Links** – Toggle visibility of every Revit link category in the active view.
- **Unhide All** – Re-enable any elements, categories, or temporary hide/isolate states in the current view.
- **Reset Graphics** – Clear per-element override graphics applied within the view.

### Dimensions
All three options live under the `Auto Dims` pulldown.
- **Grids Only** – Plans only; creates individual and overall strings for horizontal/vertical grids.
- **Levels Only** – Sections/elevations only; places level strings with overall dimension.
- **Grids + Levels** – Plans run the grid workflow, vertical views place level strings and grid strings.

### Datums
All three options live under the `Reset to 3D Extents` pulldown.
- **Grids Only** – Resets visible grid datum ends back to 3D extents in the view.
- **Levels Only** – Resets visible level datum ends (End0/End1/End2/End3 if available) to 3D.
- **Grids + Levels** – Runs both routines in one click.

## Building
1. Open `AJ Tools/AJ Tools/AJ Tools.csproj` with Visual Studio 2017+ and ensure Revit 2020 API DLL references are correct.
2. Build the project in Release; the output DLL and PNG assets will land in `AJ Tools/AJ Tools/AJ Tools/bin/Release`.

## Installing
Copy the compiled DLL and image assets into a Revit add-in folder (e.g. `%APPDATA%\Autodesk\Revit\Addins\2020`) along with an `.addin` manifest pointing at the DLL. Launch Revit 2020 and the **AJ Tools** tab will appear.
