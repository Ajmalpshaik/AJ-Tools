# AJ Tools for Revit 2020

Lightweight productivity add-in for Autodesk Revit 2020 (v1.1.0).

## Folder Layout
- `src/` – C# source, project, images.
- `dist/` – Release payload and one-click installers (ready to ship).
- `.vs/` – IDE cache (ignored; safe to delete if not in use).

## Build
```powershell
# From repository root
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "AJ Tools.sln" /p:Configuration=Release /p:Platform="Any CPU"
```

## Install (one-click)
1. Navigate to `dist/`.
2. Run `install.cmd` (or `install.ps1` if you prefer PowerShell).
3. Restart Revit 2020 and look for the **AJ Tools** ribbon tab.

## Uninstall
1. Navigate to `dist/`.
2. Run `uninstall.cmd` (or `uninstall.ps1`).

## Publishing to GitHub
Commit the cleaned structure and push:
```bash
git add .
git commit -m "Prepare release 1.1.0"
git remote add origin <your-repo-url>
git push -u origin main
```
Then attach the `dist/` folder (or a ZIP of it) to your GitHub release for users.

## Author
Ajmal P.S — ajmalnattika@gmail.com
