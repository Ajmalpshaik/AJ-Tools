<#
.SYNOPSIS
  Signs a tool so the AJ Tools Connector will run it. Ajmal runs this; nobody else can.

.DESCRIPTION
  Produces a ".ajtool" file: { "payload": <base64 of the tool JSON>, "signature": <base64 RSA-SHA256> }.

  The signature covers the payload BYTES EXACTLY AS WRITTEN. That is why the payload is carried as
  base64 rather than as a nested object - it removes any chance of the signer and the connector
  disagreeing about key order or spacing, which would otherwise mean either false rejections or, far
  worse, a way to alter a tool without breaking its signature.

  The private key lives at %APPDATA%\AJTools\signing\ajtools-signing-key.json and never leaves this
  machine. It IS the authority to publish tools - anyone holding it can run code on every machine
  that installed the connector. Do not commit it, copy it to a shared drive, or paste it anywhere.

.EXAMPLE
  .\sign-tool.ps1 -Id unhide-all -Name "Unhide All" -Panel View `
                  -Description "Restore hidden elements in the active view." `
                  -CodeFile ..\tools-source\unhide-all.cs
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Id,
  [Parameter(Mandatory)] [string] $Name,
  [Parameter(Mandatory)] [string] $CodeFile,
  [string] $Panel = "Tools",
  [string] $Description = "",

  # Default output is the connector's local tool folder, so signing a tool publishes it immediately
  # for testing. Point this at the website's upload folder when going live.
  [string] $OutFolder = (Join-Path $env:APPDATA "AJTools\tools"),

  [string] $KeyFile = (Join-Path $env:APPDATA "AJTools\signing\ajtools-signing-key.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CodeFile)) { throw "Code file not found: $CodeFile" }
if (-not (Test-Path $KeyFile))  { throw "Signing key not found: $KeyFile" }

# [System.IO.File]::ReadAllText, NOT Get-Content -Raw. Get-Content decorates its output string with
# ETS note properties (PSPath, PSParentPath, ...) and ConvertTo-Json then serialises that whole
# object instead of the text - "code" becomes {"value":"...","PSPath":{...}}, which the connector
# cannot read as a string. Measured 2026-08-12: a 1,910-character script became a 2.2 MB payload.
$code = [System.IO.File]::ReadAllText((Resolve-Path $CodeFile).Path)

# --- build the payload -------------------------------------------------------
# Compress removes insignificant whitespace. It does not matter what shape this is, only that the
# exact bytes written below are the exact bytes signed.
$payloadJson = [ordered]@{
  id          = [string] $Id
  name        = [string] $Name
  panel       = [string] $Panel
  description = [string] $Description
  code        = $code
} | ConvertTo-Json -Compress -Depth 5

$payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($payloadJson)

# --- sign it -----------------------------------------------------------------
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

$signature = $rsa.SignData(
  $payloadBytes,
  [System.Security.Cryptography.HashAlgorithmName]::SHA256,
  [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

# --- write the published file ------------------------------------------------
if (-not (Test-Path $OutFolder)) { New-Item -ItemType Directory -Force $OutFolder | Out-Null }

$outFile = Join-Path $OutFolder "$Id.ajtool"
[ordered]@{
  payload   = [Convert]::ToBase64String($payloadBytes)
  signature = [Convert]::ToBase64String($signature)
} | ConvertTo-Json | Out-File -FilePath $outFile -Encoding utf8

Write-Host "Signed and published:" -ForegroundColor Green
Write-Host "  tool   : $Name  ($Id)"
Write-Host "  panel  : $Panel"
Write-Host "  code   : $($code.Length) chars"
Write-Host "  output : $outFile"
Write-Host ""
Write-Host "Press 'Check for new tools' in the connector page to pick it up."
