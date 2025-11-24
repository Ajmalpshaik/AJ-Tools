$ErrorActionPreference = "Stop"

param([string]$RevitYear = '2020')

$addinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitYear"
$targetDir = Join-Path $addinsRoot "AJ Tools"
$srcDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Installing AJ Tools to $targetDir"

New-Item -ItemType Directory -Force -Path $addinsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Copy-Item (Join-Path $srcDir "AJ Tools.dll") -Destination $targetDir -Force
Copy-Item (Join-Path $srcDir "*.png") -Destination $targetDir -Force

# Generate the .addin with an absolute path to the DLL
$addinPath = Join-Path $addinsRoot "AJ Tools.addin"
$dllPath = Join-Path $targetDir "AJ Tools.dll"

$addinContent = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>AJ Tools</Name>
    <Assembly>$dllPath</Assembly>
    <AddInId>{9FD8F3D3-8EB5-4E71-94D4-F86EB696F3F2}</AddInId>
    <FullClassName>AJTools.App</FullClassName>
    <VendorId>AJ</VendorId>
    <VendorDescription>Ajmal P.S</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

Set-Content -Path $addinPath -Value $addinContent -Encoding UTF8

Write-Host "Done. Restart Revit $RevitYear and look for the AJ Tools tab."
