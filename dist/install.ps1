$ErrorActionPreference = "Stop"

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Definition
$addinRoot   = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2020"
$addinRootPc = Join-Path $env:ProgramData "Autodesk\Revit\Addins\2020"
$targetDir   = Join-Path $addinRoot "AJ Tools"
$targetDirPc = Join-Path $addinRootPc "AJ Tools"

Write-Host "Installing AJ Tools to $targetDir"

# Clean old payloads and ensure target folders exist (user + all-users)
Remove-Item -Path $targetDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $targetDirPc -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $addinRoot "AJ Tools.addin") -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $addinRootPc "AJ Tools.addin") -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
New-Item -ItemType Directory -Path $targetDirPc -Force | Out-Null
New-Item -ItemType Directory -Path $addinRoot -Force | Out-Null
New-Item -ItemType Directory -Path $addinRootPc -Force | Out-Null

# Copy payload (user + all-users to keep in sync)
Copy-Item -Path (Join-Path $scriptDir "AJ Tools.dll") -Destination $targetDir -Force
Copy-Item -Path (Join-Path $scriptDir "AJ Tools.dll") -Destination $targetDirPc -Force
if (Test-Path (Join-Path $scriptDir "AJ Tools.pdb")) {
    Copy-Item -Path (Join-Path $scriptDir "AJ Tools.pdb") -Destination $targetDir -Force
    Copy-Item -Path (Join-Path $scriptDir "AJ Tools.pdb") -Destination $targetDirPc -Force
}
Copy-Item -Path (Join-Path $scriptDir "Images") -Destination $targetDir -Recurse -Force
Copy-Item -Path (Join-Path $scriptDir "Images") -Destination $targetDirPc -Recurse -Force

# Generate manifest with absolute assembly path (avoid env var expansion issues)
$assemblyPath = Join-Path $targetDir "AJ Tools.dll"
$assemblyPathPc = Join-Path $targetDirPc "AJ Tools.dll"
$addinXml = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>AJ Tools</Name>
    <AddInId>{C14A253D-96C6-42B7-889A-CFE737556BA9}</AddInId>
    <FullClassName>AJTools.App</FullClassName>
    <Assembly>$assemblyPath</Assembly>
    <VendorId>AJT</VendorId>
    <VendorDescription>Ajmal P.S - ajmalnattika@gmail.com</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

$addinRoots = @($addinRoot, $addinRootPc)
($addinRoot, $addinRootPc) | ForEach-Object {
    $assembly = if ($_ -eq $addinRootPc) { $assemblyPathPc } else { $assemblyPath }
    $xml = $addinXml -replace [regex]::Escape($assemblyPath), $assembly
    $outPath = Join-Path $_ "AJ Tools.addin"
    $xml | Out-File -FilePath $outPath -Encoding utf8 -Force
}

Write-Host "AJ Tools installation complete."
