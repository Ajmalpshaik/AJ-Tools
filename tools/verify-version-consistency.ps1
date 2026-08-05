<#
    verify-version-consistency.ps1 - AJ Tools

    WHY THIS EXISTS
    The suite version is written in five places, and they drift. An independent audit on 2026-08-05
    caught README.md advertising 1.39.1 while the assembly was 1.40.3 - ten versions behind. It was
    fixed by hand, and then drifted AGAIN one version later, because the next version bump updated
    AssemblyInfo and CHANGELOG but not the README. A hand-fix does not solve a recurring drift; a check
    does.

    WHAT IT CHECKS
      1. AssemblyInfo.cs header       "Version       : X.Y.Z"
      2. AssemblyInfo.cs              [assembly: AssemblyVersion("X.Y.Z.0")]
      3. AssemblyInfo.cs              [assembly: AssemblyFileVersion("X.Y.Z.0")]
      4. AssemblyInfo.cs changelog    the newest "vX.Y.Z (date)" entry
      5. CHANGELOG.md                 the newest "## [X.Y.Z]" entry
      6. README.md                    "Current suite version: **X.Y.Z**"
    All six must agree. Optionally also compares the deployed DLL, which is informational only - it is
    stale whenever the version was bumped after the last deploy.

    RUN IT after any version bump, before committing:
      powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\verify-version-consistency.ps1
    Exit code 0 = all agree, 1 = drift (the mismatching places are listed).

    Author  : Ajmal P.S.   |   Created 2026-08-05   |   All Rights Reserved
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$asmPath    = Join-Path $repoRoot 'src\Properties\AssemblyInfo.cs'
$changePath = Join-Path $repoRoot 'CHANGELOG.md'
$readmePath = Join-Path $repoRoot 'README.md'

foreach ($p in @($asmPath, $changePath, $readmePath)) {
    if (-not (Test-Path $p)) { Write-Host "ERROR: missing $p" -ForegroundColor Red; exit 1 }
}

$asm    = Get-Content $asmPath    -Raw
$change = Get-Content $changePath -Raw
$readme = Get-Content $readmePath -Raw

function FirstMatch([string]$text, [string]$pattern, [string]$label) {
    $m = [regex]::Match($text, $pattern)
    if (-not $m.Success) { return [pscustomobject]@{ Label = $label; Version = '(not found)' } }
    return [pscustomobject]@{ Label = $label; Version = $m.Groups[1].Value }
}

$found = @(
    FirstMatch $asm    '(?m)^\s*\*\s*Version\s*:\s*([0-9]+\.[0-9]+\.[0-9]+)'          'AssemblyInfo header'
    FirstMatch $asm    'assembly:\s*AssemblyVersion\("([0-9]+\.[0-9]+\.[0-9]+)\.0"\)'  'AssemblyVersion attr'
    FirstMatch $asm    'assembly:\s*AssemblyFileVersion\("([0-9]+\.[0-9]+\.[0-9]+)\.0"\)' 'AssemblyFileVersion attr'
    FirstMatch $asm    '(?m)^\s*\*\s*v([0-9]+\.[0-9]+\.[0-9]+)\s*\('                   'AssemblyInfo changelog'
    FirstMatch $change '(?m)^##\s*\[([0-9]+\.[0-9]+\.[0-9]+)\]'                        'CHANGELOG.md'
    FirstMatch $readme 'Current suite version:\s*\*\*([0-9]+\.[0-9]+\.[0-9]+)\*\*'      'README.md'
)

$expected = $found[1].Version   # the AssemblyVersion attribute is the source of truth

Write-Host "Expected suite version (from AssemblyVersion attribute): $expected`n"
$bad = @()
foreach ($f in $found) {
    $ok = $f.Version -eq $expected
    if (-not $ok) { $bad += $f }
    Write-Host ("  {0,-24} {1,-12} {2}" -f $f.Label, $f.Version, $(if ($ok) { 'ok' } else { 'MISMATCH' }))
}

# Informational only: the deployed DLL lags whenever a bump happened after the last deploy.
$deployed = 'C:\ProgramData\Autodesk\Revit\Addins\2020\AJ Tools\AJ Tools.dll'
if (Test-Path $deployed) {
    $dv = (Get-Item $deployed).VersionInfo.FileVersion
    Write-Host ("`n  {0,-24} {1,-12} {2}" -f 'deployed DLL (info)', $dv, $(if ($dv -like "$expected*") { 'matches' } else { 'older - redeploy to update' }))
}

Write-Host ""
if ($bad.Count -eq 0) {
    Write-Host "RESULT: all six version references agree." -ForegroundColor Green
    exit 0
} else {
    foreach ($b in $bad) { Write-Host ("  DRIFT: {0} says {1}, expected {2}" -f $b.Label, $b.Version, $expected) -ForegroundColor Red }
    Write-Host "RESULT: version drift - fix before committing." -ForegroundColor Red
    exit 1
}
