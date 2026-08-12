<#
.SYNOPSIS
  Tells the connector where to look for tools - a local folder, or the website.

.DESCRIPTION
  Writes %APPDATA%\AJTools\connector.json. The connector reads this every time it refreshes, so the
  change takes effect on the next "Check for new tools" - no reinstall, no Revit restart.

  This is the single switch that turns the local development setup into the live website one. It is
  deliberately a setting rather than something compiled in, so the exact build a colleague already
  installed can be repointed without shipping them anything.

  A colleague only ever needs this if you change the website address later. A fresh install already
  defaults to a sensible local folder.

.EXAMPLE
  .\set-tool-source.ps1 -Url https://yoursite.com/ajtools/tools/
  .\set-tool-source.ps1 -Local
  .\set-tool-source.ps1 -Show
#>
[CmdletBinding(DefaultParameterSetName = "Show")]
param(
  [Parameter(ParameterSetName = "Url", Mandatory)] [string] $Url,
  [Parameter(ParameterSetName = "Local", Mandatory)] [switch] $Local,
  [Parameter(ParameterSetName = "Show")] [switch] $Show
)

$ErrorActionPreference = "Stop"

$configPath  = Join-Path $env:APPDATA "AJTools\connector.json"
$localFolder = Join-Path $env:APPDATA "AJTools\tools"

function Show-Current {
  if (Test-Path $configPath) {
    $current = (Get-Content $configPath -Raw | ConvertFrom-Json).source
    Write-Host "Tools are currently read from:" -ForegroundColor Cyan
    Write-Host "  $current"
  } else {
    Write-Host "No setting saved - the connector is using its default local folder:" -ForegroundColor Cyan
    Write-Host "  $localFolder"
  }
}

switch ($PSCmdlet.ParameterSetName) {
  "Url" {
    if ($Url -notmatch '^https?://') { throw "That does not look like a web address: $Url" }

    if ($Url -match '^http://') {
      Write-Host "WARNING: plain http, not https." -ForegroundColor Yellow
      Write-Host "Signatures still stop anyone tampering with a tool, so nothing unsafe can run."
      Write-Host "But https is worth using anyway - it stops anyone WATCHING which tools you use."
      Write-Host ""
    }

    $dir = Split-Path -Parent $configPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

    @{ source = $Url.TrimEnd('/') + '/' } | ConvertTo-Json | Out-File -FilePath $configPath -Encoding utf8

    Write-Host "Tools will now be read from:" -ForegroundColor Green
    Write-Host "  $($Url.TrimEnd('/'))/"
    Write-Host ""
    Write-Host "Press 'Check for new tools' in the connector page - no restart needed."
  }

  "Local" {
    $dir = Split-Path -Parent $configPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

    @{ source = $localFolder } | ConvertTo-Json | Out-File -FilePath $configPath -Encoding utf8

    Write-Host "Back to the local folder:" -ForegroundColor Green
    Write-Host "  $localFolder"
  }

  default { Show-Current }
}

Write-Host ""
