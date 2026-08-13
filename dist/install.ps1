param(
    [switch]$AllUsers,
    [int[]]$RevitVersions = @(2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027)
)

$ErrorActionPreference = "Stop"

# Which Revit versions can be installed is decided by what the package actually CONTAINS
# (Payload\<year>\), never by a hardcoded list. Until 2026-08-13 this script carried
# $modernNetRequiredVersions = 2025,2026,2027 and refused those outright, describing the package as
# "a .NET Framework/Revit 2020-2024 build" - a guard left over from before the multi-version backbone
# landed (2026-07-06). By then every release zip already shipped correct per-version builds under
# Payload\, and this script never looked at them: it installed the root (2020, net472) DLL to
# 2020-2024 and installed NOTHING on 2025-2027, while INSTALL.md advertised "payloads for Revit
# 2020-2027". A hardcoded list cannot go stale if it does not exist, so there isn't one any more.

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

function Clear-InstallDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        return
    } catch {
        # Revit holds its loaded DLLs open, so the delete fails while it is running. A locked file
        # can still be RENAMED, so move the whole folder aside and carry on, rather than leaving a
        # half-deleted install behind. This is the rule from the 2026-08-12 installer defect: never
        # destroy a working install before the replacement is in place.
        $aside = "{0}.{1}.old" -f $Path, [DateTime]::Now.Ticks
        Rename-Item -LiteralPath $Path -NewName (Split-Path -Leaf $aside) -Force
    }
}

function Remove-OldSetAsideFolders {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AddinRoot
    )

    # Best-effort sweep of folders set aside by earlier runs. Anything a running Revit still holds
    # open simply fails to delete and gets swept next time, so this can never fail the install.
    Get-ChildItem -LiteralPath $AddinRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'AJ Tools.*.old' } |
        ForEach-Object {
            try { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop } catch { }
        }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$requiredDll = "AJ Tools.dll"
$requiredDllPath = Join-Path $scriptDir $requiredDll
$resourcesPath = Join-Path $scriptDir "Resources"
$payloadRoot = Join-Path $scriptDir "Payload"

if (-not (Test-Path -LiteralPath $payloadRoot) -and -not (Test-Path -LiteralPath $requiredDllPath)) {
    throw "This package contains neither a Payload folder nor AJ Tools.dll. Run .\dist\package.ps1 first, then run install again."
}

if (-not (Test-Path -LiteralPath $resourcesPath)) {
    throw "Resources folder is missing in dist. Run .\dist\package.ps1 first, then run install again."
}

# Downloaded zip payloads can carry Mark-of-the-Web and trigger 0x80131515 in Revit.
Unblock-FilesInPath -Path $scriptDir

$blockedDllNames = @("RevitAPI.dll", "RevitAPIUI.dll")

function Get-PayloadFilesForVersion {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Version
    )

    # Preferred: the per-Revit build in Payload\<year>\. It carries that Revit's own DLL plus, for
    # 2025+, the AJ Tools.deps.json the .NET host needs - which a root-only copy would leave behind.
    $versionPayload = Join-Path $payloadRoot $Version
    if (Test-Path -LiteralPath $versionPayload) {
        return @(Get-ChildItem -LiteralPath $versionPayload -File -Recurse -Force |
            Where-Object { $blockedDllNames -notcontains $_.Name })
    }

    # Fallback for a legacy single-build package with no Payload folder at all.
    if (Test-Path -LiteralPath $requiredDllPath) {
        return @(Get-ChildItem -LiteralPath $scriptDir -File |
            Where-Object { $_.Extension -in '.dll', '.pdb' -and $blockedDllNames -notcontains $_.Name })
    }

    return @()
}

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

    $payloadFiles = Get-PayloadFilesForVersion -Version $version
    if ($payloadFiles.Count -eq 0) {
        if (-not $skippedVersions.Contains($version)) {
            $skippedVersions.Add($version)
        }

        Write-Warning "Skipping Revit $version. This package carries no build for it (expected $payloadRoot\$version)."
        continue
    }

    $addinRoot = $target.AddinRoot
    $targetDir = Join-Path $addinRoot "AJ Tools"
    $manifestPath = Join-Path $addinRoot "AJ Tools.addin"

    Write-Host "Installing AJ Tools for Revit $version ($($target.Scope)) to $targetDir"

    New-Item -ItemType Directory -Path $addinRoot -Force | Out-Null
    Clear-InstallDirectory -Path $targetDir
    Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    foreach ($file in $payloadFiles) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $targetDir $file.Name) -Force
    }

    Copy-Item -LiteralPath $resourcesPath -Destination $targetDir -Recurse -Force
    Unblock-FilesInPath -Path $targetDir

    $assemblyCheck = Join-Path $targetDir $requiredDll
    if (-not (Test-Path -LiteralPath $assemblyCheck)) {
        throw "Install for Revit $version failed: no $requiredDll landed in $targetDir."
    }

    Remove-OldSetAsideFolders -AddinRoot $addinRoot

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
    Write-Host "Those versions have no build in this package. Rebuild with .\dist\package.ps1 to include them."
}
