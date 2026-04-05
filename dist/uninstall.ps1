param(
    [switch]$AllUsers
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$targets = @(
    @{
        Scope = "Current User"
        AddinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2020"
    }
)

if ($AllUsers) {
    if (-not (Test-IsAdministrator)) {
        throw "All-users uninstall requires Administrator privileges. Run uninstall-all-users.cmd as Administrator."
    }

    $targets += @{
        Scope = "All Users"
        AddinRoot = Join-Path $env:ProgramData "Autodesk\Revit\Addins\2020"
    }
}

$addinFiles = @(
    "AJ Tools.addin",
    "AJ Tools 2020.addin"
)

foreach ($target in $targets) {
    $root = $target.AddinRoot
    $targetDir = Join-Path $root "AJ Tools"

    Write-Host "Removing AJ Tools ($($target.Scope)) from $targetDir"

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
