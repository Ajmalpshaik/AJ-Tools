<#
.SYNOPSIS
  Installs the AJ Tools Connector for the current user.

.DESCRIPTION
  This is the whole install a colleague ever runs. It copies the connector beside Revit's other
  add-ins and registers it. No admin rights, no system changes, nothing added to Program Files.

  It does NOT touch an existing AJ Tools installation: different folder, different manifest,
  different add-in id. Either can be removed without affecting the other.

.PARAMETER RevitVersions
  Which Revit versions to install for. Defaults to whichever are actually present on this machine.

.PARAMETER Uninstall
  Removes the connector instead of installing it.

.EXAMPLE
  .\install-connector.ps1
  .\install-connector.ps1 -Uninstall
#>
[CmdletBinding()]
param(
  [int[]] $RevitVersions,
  [switch] $Uninstall
)

$ErrorActionPreference = "Stop"

$AddinName  = "AJ Connect"
$AddinId    = "861D2CC6-DA2B-48BD-B292-91D6D2F114E4"
$ClassName  = "AJToolsConnector.ConnectorApp"
$DllName    = "AJ Tools Connector.dll"

# The add-in was briefly registered under this older name. Installing or uninstalling clears it too,
# otherwise Revit loads BOTH copies and shows two identical tabs.
$LegacyNames = @("AJ Tools Connector")

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$payloadDir  = Join-Path $scriptDir "Payload"
$addinsRoot  = Join-Path $env:APPDATA "Autodesk\Revit\Addins"

function Write-Step { param($Text) Write-Host "  $Text" }

# Windows marks anything downloaded from the internet with a Zone.Identifier stream, and .NET refuses
# to load a blocked assembly - the add-in would simply never appear, with no error anywhere. Clearing
# it is the single most important step in this script for a file that arrived by download.
function Unblock-Payload {
  param([string] $Path)
  if (-not (Test-Path -LiteralPath $Path)) { return }

  foreach ($item in Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue) {
    try { Unblock-File -LiteralPath $item.FullName -ErrorAction SilentlyContinue } catch { }
    try { Remove-Item -LiteralPath $item.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue } catch { }
  }
}

function Remove-Registration {
  param([string] $VersionRoot, [string] $Name)

  $folder   = Join-Path $VersionRoot $Name
  $manifest = Join-Path $VersionRoot "$Name.addin"

  if (Test-Path $manifest) { Remove-Item $manifest -Force }
  if (Test-Path $folder)   { Remove-Item $folder -Recurse -Force }
}

function Get-InstalledRevitVersions {
  if (-not (Test-Path $addinsRoot)) { return @() }

  Get-ChildItem $addinsRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^\d{4}$' } |
    ForEach-Object { [int] $_.Name } |
    Sort-Object
}

# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "AJ Connect" -ForegroundColor Cyan
Write-Host "==========" -ForegroundColor Cyan
Write-Host ""

if (-not $RevitVersions -or $RevitVersions.Count -eq 0) {
  $RevitVersions = Get-InstalledRevitVersions
}

if (-not $RevitVersions -or $RevitVersions.Count -eq 0) {
  Write-Host "No Revit installation was found on this computer." -ForegroundColor Yellow
  Write-Host "If Revit is installed, start it once and then run this again."
  exit 1
}

if ($Uninstall) {
  Write-Host "Removing the connector..."
  foreach ($version in $RevitVersions) {
    $versionRoot = Join-Path $addinsRoot $version

    Remove-Registration -VersionRoot $versionRoot -Name $AddinName
    foreach ($legacy in $LegacyNames) { Remove-Registration -VersionRoot $versionRoot -Name $legacy }

    Write-Step "Revit $version - removed"
  }

  Write-Host ""
  Write-Host "Done. Restart Revit and the AJ Connector tab will be gone." -ForegroundColor Green
  Write-Host "Your other add-ins were not touched."
  exit 0
}

if (-not (Test-Path (Join-Path $payloadDir $DllName))) {
  Write-Host "This installer looks incomplete - $DllName is missing." -ForegroundColor Red
  Write-Host "Expected it in: $payloadDir"
  Write-Host "Please download the zip again and extract ALL of it before running this."
  exit 1
}

Write-Step "Clearing the downloaded-file block..."
Unblock-Payload -Path $payloadDir

foreach ($version in $RevitVersions) {
  $versionRoot = Join-Path $addinsRoot $version
  $target      = Join-Path $versionRoot $AddinName
  $manifest    = Join-Path $versionRoot "$AddinName.addin"

  if (-not (Test-Path $versionRoot)) { New-Item -ItemType Directory -Force $versionRoot | Out-Null }

  Remove-Registration -VersionRoot $versionRoot -Name $AddinName
  foreach ($legacy in $LegacyNames) { Remove-Registration -VersionRoot $versionRoot -Name $legacy }

  New-Item -ItemType Directory -Force $target | Out-Null

  Copy-Item (Join-Path $payloadDir "*") -Destination $target -Recurse -Force

  $dllPath = Join-Path $target $DllName
  $xml = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>$AddinName</Name>
    <AddInId>$AddinId</AddInId>
    <FullClassName>$ClassName</FullClassName>
    <Assembly>$dllPath</Assembly>
    <VendorId>AJT</VendorId>
    <VendorDescription>Ajmal P.S - ajmalnattika@gmail.com</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

  $xml | Out-File -FilePath $manifest -Encoding utf8
  Write-Step "Revit $version - installed"
}

Write-Host ""
Write-Host "Installed." -ForegroundColor Green
Write-Host ""
Write-Host "Next:"
Write-Host "  1. Start Revit (close it first if it is already open)"
Write-Host "  2. Open any model"
Write-Host "  3. Go to the AJ Connect tab and click AJ Connect"
Write-Host ""
Write-Host "Your browser opens and the tools appear there. You never install again -"
Write-Host "new tools arrive on their own."
Write-Host ""
