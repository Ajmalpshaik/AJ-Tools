; Inno Setup script for AJ Tools installer
; To build: open Inno Setup Compiler (ISCC.exe) and compile this script, or run the build script in PowerShell.

[Setup]
AppName=AJ Tools
AppVersion=1.1.0
AppPublisher=Ajmal P.S
DefaultDirName={userappdata}\Autodesk\Revit\Addins\2020\AJ Tools
DefaultGroupName=AJ Tools
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=AJ-Tools-1.1.0-setup-2020
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; include everything from the dist folder
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{userdesktop}\AJ Tools (Revit 2020)"; Filename: "{app}\AJ Tools.dll"; WorkingDir: "{app}"

[Run]
; No automatic run after install

[UninstallDelete]
Type: files; Name: "{app}\*"

; Helper constant for build-time SourceDir. The build script will pass this.
#define SourceDir "..\\dist"
