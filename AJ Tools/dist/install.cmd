@echo off
echo ================================
echo   AJ Tools Installer v1.1.0
echo ================================
echo.
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0install.ps1"
if errorlevel 1 pause
