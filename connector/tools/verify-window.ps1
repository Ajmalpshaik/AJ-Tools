<#
.SYNOPSIS
  Proves the AJ Connect window's styles actually work, without launching Revit.

.DESCRIPTION
  A clean msbuild proves the XAML PARSES. It does not prove the window RUNS. BAML compilation never
  resolves {StaticResource} against the runtime resource tree, so a missing or misspelled key
  compiles perfectly and then throws XamlParseException the first time a template is applied - which,
  for an add-in, is in front of the user.

  This closes that gap: it lifts the <Window.Resources> block out of the XAML source, re-parses it as
  a standalone ResourceDictionary, applies every Style to a real control and forces ApplyTemplate() -
  the moment WPF actually resolves the resources inside a template. It then checks that every
  StaticResource referenced anywhere in the file has a matching key.

  Same technique as AJ Tools' own tools\verify-window-styles.ps1, scoped to this project.

.NOTES
  MUST run with -STA. WPF refuses to create controls on an MTA thread.

.EXAMPLE
  powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File connector\tools\verify-window.ps1
#>
[CmdletBinding()]
param(
  [string] $XamlPath
)

$ErrorActionPreference = "Stop"

if (-not $XamlPath) {
  $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
  $XamlPath  = Join-Path (Split-Path -Parent $scriptDir) "ui\ConnectWindow.xaml"
}

if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
  Write-Host "This must run with -STA. WPF cannot create controls on an MTA thread." -ForegroundColor Red
  exit 2
}

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$xaml = Get-Content $XamlPath -Raw

$openTag  = "<Window.Resources>"
$closeTag = "</Window.Resources>"

$start = $xaml.IndexOf($openTag)
$end   = $xaml.IndexOf($closeTag)

if ($start -lt 0 -or $end -lt 0) {
  Write-Host "No <Window.Resources> block found in $XamlPath - nothing to verify." -ForegroundColor Yellow
  exit 0
}

$block = $xaml.Substring($start, ($end + $closeTag.Length) - $start)

$dictOpen = "<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>"

$block = $block.Replace($openTag, $dictOpen).Replace($closeTag, "</ResourceDictionary>")

$dictionary = [Windows.Markup.XamlReader]::Parse($block)

Write-Host ""
Write-Host "Verifying $(Split-Path -Leaf $XamlPath)" -ForegroundColor Cyan
Write-Host "  keys defined: $($dictionary.Keys.Count)"
Write-Host ""

$failures = 0

foreach ($key in $dictionary.Keys) {
  $item = $dictionary[$key]

  if ($item -is [System.Windows.Style]) {
    try {
      $control = [Activator]::CreateInstance($item.TargetType)
      $control.Style = $item

      # This is the step that matters: building the ControlTemplate is when WPF resolves the
      # StaticResources inside it.
      if ($control -is [System.Windows.FrameworkElement]) { $null = $control.ApplyTemplate() }

      Write-Host ("  OK    {0,-16} ({1})" -f $key, $item.TargetType.Name) -ForegroundColor Green
    }
    catch {
      Write-Host ("  FAIL  {0,-16} -> {1}" -f $key, $_.Exception.Message) -ForegroundColor Red
      $failures++
    }
  }
  else {
    Write-Host ("  OK    {0,-16} ({1})" -f $key, $item.GetType().Name) -ForegroundColor DarkGray
  }
}

Write-Host ""
Write-Host "  Every StaticResource referenced in the file:" -ForegroundColor Cyan

# Strip XML comments first. This file's own comments explain the StaticResource root-element trap and
# contain the literal phrase "StaticResource on this Window tag", which the scan below would otherwise
# report as a missing key called "on" - a false alarm that makes the whole check untrustworthy.
$scannable = [regex]::Replace($xaml, "(?s)<!--.*?-->", "")

$referenced = [regex]::Matches($scannable, "\{\s*StaticResource\s+([A-Za-z0-9_]+)\s*\}") |
              ForEach-Object { $_.Groups[1].Value } |
              Sort-Object -Unique

foreach ($name in $referenced) {
  if ($dictionary.Contains($name)) {
    Write-Host ("    OK      {0}" -f $name) -ForegroundColor Green
  } else {
    Write-Host ("    MISSING {0}  <- would crash at runtime" -f $name) -ForegroundColor Red
    $failures++
  }
}

# Unused keys are harmless, but worth surfacing - usually a leftover or a typo'd reference.
$unused = @($dictionary.Keys | Where-Object { $referenced -notcontains $_ })
if ($unused.Count -gt 0) {
  Write-Host ""
  Write-Host "  Defined but never used (harmless): $($unused -join ', ')" -ForegroundColor DarkYellow
}

Write-Host ""
if ($failures -eq 0) {
  Write-Host "RESULT: all styles build and every resource resolves." -ForegroundColor Green
  exit 0
}

Write-Host "RESULT: $failures problem(s)." -ForegroundColor Red
exit 1
