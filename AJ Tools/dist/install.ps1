$ErrorActionPreference = "Stop"

param([string]$RevitYear = '2020')

$src = Split-Path -Parent $MyInvocation.MyCommand.Definition
$log = Join-Path $src 'install_log.txt'
Add-Content -Path $log -Value "`n=== Install started: $(Get-Date) ===`n"

try {
    $addinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitYear"
    $targetDir = Join-Path $addinsRoot "AJ Tools"

    Write-Host "Installing AJ Tools to $targetDir"
    Add-Content -Path $log -Value "Installing to: $targetDir"

    New-Item -ItemType Directory -Force -Path $addinsRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    # List source files
    $files = Get-ChildItem -Path $src -File -Force | Where-Object { $_.Name -notmatch 'install_log.txt' }
    Add-Content -Path $log -Value "Source files:`n$($files | ForEach-Object { $_.Name } | Out-String)"

    # Copy package files but exclude installer scripts and archives
    $exclude = @('*.ps1','*.cmd','*.zip','*.iss')
    Copy-Item -Path (Join-Path $src '*') -Destination $targetDir -Recurse -Force -Exclude $exclude

    # Confirm copied
    $copied = Get-ChildItem -Path $targetDir -File -Recurse | Select-Object -ExpandProperty FullName
    Add-Content -Path $log -Value "Copied files to target:`n$($copied | Out-String)"

    # Unblock important files
    $filesToUnblock = @('AJ Tools.dll','AJ Tools.pdb')
    foreach ($f in $filesToUnblock) {
        $p = Join-Path $targetDir $f
        if (Test-Path $p) {
            try { Unblock-File -Path $p -ErrorAction SilentlyContinue; Add-Content -Path $log -Value "Unblocked: $p" } catch { Add-Content -Path $log -Value "Failed to unblock: $p" }
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
    Add-Content -Path $log -Value "Wrote addin manifest: $addinPath"

    Write-Host "Installation complete. Restart Revit 2020 and check for the 'AJ Tools' tab."; Add-Content -Path $log -Value "Install succeeded: $(Get-Date)"
    exit 0
}
catch {
    Write-Host "Installation failed: $($_.Exception.Message)" -ForegroundColor Red
    Add-Content -Path $log -Value "ERROR: $($_.Exception.ToString())"
    exit 1
}
