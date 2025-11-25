$ErrorActionPreference = 'Stop'
param([string]$RevitYear = '2020')

$src = Split-Path -Parent $MyInvocation.MyCommand.Definition
$log = Join-Path $src 'install_log.txt'
Add-Content -Path $log -Value "`n=== Install started: $(Get-Date) ===`n"

function Log { param($msg) Add-Content -Path $log -Value $msg; Write-Host $msg }

try {
    Log "Source folder: $src"

    $addinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitYear"
    $targetDir = Join-Path $addinsRoot 'AJ Tools'

    Log "Target folder: $targetDir"

    if (Get-Process -Name Revit -ErrorAction SilentlyContinue) {
        Log "Warning: Revit process detected. Please close Revit before installing to avoid file locks."
    }

    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    $copied = @()

    # Copy DLL
    $dll = Join-Path $src 'AJ Tools.dll'
    if (Test-Path $dll) {
        Copy-Item -Path $dll -Destination $targetDir -Force
        $copied += (Join-Path $targetDir 'AJ Tools.dll')
        Log "Copied AJ Tools.dll"
    } else { Log "ERROR: AJ Tools.dll not found in dist"; throw "Missing AJ Tools.dll" }

    # Copy PDB if present
    $pdb = Join-Path $src 'AJ Tools.pdb'
    if (Test-Path $pdb) { Copy-Item -Path $pdb -Destination $targetDir -Force; $copied += (Join-Path $targetDir 'AJ Tools.pdb'); Log "Copied AJ Tools.pdb" }

    # Copy PNG images
    $images = Get-ChildItem -Path $src -Filter *.png -File -ErrorAction SilentlyContinue
    foreach ($img in $images) {
        Copy-Item -Path $img.FullName -Destination $targetDir -Force
        $copied += (Join-Path $targetDir $img.Name)
        Log "Copied $($img.Name)"
    }

    # Unblock DLL
    $dllTarget = Join-Path $targetDir 'AJ Tools.dll'
    if (Test-Path $dllTarget) {
        try { Unblock-File -Path $dllTarget -ErrorAction SilentlyContinue; Log "Unblocked AJ Tools.dll" } catch { Log "Unblock failed (ignored)" }
    }

    # Write .addin manifest
    $addinPath = Join-Path $addinsRoot 'AJ Tools.addin'
    $dllFull = $dllTarget
    $guid = 'fe1f581f-9ea0-4752-b870-7192ae828b82'

    $xml = @"
<?xml version='1.0' encoding='utf-8'?>
<RevitAddIns>
  <AddIn Type='Application'>
    <Name>AJ Tools</Name>
    <Assembly>$dllFull</Assembly>
    <AddInId>{$guid}</AddInId>
    <FullClassName>AJTools.App</FullClassName>
    <VendorId>AJ</VendorId>
    <VendorDescription>Ajmal P.S</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    New-Item -ItemType Directory -Force -Path $addinsRoot | Out-Null
    Set-Content -Path $addinPath -Value $xml -Encoding UTF8
    Log "Wrote manifest: $addinPath"

    Log "Files copied:"
    $copied | ForEach-Object { Log "  $_" }

    Log "Installation completed successfully."
    Write-Host "Installation complete. Press any key to exit." -ForegroundColor Green
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}
catch {
    Log "ERROR during install: $($_.Exception.Message)"
    Write-Host "Installation failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "See install_log.txt in the dist folder for details." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    exit 1
}
