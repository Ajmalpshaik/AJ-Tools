<#
    verify-window-styles.ps1 - AJ Tools

    Companion to verify-wpf-styles.ps1. That one checks the two SHARED dictionaries; this one checks the
    windows that keep their styles inside their own <Window.Resources> and merge nothing:
        UI/AboutWindow.xaml, UI/GraphicsTools/GraphicsOverrideWindow.xaml, GameMode/GameKeySettingsWindow.xaml

    Those can't be loaded by pack URI as a dictionary, and instantiating the Window itself would drag in
    Revit. So this lifts the <Window.Resources> block straight out of the XAML SOURCE, re-parses it as a
    standalone ResourceDictionary, then applies every Style and ControlTemplate it finds to a real control
    and forces the template to build - which is the moment WPF resolves the {StaticResource} references
    inside it. That is the failure a clean msbuild cannot catch.

    It discovers the styles itself from each dictionary's TargetType, so new styles are covered
    automatically with no list to maintain.

    USE IT
      powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File tools\verify-window-styles.ps1
      Exit code 0 = clean, otherwise the number of failures.

    Author  : Ajmal P.S.   |   Created 2026-08-05   |   All Rights Reserved
#>

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    Write-Host "ERROR: must run with -STA (WPF cannot create controls on an MTA thread)." -ForegroundColor Red
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$targets = @(
    "src\UI\AboutWindow.xaml",
    "src\UI\GraphicsTools\GraphicsOverrideWindow.xaml",
    "src\GameMode\GameKeySettingsWindow.xaml"
)

$script:fail = 0

function Get-WindowResourceDictionary($path) {
    $text = [IO.File]::ReadAllText($path)
    $open  = $text.IndexOf('<Window.Resources>')
    $close = $text.LastIndexOf('</Window.Resources>')
    if ($open -lt 0 -or $close -lt 0) { return $null }
    $open += '<Window.Resources>'.Length
    $inner = $text.Substring($open, $close - $open)

    $wrapper = @"
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework">
$inner
</ResourceDictionary>
"@
    return [System.Windows.Markup.XamlReader]::Parse($wrapper)
}

function Test-Applied($ctrl, $label, $expectTemplate) {
    $ctrl.Measure((New-Object System.Windows.Size(400, 200)))
    $ctrl.Arrange((New-Object System.Windows.Rect(0, 0, 400, 200)))
    $null = $ctrl.ApplyTemplate()
    if ($expectTemplate -and [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($ctrl) -lt 1) {
        "    EMPTY : $label (custom template produced no visual tree)"; $script:fail++; return
    }
    "    OK    : $label"
}

function Style-HasTemplate($style) {
    $s = $style
    while ($s) {
        foreach ($setter in $s.Setters) {
            if ($setter.Property -and $setter.Property.Name -eq 'Template') { return $true }
        }
        $s = $s.BasedOn
    }
    return $false
}

foreach ($rel in $targets) {
    $path = Join-Path $repoRoot $rel
    "--- $rel ---"
    try {
        $rd = Get-WindowResourceDictionary $path
    } catch {
        "    PARSE FAIL: $($_.Exception.Message)"; $script:fail++; continue
    }
    if ($null -eq $rd) { "    no <Window.Resources> found"; continue }

    $checked = 0
    foreach ($key in @($rd.Keys)) {
        $entry = $rd[$key]

        if ($entry -is [System.Windows.Style]) {
            $tt = $entry.TargetType
            if ($null -eq $tt -or $tt.IsAbstract) { continue }
            try {
                $c = [Activator]::CreateInstance($tt)
                if ($c -isnot [System.Windows.FrameworkElement]) { continue }
                $c.Style = $entry
                Test-Applied $c "$key ($($tt.Name))" (Style-HasTemplate $entry)
                $checked++
            } catch {
                "    FAIL  : $key -> $($_.Exception.Message)"; $script:fail++
            }
        }
        elseif ($entry -is [System.Windows.Controls.ControlTemplate]) {
            $tt = $entry.TargetType
            if ($null -eq $tt -or $tt.IsAbstract) { continue }
            try {
                $c = [Activator]::CreateInstance($tt)
                $c.Template = $entry
                Test-Applied $c "$key (ControlTemplate for $($tt.Name))" $true
                $checked++
            } catch {
                "    FAIL  : $key -> $($_.Exception.Message)"; $script:fail++
            }
        }
    }

    # Implicit styles are keyed by Type, not by name - they are included in $rd.Keys above.
    "    ($checked styles/templates exercised)"
}

""
if ($script:fail -eq 0) {
    Write-Host "RESULT: all window-local templates built, every StaticResource resolved." -ForegroundColor Green
} else {
    Write-Host "RESULT: $($script:fail) FAILURE(S) - do NOT deploy." -ForegroundColor Red
}
exit $script:fail
