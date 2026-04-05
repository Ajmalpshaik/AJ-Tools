@echo off
setlocal

set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install.ps1"
if %errorlevel% neq 0 (
    echo Install failed. Check the PowerShell output above.
    pause
    exit /b %errorlevel%
)

echo AJ Tools installed successfully for current user.
echo To install for all users, run install-all-users.cmd as Administrator.
pause
