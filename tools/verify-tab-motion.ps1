<#
    verify-tab-motion.ps1 - AJ Tools

    Regression guard for Helpers/TabMotionHelper.cs.

    THE TRAP IT PROTECTS: Selector.SelectionChanged is a ROUTED event, so a ComboBox or ListBox sitting
    inside a tab bubbles its own selection change up to the TabControl. A naive handler replays the whole
    tab transition every time the user picks a value from a dropdown inside the tab. The helper guards
    against it by checking e.OriginalSource; this script proves the guard actually holds, by building a
    TabControl with a ComboBox inside the first tab and asserting that changing the dropdown does NOT
    animate while switching the tab DOES - and that neither selection is disturbed.

    Run after any change to TabMotionHelper:
      powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File tools\verify-tab-motion.ps1
    Exit code 0 = pass. Build the Release configuration first - the built DLL is the input.

    Author  : Ajmal P.S.   |   Created 2026-08-05   |   All Rights Reserved
#>

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$asm = [Reflection.Assembly]::LoadFrom("D:\Ajmal\Revit Addins\src\bin\Release\AJ Tools.dll")
$app = New-Object System.Windows.Application

# Build a tab control with a ComboBox living INSIDE the first tab - that is the trap case.
$combo = New-Object System.Windows.Controls.ComboBox
$null = $combo.Items.Add("one"); $null = $combo.Items.Add("two")
$combo.SelectedIndex = 0

$tab1 = New-Object System.Windows.Controls.TabItem; $tab1.Header = "First";  $tab1.Content = $combo
$tab2 = New-Object System.Windows.Controls.TabItem; $tab2.Header = "Second"; $tab2.Content = "plain text"

$tc = New-Object System.Windows.Controls.TabControl
$null = $tc.Items.Add($tab1); $null = $tc.Items.Add($tab2)
$tc.SelectedIndex = 0

$grid = New-Object System.Windows.Controls.Grid
$null = $grid.Children.Add($tc)
$grid.Measure((New-Object System.Windows.Size(500,400)))
$grid.Arrange((New-Object System.Windows.Rect(0,0,500,400)))
$grid.UpdateLayout()

# Attach exactly the way the helper does for a loaded window.
$t = $asm.GetType("AJTools.Utils.TabMotionHelper")
if ($null -eq $t) { "FAIL: TabMotionHelper type not found"; exit 1 }
$attach = $t.GetMethod("AttachToTabControls", [Reflection.BindingFlags]"NonPublic,Static")
$attach.Invoke($null, @([System.Windows.DependencyObject]$grid))

$hostEl = $tc.Template.FindName("PART_SelectedContentHost", $tc)
if ($null -eq $hostEl) { "FAIL: content host not found"; exit 1 }

function Reset-Animations {
    $hostEl.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $null)
    if ($hostEl.RenderTransform -is [System.Windows.Media.TranslateTransform]) {
        $hostEl.RenderTransform.BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $null)
    }
}

Reset-Animations
"baseline                      -> animating: {0}  (expect False)" -f $hostEl.HasAnimatedProperties

# THE TRAP: a dropdown inside the tab bubbles Selector.SelectionChanged up to the TabControl.
$combo.SelectedIndex = 1
$grid.UpdateLayout()
$afterCombo = $hostEl.HasAnimatedProperties
"changed a dropdown INSIDE tab -> animating: {0}  (expect False - must NOT replay)" -f $afterCombo

# The real thing: switching tabs.
$tc.SelectedIndex = 1
$grid.UpdateLayout()
$afterTab = $hostEl.HasAnimatedProperties
"switched tab                  -> animating: {0}  (expect True)" -f $afterTab

# And the selection itself must still work - motion must never eat the tab change.
"selected index after switch   -> {0}  (expect 1)" -f $tc.SelectedIndex
"dropdown value after change   -> {0}  (expect two)" -f $combo.SelectedItem

$ok = (-not $afterCombo) -and $afterTab -and ($tc.SelectedIndex -eq 1) -and ($combo.SelectedItem -eq "two")
""
if ($ok) { Write-Host "RESULT: PASS - transition fires on tab change only, selection unaffected." -ForegroundColor Green; exit 0 }
else     { Write-Host "RESULT: FAIL" -ForegroundColor Red; exit 1 }
