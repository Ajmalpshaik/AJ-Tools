param(
    [switch]$AllUsers,
    [int[]]$RevitVersions = @(2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027)
)

$ErrorActionPreference = "Stop"

$supportedNetFrameworkVersions = @(2020, 2021, 2022, 2023, 2024)
$modernNetRequiredVersions = @(2025, 2026, 2027)

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Unblock-FilesInPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $items = @()
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $items = @(Get-Item -LiteralPath $Path -Force)
    } else {
        $items = @(Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue)
    }

    foreach ($item in $items) {
        try {
            Unblock-File -LiteralPath $item.FullName -ErrorAction SilentlyContinue
        } catch {
        }

        try {
            Remove-Item -LiteralPath $item.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue
        } catch {
        }
    }
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

# Downloaded zip payloads can carry Mark-of-the-Web and trigger 0x80131515 in Revit.
Unblock-FilesInPath -Path $scriptDir

$blockedDllNames = @("RevitAPI.dll", "RevitAPIUI.dll")
$dllPayload = Get-ChildItem -LiteralPath $scriptDir -Filter *.dll -File |
    Where-Object { $blockedDllNames -notcontains $_.Name }
$pdbPayload = Get-ChildItem -LiteralPath $scriptDir -Filter *.pdb -File -ErrorAction SilentlyContinue

function Get-InstallTargets {
    param(
        [Parameter(Mandatory = $true)]
        [int[]]$Versions,
        [switch]$IncludeAllUsers
    )

    $targets = @()
    foreach ($version in $Versions) {
        $targets += @{
            Scope = "Current User"
            Version = $version
            AddinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$version"
        }

        if ($IncludeAllUsers) {
            $targets += @{
                Scope = "All Users"
                Version = $version
                AddinRoot = Join-Path $env:ProgramData "Autodesk\Revit\Addins\$version"
            }
        }
    }

    return $targets
}

if ($AllUsers) {
    if (-not (Test-IsAdministrator)) {
        throw "All-users install requires Administrator privileges. Run install-all-users.cmd as Administrator."
    }
}

$installTargets = Get-InstallTargets -Versions $RevitVersions -IncludeAllUsers:$AllUsers
$installedVersions = New-Object System.Collections.Generic.List[int]
$skippedVersions = New-Object System.Collections.Generic.List[int]

foreach ($target in $installTargets) {
    $version = [int]$target.Version

    if ($modernNetRequiredVersions -contains $version) {
        if (-not $skippedVersions.Contains($version)) {
            $skippedVersions.Add($version)
        }

        Write-Warning "Skipping Revit $version. This package is a .NET Framework/Revit 2020-2024 build. Revit $version requires a separate modern .NET build."
        continue
    }

    if (-not ($supportedNetFrameworkVersions -contains $version)) {
        if (-not $skippedVersions.Contains($version)) {
            $skippedVersions.Add($version)
        }

        Write-Warning "Skipping Revit $version. AJ Tools has not been configured for this Revit version."
        continue
    }

    $addinRoot = $target.AddinRoot
    $targetDir = Join-Path $addinRoot "AJ Tools"
    $manifestPath = Join-Path $addinRoot "AJ Tools.addin"

    Write-Host "Installing AJ Tools for Revit $version ($($target.Scope)) to $targetDir"

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
    Unblock-FilesInPath -Path $targetDir

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
    Unblock-FilesInPath -Path $manifestPath

    if (-not $installedVersions.Contains($version)) {
        $installedVersions.Add($version)
    }
}

if ($AllUsers) {
    Write-Host "AJ Tools installation complete (Current User + All Users)."
} else {
    Write-Host "AJ Tools installation complete (Current User)."
    Write-Host "Tip: run install-all-users.cmd as Administrator to install for all users."
}

if ($installedVersions.Count -gt 0) {
    $installedVersionText = [string]::Join(", ", @($installedVersions | Sort-Object | ForEach-Object { $_.ToString() }))
    Write-Host "Installed Revit versions: $installedVersionText"
}

if ($skippedVersions.Count -gt 0) {
    $skippedVersionText = [string]::Join(", ", @($skippedVersions | Sort-Object | ForEach-Object { $_.ToString() }))
    Write-Host "Skipped Revit versions: $skippedVersionText"
    Write-Host "NEEDS_REVIEW: Revit 2025-2027 require separate modern .NET build outputs before they can be installed."
}
