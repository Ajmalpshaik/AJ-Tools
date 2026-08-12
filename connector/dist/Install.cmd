@echo off
REM Double-click entry point for the AJ Tools Connector.
REM Exists so a colleague never has to know what PowerShell is, and so the install works even
REM when script execution is restricted on their machine (-ExecutionPolicy Bypass applies to this
REM process only and changes nothing on the computer).

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-connector.ps1"

echo.
pause
