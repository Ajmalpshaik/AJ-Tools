<#
    verify-wpf-styles.ps1 - AJ Tools

    WHY THIS EXISTS
    A clean msbuild proves the XAML parses. It does NOT prove the styles WORK: BAML compilation never
    resolves {StaticResource} lookups against the runtime resource tree, so a missing or mistyped key
    compiles perfectly and then throws XamlParseException the first time a template is applied. That has
    already taken the whole add-in down during Revit's OnStartup once (suite v1.16.0, 2026-07-18) because
    AiShellPaneProvider builds the AI pane unconditionally at startup.

    WHAT IT DOES
    Loads the two shared resource dictionaries straight out of the BUILT AJ Tools.dll, then forces every
    style to instantiate its ControlTemplate - which is the moment WPF resolves the StaticResources inside
    it. Anything broken shows up here, on this machine, in a couple of seconds, without launching Revit.

    USE IT
      1. Build first (the DLL is the input):
         msbuild "src\AJ Tools.csproj" -p:Configuration=Release -p:Platform=x64 -p:SkipAjToolsAutoDeploy=true
      2. powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File tools\verify-wpf-styles.ps1
         (-STA is required: WPF will not create controls on an MTA thread.)
      Exit code 0 = all good, otherwise the number of failures.

    Run it after ANY edit to ModernStyles.xaml or SoftUiStyles.xaml. Add new style keys to the lists at
    the bottom as they are created.

    NOTE FOR WHOEVER EDITS THIS: ResourceDictionary implements IDictionary, so in PowerShell
    "$rd.Source = $uri" silently adds a dictionary ENTRY called "Source" instead of setting the property,
    and the dictionary loads empty. The property must be set through reflection - see Load-Dict below.

    Author  : Ajmal P.S.   |   Created 2026-08-05   |   All Rights Reserved
#>

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    Write-Host "ERROR: must run with -STA (WPF cannot create controls on an MTA thread)." -ForegroundColor Red
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $repoRoot "src\bin\Release\AJ Tools.dll"
if (-not (Test-Path $dll)) {
    Write-Host "ERROR: build not found at $dll - build the Release configuration first." -ForegroundColor Red
    exit 1
}

$asm = [Reflection.Assembly]::LoadFrom($dll)
"Verifying {0} v{1}" -f $asm.GetName().Name, $asm.GetName().Version

# Creating an Application registers the pack:// scheme and gives templates a resource context.
$app = New-Object System.Windows.Application

function Load-Dict($relPath) {
    $rd  = New-Object System.Windows.ResourceDictionary
    $uri = New-Object System.Uri("pack://application:,,,/AJ Tools;component/$relPath")
    [System.Windows.ResourceDictionary].GetProperty('Source').SetValue($rd, $uri, $null)
    return $rd
}

$script:fail = 0

function Test-Style($dict, $key, $ctrlType, $label) {
    try {
        $style = $dict[$key]
        if ($null -eq $style) { "  MISSING KEY : $label"; $script:fail++; return }
        $c = New-Object $ctrlType
        $c.Style = $style
        $c.Measure((New-Object System.Windows.Size(400, 200)))
        $c.Arrange((New-Object System.Windows.Rect(0, 0, 400, 200)))
        $null = $c.ApplyTemplate()

        # Only a style that actually sets its own Template can be expected to build a visual tree here.
        # A colours-only style (e.g. SoftUiStyles' ProgressBarStyle) falls back to the default theme
        # template, which is not applied outside a real window - that is correct, not a failure.
        $hasTemplate = $false
        foreach ($s in $style.Setters) {
            if ($s.Property -and $s.Property.Name -eq 'Template') { $hasTemplate = $true; break }
        }
        if (-not $hasTemplate) {
            $b = $style.BasedOn
            while ($b -and -not $hasTemplate) {
                foreach ($s in $b.Setters) {
                    if ($s.Property -and $s.Property.Name -eq 'Template') { $hasTemplate = $true; break }
                }
                $b = $b.BasedOn
            }
        }

        if (-not $hasTemplate) { "  OK          : $label (colours only, keeps default chrome)"; return }

        if ([System.Windows.Media.VisualTreeHelper]::GetChildrenCount($c) -lt 1) {
            "  EMPTY       : $label (custom template produced no visual tree)"; $script:fail++; return
        }
        "  OK          : $label"
    } catch {
        "  FAIL        : $label -> $($_.Exception.Message)"
        $script:fail++
    }
}

$modern = Load-Dict "UI/ModernStyles.xaml"
$soft   = Load-Dict "AiShell/Views/SoftUiStyles.xaml"

