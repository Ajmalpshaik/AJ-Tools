@echo off
setlocal
rem Run the PowerShell installer script from the same folder as this CMD.
powershell -ExecutionPolicy Bypass -NoLogo -NoProfile -File "%~dp0install.ps1"
echo.
echo Done. Restart Revit 2020.
endlocal
