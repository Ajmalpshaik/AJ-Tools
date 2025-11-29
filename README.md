# AJ Tools for Autodesk Revit 2020

![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Revit%202020-green.svg)
![License](https://img.shields.io/badge/license-Proprietary-red.svg)

A lightweight, productivity-focused add-in for Autodesk Revit 2020, designed to streamline day-to-day documentation and modeling workflows.

## Features

### Graphics Tools
- **Toggle Links** - Quickly toggle visibility of all Revit links in the active view
- **Unhide All** - Reveal all hidden elements in the active view with a single click
- **Reset Graphics** - Clear all per-element graphic overrides in the active view

### Dimensions Tools
- **Auto Dimensions** - Automatically dimension grids and levels with pulldown options:
  - Grids Only (plan views)
  - Levels Only (section/elevation views)
  - Grids + Levels (combined)
- **Dimensions by Line** - Place custom grid or level dimensions along a user-defined line
- **Copy Dim Text** - Copy Above/Below/Prefix/Suffix text from one dimension to others

### Datums Tools
- **Reset to 3D Extents** - Reset grid and level datum extents back to model extents
- **Flip Grid Bubble** - Toggle which end of a grid displays the bubble

### Views Tools
- **Copy View Range** - Copy the active plan view's range and paste it to other plan views

### MEP Tools
- **Match Elevation** - Match the middle elevation from a source MEP element to others
- **Filter Pro** - Create parameter-based filters quickly from categories, parameters, and values with color application support

### Annotations Tools
- **Reset Text** - Reset selected text notes or tags back to their default offset position

### Refresh Mind
- **Cyber Snake** - A classic Snake mini-game for quick breaks
- **Neon Defender** - An action-packed twin-stick shooter mini-game

## Requirements

- Autodesk Revit 2020
- Windows 10 or later
- .NET Framework 4.7.2

## Installation

### One-Click Install (Recommended)

1. Download the latest release from the [Releases](../../releases) page
2. Extract the `dist` folder contents
3. Run `install.cmd` (or `install.ps1` for PowerShell)
4. Restart Revit 2020
5. Look for the **AJ Tools** ribbon tab

### Manual Installation

1. Copy `AJ Tools.dll` and the `Images` folder to:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2020\AJ Tools\
   ```
2. Copy `AJ Tools.addin` to:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2020\
   ```
3. Restart Revit 2020

## Uninstallation

1. Navigate to the `dist` folder
2. Run `uninstall.cmd` (or `uninstall.ps1`)
3. Restart Revit 2020

## Building from Source

### Prerequisites

- Visual Studio 2019 or later
- Autodesk Revit 2020 SDK (for API references)
- .NET Framework 4.7.2 SDK

### Build Instructions

```powershell
# From repository root
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "AJ Tools.sln" /p:Configuration=Release /p:Platform="Any CPU"
```

The build process automatically deploys the add-in to your local Revit addins folder.

## Project Structure

```
AJ-Tools/
├── src/
│   ├── App.cs                    # Main application entry point
│   ├── Commands/                 # All Revit commands
│   │   ├── CmdAbout.cs
│   │   ├── CmdAutoDimensions.cs
│   │   ├── CmdCopyDimensionText.cs
│   │   ├── CmdCopyViewRange.cs
│   │   ├── CmdDimensionByLine.cs
│   │   ├── CmdFilterPro.cs
│   │   ├── CmdFlipGridBubble.cs
│   │   ├── CmdMatchElevation.cs
│   │   ├── CmdNeonDefender.cs
│   │   ├── CmdResetDatums.cs
│   │   ├── CmdResetOverrides.cs
│   │   ├── CmdResetTextPosition.cs
│   │   ├── CmdSnakeGame.cs
│   │   ├── CmdToggleRevitLinks.cs
│   │   └── CmdUnhideAll.cs
│   ├── Services/
│   │   └── AutoDimensionService.cs
│   ├── FilterProHelper.cs
│   ├── FilterProWindow.xaml      # Filter Pro WPF UI
│   ├── FilterProWindow.xaml.cs
│   ├── Images/                   # Ribbon icons
│   └── Properties/
│       └── AssemblyInfo.cs
├── dist/                         # Release payload
│   ├── AJ Tools.addin
│   ├── AJ Tools.dll
│   ├── Images/
│   ├── install.cmd
│   ├── install.ps1
│   ├── uninstall.cmd
│   └── uninstall.ps1
├── AJ Tools.sln                  # Visual Studio solution
└── README.md
```

## Support

For bug reports, feature requests, or questions:

- Open an issue on [GitHub Issues](../../issues)
- Contact the developer directly

## Author

**Ajmal P.S**
- Email: [ajmalnattika@gmail.com](mailto:ajmalnattika@gmail.com)
- LinkedIn: [linkedin.com/in/ajmalps](https://www.linkedin.com/in/ajmalps/)

## License

This software is proprietary. All rights reserved.

---

© 2025 Ajmal P.S. All rights reserved.
