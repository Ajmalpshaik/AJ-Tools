$ErrorActionPreference = "Stop"

$addinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2020"
$addinRootPc = Join-Path $env:ProgramData "Autodesk\Revit\Addins\2020"
$targetDir = Join-Path $addinRoot "AJ Tools"
$targetDirPc = Join-Path $addinRootPc "AJ Tools"

Write-Host "Removing AJ Tools from $targetDir"

if (Test-Path $targetDir) {
    Remove-Item -Path $targetDir -Recurse -Force
}
if (Test-Path $targetDirPc) {
    Remove-Item -Path $targetDirPc -Recurse -Force
}

$addinFiles = @(
    "AJ Tools.addin",
    "AJ Tools 2020.addin"  # legacy cleanup
)

foreach ($root in @($addinRoot, $addinRootPc)) {
    foreach ($manifest in $addinFiles) {
        $path = Join-Path $root $manifest
        if (Test-Path $path) {
            Remove-Item -Path $path -Force
        }
    }
}

Write-Host "AJ Tools removed."
