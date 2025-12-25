# Source Overview

This folder contains the C# source for the AJ Tools Revit add-in.

## Folder Map
- `App/` - Revit add-in entry point and ribbon setup.
- `Commands/` - ExternalCommand entry points for each ribbon tool.
- `Services/` - Core logic for tools (Auto Dimension, Filter Pro, Flow Direction, Reset Datums).
- `Models/` - Data models and enums used by the UI and services.
- `UI/` - WPF windows and WinForms dialogs.
- `Utils/` - Shared helpers, selection filters, and constants.
- `Resources/` - Ribbon icons and UI assets (deployed next to the DLL).
- `Properties/` - Assembly metadata.
