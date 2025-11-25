$ErrorActionPreference = "Stop"
param([string]$RevitYear = '2020')
$src = Split-Path -Parent $MyInvocation.MyCommand.Definition
$addinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitYear"
$targetDir = Join-Path $addinsRoot "AJ Tools"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item -Path "$src\*.dll" -Destination $targetDir -Force
Copy-Item -Path "$src\*.png" -Destination $targetDir -Force
Unblock-File -Path "$targetDir\AJ Tools.dll" -ErrorAction SilentlyContinue
$dllPath = Join-Path $targetDir "AJ Tools.dll"
$addinPath = Join-Path $addinsRoot "AJ Tools.addin"
$xml = "<?xml version=`"1.0`" encoding=`"utf-8`"?><RevitAddIns><AddIn Type=`"Application`"><Name>AJ Tools</Name><Assembly>$dllPath</Assembly><AddInId>{fe1f581f-9ea0-4752-b870-7192ae828b82}</AddInId><FullClassName>AJTools.App</FullClassName><VendorId>AJ</VendorId></AddIn></RevitAddIns>"
Set-Content -Path $addinPath -Value $xml -Encoding UTF8
Write-Host "Installation complete! Restart Revit 2020." -ForegroundColor Green
pause
