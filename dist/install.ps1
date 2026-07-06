param(
    [switch]$AllUsers,
    [int[]]$RevitVersions = @(2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027)
)

$ErrorActionPreference = "Stop"

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
$payloadRoot = Join-Path $scriptDir "Payload"
$resourcesPath = Join-Path $scriptDir "Resources"

if (-not (Test-Path -LiteralPath $payloadRoot) -and -not (Test-Path -LiteralPath (Join-Path $scriptDir $requiredDll))) {
    throw "AJ Tools payload is missing in dist. Run .\dist\package.ps1 first, then run install again."
}

if (-not (Test-Path -LiteralPath $resourcesPath)) {
    throw "Resources folder is missing in dist. Run .\dist\package.ps1 first, then run install again."
}

# Downloaded zip payloads can carry Mark-of-the-Web and trigger 0x80131515 in Revit.
Unblock-FilesInPath -Path $scriptDir

$blockedDllNames = @("RevitAPI.dll", "RevitAPIUI.dll")

function Resolve-PayloadDirForVersion {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Version
    )

    $candidates = @()
    $candidates += (Join-Path $payloadRoot $Version)
    if ($Version -ge 2020 -and $Version -le 2024) {
        $candidates += (Join-Path $payloadRoot "2020-2024")
    }
    $candidates += $scriptDir

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $candidateDll = Join-Path $candidate $requiredDll
        if (Test-Path -LiteralPath $candidateDll) {
            return $candidate
        }
    }

    throw "No AJ Tools payload found for Revit $Version. Run .\dist\package.ps1 to create versioned payloads."
}

function Get-DllPayload {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PayloadDir
    )

    $items = @(Get-ChildItem -LiteralPath $PayloadDir -Filter *.dll -File |
        Where-Object { $blockedDllNames -notcontains $_.Name })

    if (-not ($items | Where-Object { $_.Name -eq $requiredDll })) {
        throw "AJ Tools.dll is missing from payload '$PayloadDir'."
    }

    return $items
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

foreach ($target in $installTargets) {
    $version = [int]$target.Version
    $payloadDir = Resolve-PayloadDirForVersion -Version $version
    $dllPayload = Get-DllPayload -PayloadDir $payloadDir
    $pdbPayload = Get-ChildItem -LiteralPath $payloadDir -Filter *.pdb -File -ErrorAction SilentlyContinue

    $addinRoot = $target.AddinRoot
    $targetDir = Join-Path $addinRoot "AJ Tools"
    $manifestPath = Join-Path $addinRoot "AJ Tools.addin"

    Write-Host "Installing AJ Tools for Revit $version ($($target.Scope)) to $targetDir"
    Write-Host "  Payload: $payloadDir"

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
