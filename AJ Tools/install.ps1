$ErrorActionPreference = "Stop"

$addinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2020"
$targetDir = Join-Path $addinsRoot "AJ Tools"
$srcDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Installing AJ Tools to $targetDir"

New-Item -ItemType Directory -Force -Path $addinsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Copy-Item (Join-Path $srcDir "AJ Tools.dll") -Destination $targetDir -Force
Copy-Item (Join-Path $srcDir "*.png") -Destination $targetDir -Force
Copy-Item (Join-Path $srcDir "AJ Tools.addin") -Destination $addinsRoot -Force

Write-Host "Done. Restart Revit 2020 and look for the AJ Tools tab."
