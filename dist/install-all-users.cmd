@echo off
setlocal

set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install.ps1" -AllUsers
if %errorlevel% neq 0 (
    echo Install failed. Run this file as Administrator and check the PowerShell output.
    pause
    exit /b %errorlevel%
)

echo AJ Tools installed for current user and all users.
pause
