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

function Get-UninstallTargets {
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
        throw "All-users uninstall requires Administrator privileges. Run uninstall-all-users.cmd as Administrator."
    }
}

$targets = Get-UninstallTargets -Versions $RevitVersions -IncludeAllUsers:$AllUsers

$addinFiles = @(
    "AJ Tools.addin",
    "AJ Tools 2020.addin"
)

foreach ($target in $targets) {
    $root = $target.AddinRoot
    $targetDir = Join-Path $root "AJ Tools"

    Write-Host "Removing AJ Tools for Revit $($target.Version) ($($target.Scope)) from $targetDir"

    if (Test-Path -LiteralPath $targetDir) {
        Remove-Item -LiteralPath $targetDir -Recurse -Force
    }

    foreach ($manifest in $addinFiles) {
        $path = Join-Path $root $manifest
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

if ($AllUsers) {
    Write-Host "AJ Tools removed (Current User + All Users)."
} else {
    Write-Host "AJ Tools removed (Current User)."
}
