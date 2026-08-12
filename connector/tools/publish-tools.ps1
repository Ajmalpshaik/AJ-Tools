<#
.SYNOPSIS
  Signs every tool listed in tools.json and produces a folder ready to upload to the website.

.DESCRIPTION
  This is the one command Ajmal runs to publish. It reads connector/tools-source/tools.json, signs
  each tool's code with his private key, and writes:

      <output>\index.json        - the list of tool files (what the connector asks for first)
      <output>\<id>.ajtool       - one signed file per tool

  Upload that whole folder to the website and every installed connector picks the change up on its
  next refresh. Nobody reinstalls anything.

  index.json is NOT trusted by the connector - it only says which files to look at. Trust comes from
  each file's own signature. So a tampered index can remove or rename things, but it can never make
  the connector run something Ajmal did not sign.

.PARAMETER Output
  Where to write. Defaults to the connector's LOCAL tool folder, so running this with no arguments
  publishes straight to your own machine for testing.

.PARAMETER ForUpload
  Write to connector\dist\publish\ instead - the folder you upload to the website.

.EXAMPLE
  .\publish-tools.ps1                # publish locally, for testing
  .\publish-tools.ps1 -ForUpload     # build the folder to upload to the website
#>
[CmdletBinding()]
param(
  [string] $Output,
  [switch] $ForUpload,
  [string] $KeyFile = (Join-Path $env:APPDATA "AJTools\signing\ajtools-signing-key.json")
)

$ErrorActionPreference = "Stop"

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$connector  = Split-Path -Parent $scriptDir
$sourceDir  = Join-Path $connector "tools-source"
$manifest   = Join-Path $sourceDir "tools.json"

if (-not $Output) {
  $Output = if ($ForUpload) { Join-Path $connector "dist\publish" }
            else            { Join-Path $env:APPDATA "AJTools\tools" }
}

if (-not (Test-Path $manifest)) { throw "Tool list not found: $manifest" }
if (-not (Test-Path $KeyFile))  { throw "Signing key not found: $KeyFile" }

# --- load the private key once -----------------------------------------------
$key = Get-Content $KeyFile -Raw | ConvertFrom-Json

$p = New-Object System.Security.Cryptography.RSAParameters
$p.Modulus  = [Convert]::FromBase64String($key.Modulus)
$p.Exponent = [Convert]::FromBase64String($key.Exponent)
$p.D        = [Convert]::FromBase64String($key.D)
$p.P        = [Convert]::FromBase64String($key.P)
$p.Q        = [Convert]::FromBase64String($key.Q)
$p.DP       = [Convert]::FromBase64String($key.DP)
$p.DQ       = [Convert]::FromBase64String($key.DQ)
$p.InverseQ = [Convert]::FromBase64String($key.InverseQ)

$rsa = [System.Security.Cryptography.RSA]::Create()
$rsa.ImportParameters($p)

# --- publish each tool -------------------------------------------------------
if (-not (Test-Path $Output)) { New-Item -ItemType Directory -Force $Output | Out-Null }

$tools = Get-Content $manifest -Raw | ConvertFrom-Json
$index = @()

Write-Host ""
Write-Host "Publishing to: $Output" -ForegroundColor Cyan
Write-Host ""

foreach ($tool in $tools) {
  $codePath = Join-Path $sourceDir $tool.code
  if (-not (Test-Path $codePath)) {
    Write-Host "  SKIPPED $($tool.id) - code file missing: $($tool.code)" -ForegroundColor Yellow
    continue
  }

  # [System.IO.File]::ReadAllText, NOT Get-Content -Raw.
  #
  # Get-Content decorates every string it returns with ETS note properties (PSPath, PSParentPath,
  # PSProvider, ReadCount...). ConvertTo-Json then serialises the DECORATED OBJECT rather than the
  # text, so "code" comes out as {"value":"...","PSPath":{...}} instead of a plain string - and the
  # connector, which expects a string, cannot read it. Measured 2026-08-12: a 1,910-character script
  # produced a 2.2 MB payload this way. ReadAllText returns an undecorated String.
  # Explicit [string] casts on the rest guard the same class of surprise.
  $codeText = [System.IO.File]::ReadAllText((Resolve-Path $codePath).Path)

  $payloadJson = [ordered]@{
    id          = [string] $tool.id
    name        = [string] $tool.name
    panel       = [string] $tool.panel
    description = [string] $tool.description
    code        = $codeText
  } | ConvertTo-Json -Compress -Depth 5

  $payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($payloadJson)

  $signature = $rsa.SignData(
    $payloadBytes,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

  $fileName = "$($tool.id).ajtool"
  [ordered]@{
    payload   = [Convert]::ToBase64String($payloadBytes)
    signature = [Convert]::ToBase64String($signature)
  } | ConvertTo-Json | Out-File -FilePath (Join-Path $Output $fileName) -Encoding utf8

  $index += $fileName
  Write-Host "  signed  $($tool.name)  ->  $fileName" -ForegroundColor Green
}

# A single-item array must still serialise as an array, hence the explicit wrapper - ConvertTo-Json
# unwraps a one-element array into a bare value, which the connector would fail to read as a list.
ConvertTo-Json -InputObject @($index) | Out-File -FilePath (Join-Path $Output "index.json") -Encoding utf8

Write-Host ""
Write-Host "$($index.Count) tool(s) published, plus index.json." -ForegroundColor Green

if ($ForUpload) {
  Write-Host ""
  Write-Host "Next: upload the CONTENTS of this folder to your website, for example"
  Write-Host "  https://yoursite.com/ajtools/tools/"
  Write-Host ""
  Write-Host "Then point connectors at it once with:"
  Write-Host "  .\set-tool-source.ps1 -Url https://yoursite.com/ajtools/tools/"
} else {
  Write-Host "Press 'Check for new tools' in the connector page to pick this up."
}

Write-Host ""
