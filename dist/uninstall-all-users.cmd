@echo off
setlocal

set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%uninstall.ps1" -AllUsers
if %errorlevel% neq 0 (
    echo Uninstall failed. Run this file as Administrator and check the PowerShell output.
    pause
    exit /b %errorlevel%
)

echo AJ Tools removed for current user and all users.
pause
