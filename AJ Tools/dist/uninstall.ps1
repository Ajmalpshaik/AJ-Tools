param([string]$RevitYear = '2020')
$dest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitYear\AJ Tools"
if (Test-Path $dest) { Remove-Item -Path $dest -Recurse -Force }			
Write-Host "Removed AJ Tools from $dest"