"--- UI/ModernStyles.xaml ({0} keys) - used by 29 windows ---" -f $modern.Count
$app.Resources.MergedDictionaries.Add($modern)
Test-Style $modern 'ButtonBase'                'System.Windows.Controls.Button'       'ButtonBase'
Test-Style $modern 'ModernButton'              'System.Windows.Controls.Button'       'ModernButton'
Test-Style $modern 'PrimaryButton'             'System.Windows.Controls.Button'       'PrimaryButton'
Test-Style $modern 'SecondaryButton'           'System.Windows.Controls.Button'       'SecondaryButton'
Test-Style $modern 'SuccessButton'             'System.Windows.Controls.Button'       'SuccessButton'
Test-Style $modern 'DangerButton'              'System.Windows.Controls.Button'       'DangerButton'
Test-Style $modern 'CompactPrimaryButton'      'System.Windows.Controls.Button'       'CompactPrimaryButton'
Test-Style $modern 'CompactSecondaryButton'    'System.Windows.Controls.Button'       'CompactSecondaryButton'
Test-Style $modern 'WindowControlButton'       'System.Windows.Controls.Button'       'WindowControlButton'
Test-Style $modern 'WindowCloseButton'         'System.Windows.Controls.Button'       'WindowCloseButton'
Test-Style $modern 'ModernTextBox'             'System.Windows.Controls.TextBox'      'ModernTextBox'
Test-Style $modern 'ModernCheckBox'            'System.Windows.Controls.CheckBox'     'ModernCheckBox'
Test-Style $modern 'ToggleSwitchCheckBox'      'System.Windows.Controls.CheckBox'     'ToggleSwitchCheckBox'
Test-Style $modern 'ModernListCheckBox'        'System.Windows.Controls.CheckBox'     'ModernListCheckBox'
Test-Style $modern 'ModernRadioButton'         'System.Windows.Controls.RadioButton'  'ModernRadioButton'
Test-Style $modern 'ModernGroupBox'            'System.Windows.Controls.GroupBox'     'ModernGroupBox'
Test-Style $modern 'ModernListBox'             'System.Windows.Controls.ListBox'      'ModernListBox'
Test-Style $modern 'ModernListBoxItem'         'System.Windows.Controls.ListBoxItem'  'ModernListBoxItem'
Test-Style $modern 'ModernListBoxItemSuccess'  'System.Windows.Controls.ListBoxItem'  'ModernListBoxItemSuccess'
Test-Style $modern ([System.Windows.Controls.ComboBox])     'System.Windows.Controls.ComboBox'     'ComboBox (implicit)'
Test-Style $modern ([System.Windows.Controls.ComboBoxItem]) 'System.Windows.Controls.ComboBoxItem' 'ComboBoxItem (implicit)'
Test-Style $modern ([System.Windows.Controls.TabItem])      'System.Windows.Controls.TabItem'      'TabItem (implicit)'
Test-Style $modern ([System.Windows.Controls.ProgressBar])  'System.Windows.Controls.ProgressBar'  'ProgressBar (implicit)'

"--- AiShell/Views/SoftUiStyles.xaml ({0} keys) - AI pane, AI Settings, Saved Scripts ---" -f $soft.Count
$app.Resources.MergedDictionaries.Clear()
$app.Resources.MergedDictionaries.Add($soft)
Test-Style $soft 'SoftButtonBase'        'System.Windows.Controls.Button'       'SoftButtonBase'
Test-Style $soft 'PrimaryButtonStyle'    'System.Windows.Controls.Button'       'PrimaryButtonStyle'
Test-Style $soft 'SecondaryButtonStyle'  'System.Windows.Controls.Button'       'SecondaryButtonStyle'
Test-Style $soft 'WarningButtonStyle'    'System.Windows.Controls.Button'       'WarningButtonStyle'
Test-Style $soft 'SoftTextBoxStyle'      'System.Windows.Controls.TextBox'      'SoftTextBoxStyle'
Test-Style $soft 'SoftMonoTextBoxStyle'  'System.Windows.Controls.TextBox'      'SoftMonoTextBoxStyle'
Test-Style $soft 'SoftPasswordBoxStyle'  'System.Windows.Controls.PasswordBox'  'SoftPasswordBoxStyle'
Test-Style $soft 'SoftComboBoxStyle'     'System.Windows.Controls.ComboBox'     'SoftComboBoxStyle'
Test-Style $soft 'SoftComboBoxItemStyle' 'System.Windows.Controls.ComboBoxItem' 'SoftComboBoxItemStyle'
Test-Style $soft 'ProgressBarStyle'      'System.Windows.Controls.ProgressBar'  'ProgressBarStyle'

""
if ($script:fail -eq 0) {
    Write-Host "RESULT: all templates built, every StaticResource resolved." -ForegroundColor Green
} else {
    Write-Host "RESULT: $($script:fail) FAILURE(S) - do NOT deploy." -ForegroundColor Red
}
exit $script:fail
