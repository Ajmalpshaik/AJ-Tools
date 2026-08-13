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

    # Folders the installer set aside because Revit had them locked ("AJ Tools.<ticks>.old"), plus
    # the timestamped payload folders the dev build deploy leaves ("AJ Tools.<ticks>"). Without this
    # an uninstall looks clean while hundreds of MB stay behind - 2,086 MB had accumulated by
    # 2026-08-12. Best-effort: anything still locked is skipped rather than failing the uninstall.
    Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^AJ Tools\.\d+(\.old)?$' } |
        ForEach-Object {
            try { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop } catch {
                Write-Warning "Could not remove $($_.FullName) - it is still in use."
            }
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
