<#
.SYNOPSIS
    Fast local caller for the AJ Tools AJ AI bridge.

.DESCRIPTION
    Use this only when the native MCP tool (`mcp__aj-tools-aj-ai__ping` /
    `mcp__aj-tools-aj-ai__run_csharp`) is not exposed in the current agent
    session. It reads the Revit-side discovery file, sends one C# snippet through
    the named pipe, prints the bridge JSON response, and exits.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude\tools\invoke-aj-ai-bridge.ps1 -Ping

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude\tools\invoke-aj-ai-bridge.ps1 -CodeFile .\tmp-query.cs
#>

[CmdletBinding(DefaultParameterSetName = "Code")]
param(
    [Parameter(ParameterSetName = "Ping")]
    [switch]$Ping,

    [Parameter(ParameterSetName = "Code")]
    [string]$Code,

    [Parameter(ParameterSetName = "Code")]
    [string]$CodeFile,

    [Parameter(ParameterSetName = "Code")]
    [switch]$AllowDestructive,

    [int]$ConnectTimeoutMs = 5000
)

$ErrorActionPreference = "Stop"

$discoveryFile = Join-Path $env:APPDATA "AJTools\ajai-bridge.json"
if (-not (Test-Path -LiteralPath $discoveryFile)) {
    throw "AJ AI Bridge is not connected. In Revit, open AJ AI and click Connect AJ AI Bridge, then try again."
}

$info = Get-Content -LiteralPath $discoveryFile -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($info.pipeName) -or [string]::IsNullOrWhiteSpace($info.token)) {
    throw "AJ AI Bridge connection file is malformed. Reconnect from the AJ AI pane in Revit."
}

if ($Ping) {
    $Code = '"pong"'
    $AllowDestructive = $false
} elseif (-not [string]::IsNullOrWhiteSpace($CodeFile)) {
    $Code = Get-Content -LiteralPath $CodeFile -Raw
} elseif ([string]::IsNullOrWhiteSpace($Code)) {
    throw "Provide -Ping, -Code, or -CodeFile."
}

$pipe = $null
$writer = $null
$reader = $null

try {
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
        ".",
        [string]$info.pipeName,
        [System.IO.Pipes.PipeDirection]::InOut)

    $pipe.Connect($ConnectTimeoutMs)

    $writer = [System.IO.StreamWriter]::new($pipe, [System.Text.Encoding]::UTF8)
    $writer.AutoFlush = $true
    $reader = [System.IO.StreamReader]::new($pipe, [System.Text.Encoding]::UTF8)

    $request = @{
        token = [string]$info.token
        code = $Code
        allowDestructive = [bool]$AllowDestructive
    } | ConvertTo-Json -Compress

    $writer.WriteLine($request)
    $reader.ReadLine()
}
finally {
    foreach ($obj in @($reader, $writer, $pipe)) {
        if ($null -ne $obj) {
            try { $obj.Dispose() } catch {}
        }
    }
}
