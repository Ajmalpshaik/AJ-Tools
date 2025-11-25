$ErrorActionPreference = "Stop"

param([string]$RevitYear = '2020')

$src = Split-Path -Parent $MyInvocation.MyCommand.Definition
$addinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitYear"
$targetDir = Join-Path $addinsRoot "AJ Tools"

Write-Host "Installing AJ Tools to $targetDir"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

# Copy package files but exclude installer sources and archives
Copy-Item -Path (Join-Path $src '*') -Destination $targetDir -Recurse -Force -Exclude '*.ps1','*.cmd','*.zip','*.iss'

# Unblock important files
$filesToUnblock = @('AJ Tools.dll','AJ Tools.pdb')
foreach ($f in $filesToUnblock) {
    $p = Join-Path $targetDir $f
    if (Test-Path $p) {
        try { Unblock-File -Path $p -ErrorAction SilentlyContinue } catch { }
    }
}

# Create .addin manifest with absolute assembly path
$addinPath = Join-Path $addinsRoot 'AJ Tools.addin'
$dllPath = Join-Path $targetDir 'AJ Tools.dll'
$guid = 'fe1f581f-9ea0-4752-b870-7192ae828b82'

$addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>AJ Tools</Name>
    <Assembly>$dllPath</Assembly>
    <AddInId>{$guid}</AddInId>
    <FullClassName>AJTools.App</FullClassName>
    <VendorId>AJ</VendorId>
    <VendorDescription>Ajmal P.S</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

Set-Content -Path $addinPath -Value $addinContent -Encoding UTF8

Write-Host "Installation complete. Restart Revit 2020 and check for the 'AJ Tools' tab."
