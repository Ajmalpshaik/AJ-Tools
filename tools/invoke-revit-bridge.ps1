<#
.SYNOPSIS
    Visible shortcut to the AJ Tools AutoDebugger bridge helper.

.DESCRIPTION
    Some agents search the workspace with plain `rg --files`, which ignores dot folders like
    `.claude` by default. This visible wrapper forwards to the real helper in `.claude\tools`
    so simple live-model checks do not rebuild named-pipe code by hand.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\invoke-revit-bridge.ps1 -Ping

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\invoke-revit-bridge.ps1 -CodeFile query.cs
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

$repoRoot = Split-Path -Parent $PSScriptRoot
$helper = Join-Path $repoRoot ".claude\tools\invoke-autodebugger.ps1"
if (-not (Test-Path -LiteralPath $helper)) {
    throw "Missing helper: $helper"
}

$helperArgs = @{ ConnectTimeoutMs = $ConnectTimeoutMs }
if ($Ping) {
    $helperArgs.Ping = $true
} else {
    if (-not [string]::IsNullOrWhiteSpace($Code)) {
        $helperArgs.Code = $Code
    }
    if (-not [string]::IsNullOrWhiteSpace($CodeFile)) {
        $helperArgs.CodeFile = $CodeFile
    }
    if ($AllowDestructive) {
        $helperArgs.AllowDestructive = $true
    }
}

# Invoke in this already-approved PowerShell process. Splatting preserves multi-line C# as one value;
# passing it through a second powershell.exe -File call split it into positional arguments.
& $helper @helperArgs
