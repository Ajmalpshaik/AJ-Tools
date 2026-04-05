param(
    [switch]$AllUsers
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$requiredDll = "AJ Tools.dll"
$requiredDllPath = Join-Path $scriptDir $requiredDll
$resourcesPath = Join-Path $scriptDir "Resources"

if (-not (Test-Path -LiteralPath $requiredDllPath)) {
    throw "AJ Tools.dll is missing in dist. Run .\dist\package.ps1 first, then run install again."
}

if (-not (Test-Path -LiteralPath $resourcesPath)) {
    throw "Resources folder is missing in dist. Run .\dist\package.ps1 first, then run install again."
}

$blockedDllNames = @("RevitAPI.dll", "RevitAPIUI.dll")
$dllPayload = Get-ChildItem -LiteralPath $scriptDir -Filter *.dll -File |
    Where-Object { $blockedDllNames -notcontains $_.Name }
$pdbPayload = Get-ChildItem -LiteralPath $scriptDir -Filter *.pdb -File -ErrorAction SilentlyContinue

$installTargets = @(
    @{
        Scope = "Current User"
        AddinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2020"
    }
)

if ($AllUsers) {
    if (-not (Test-IsAdministrator)) {
        throw "All-users install requires Administrator privileges. Run install-all-users.cmd as Administrator."
    }

    $installTargets += @{
        Scope = "All Users"
        AddinRoot = Join-Path $env:ProgramData "Autodesk\Revit\Addins\2020"
    }
}

foreach ($target in $installTargets) {
    $addinRoot = $target.AddinRoot
    $targetDir = Join-Path $addinRoot "AJ Tools"
    $manifestPath = Join-Path $addinRoot "AJ Tools.addin"

    Write-Host "Installing AJ Tools ($($target.Scope)) to $targetDir"

    New-Item -ItemType Directory -Path $addinRoot -Force | Out-Null
    Remove-Item -LiteralPath $targetDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    foreach ($dll in $dllPayload) {
        Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $targetDir $dll.Name) -Force
    }

    foreach ($pdb in $pdbPayload) {
        Copy-Item -LiteralPath $pdb.FullName -Destination (Join-Path $targetDir $pdb.Name) -Force
    }

    Copy-Item -LiteralPath $resourcesPath -Destination $targetDir -Recurse -Force

    $assemblyPath = Join-Path $targetDir $requiredDll
    $addinXml = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>AJ Tools</Name>
    <AddInId>{C14A253D-96C6-42B7-889A-CFE737556BA9}</AddInId>
    <FullClassName>AJTools.App.App</FullClassName>
    <Assembly>$assemblyPath</Assembly>
    <VendorId>AJT</VendorId>
    <VendorDescription>Ajmal P.S - ajmalnattika@gmail.com</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

    $addinXml | Out-File -FilePath $manifestPath -Encoding utf8 -Force
}

if ($AllUsers) {
    Write-Host "AJ Tools installation complete (Current User + All Users)."
} else {
    Write-Host "AJ Tools installation complete (Current User)."
    Write-Host "Tip: run install-all-users.cmd as Administrator to install for all users."
}
