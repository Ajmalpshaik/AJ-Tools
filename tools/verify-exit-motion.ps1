<#
    verify-exit-motion.ps1 - AJ Tools

    Regression guard for WindowMotionHelper.AttachStandardExit.

    THE TRAP IT PROTECTS: an exit animation has to CANCEL the window's own Closing, animate, then
    re-issue the close - and WPF THROWS DialogResult AWAY when a close is cancelled. Measured on real
    dialogs 2026-08-05: a window that sets DialogResult = true and has its Closing cancelled returns
    FALSE from ShowDialog(). Every AJ Tools command is written as
        if (window.ShowDialog() == true) { ...do the work... }
    so getting this wrong makes every Run button behave like Cancel - the window opens, closes, and the
    tool silently does nothing. That is a data-loss-shaped bug with no error message.

    This script runs the REAL helper (by reflection, it is internal) against real dialogs and asserts the
    answer matches a no-animation control group in every case, including the Click+IsCancel button shape
    that raises Closing twice per click.

    Run after ANY change to WindowMotionHelper:
      powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File tools\verify-exit-motion.ps1
    Exit code 0 = pass. Build the Release configuration first - the built DLL is the input.

    Author  : Ajmal P.S.   |   Created 2026-08-05   |   All Rights Reserved
#>

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$repoRoot = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $repoRoot "src\bin\Release\AJ Tools.dll"
if (-not (Test-Path $dll)) {
    Write-Host "ERROR: build not found at $dll - build the Release configuration first." -ForegroundColor Red
    exit 1
}
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    Write-Host "ERROR: must run with -STA." -ForegroundColor Red
    exit 1
}

$asm = [Reflection.Assembly]::LoadFrom($dll)
$helper = $asm.GetType("AJTools.Utils.WindowMotionHelper")
if ($null -eq $helper) { Write-Host "FAIL: WindowMotionHelper not found" -ForegroundColor Red; exit 1 }
$attachExit = $helper.GetMethod("AttachStandardExit", [Reflection.BindingFlags]"NonPublic,Static")
if ($null -eq $attachExit) { Write-Host "FAIL: AttachStandardExit not found" -ForegroundColor Red; exit 1 }

Add-Type -ReferencedAssemblies PresentationFramework, PresentationCore, WindowsBase, System.Xaml @"
using System;
using System.Windows;
using System.Windows.Controls;

public static class ExitHarness
{
    // setResult: "true" | "false" | "none".  alsoIsCancelPath reproduces a button that is both
    // Click= (handler closes) and IsCancel="True", which raises Closing twice per click.
    public static Window Build(string setResult, bool alsoIsCancelPath)
    {
        Window w = new Window();
        w.Width = 240; w.Height = 140; w.ShowInTaskbar = false;
        w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        w.Content = new Grid();   // the helper animates the root CONTENT element

        w.Loaded += delegate
        {
            if (setResult == "true") w.DialogResult = true;
            else if (setResult == "false") w.DialogResult = false;
            else w.Close();

            if (alsoIsCancelPath) { try { w.DialogResult = false; } catch { } }
        };
        return w;
    }

    // A window that is BUSY must be able to veto its own close, and the exit animation must respect
    // that veto instead of animating and then forcing the window shut anyway. This matters because a
    // progress-reporting loop pumps the dispatcher, which runs a real Win32 message loop - so the
    // title-bar X and Esc DO reach the window mid-scan (measured). Returns true if the veto held.
    public static bool VetoHolds(Action<Window> attachExit)
    {
        Window w = new Window();
        w.Width = 240; w.Height = 140; w.ShowInTaskbar = false;
        w.Content = new Grid();

        bool busy = true;
        int closeAttempts = 0;

        // Subscribed BEFORE the exit helper, exactly as the purge windows do it.
        w.Closing += delegate(object s, System.ComponentModel.CancelEventArgs e)
        {
            closeAttempts++;
            if (busy) e.Cancel = true;
        };
        attachExit(w);

        bool stillOpenWhileBusy = false;
        w.Loaded += delegate
        {
            w.Close();                                   // user hits X mid-scan
            stillOpenWhileBusy = w.IsVisible;            // must still be open
            busy = false;                                // scan finishes
            w.Close();                                   // now it may close
        };

        w.ShowDialog();
        return stillOpenWhileBusy && closeAttempts >= 2;
    }
}
"@

function Run-Case {
    param([string]$Label, [string]$Set, [bool]$IsCancelPath, [bool]$WithExit)

    $w = [ExitHarness]::Build($Set, $IsCancelPath)
    if ($WithExit) { $attachExit.Invoke($null, @([System.Windows.Window]$w)) | Out-Null }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $returned = $w.ShowDialog()
    $sw.Stop()

    $text = if ($null -eq $returned) { "null" } else { "$returned" }
    Write-Host ("  {0,-38} returned={1,-5} ({2} ms)" -f $Label, $text, $sw.ElapsedMilliseconds)
    return ,@($text, $sw.ElapsedMilliseconds)
}

Write-Host "--- CONTROL: no exit motion (today's behaviour) ---"
$c1 = Run-Case "Run sets DialogResult=true"      "true"  $false $false
$c2 = Run-Case "Cancel sets DialogResult=false"  "false" $false $false
$c3 = Run-Case "plain Close(), no result"        "none"  $false $false
$c4 = Run-Case "Click + IsCancel double-close"   "true"  $true  $false

Write-Host "--- WITH the real WindowMotionHelper exit ---"
$e1 = Run-Case "Run sets DialogResult=true"      "true"  $false $true
$e2 = Run-Case "Cancel sets DialogResult=false"  "false" $false $true
$e3 = Run-Case "plain Close(), no result"        "none"  $false $true
$e4 = Run-Case "Click + IsCancel double-close"   "true"  $true  $true

Write-Host "--- busy window must be able to veto its own close ---"
$vetoHeld = [ExitHarness]::VetoHolds([Action[System.Windows.Window]]{ param($w) $attachExit.Invoke($null, @([System.Windows.Window]$w)) })
Write-Host ("  {0,-38} veto held={1}" -f "close attempted while busy", $vetoHeld)

Write-Host ""
$mismatch = @()
if (-not $vetoHeld) { $mismatch += "a busy window could NOT refuse its close - the exit helper overrode the veto" }
if ($e1[0] -ne $c1[0]) { $mismatch += "Run(true): control=$($c1[0]) exit=$($e1[0])" }
if ($e2[0] -ne $c2[0]) { $mismatch += "Cancel(false): control=$($c2[0]) exit=$($e2[0])" }
if ($e3[0] -ne $c3[0]) { $mismatch += "plain Close: control=$($c3[0]) exit=$($e3[0])" }
if ($e4[0] -ne $c4[0]) { $mismatch += "Click+IsCancel: control=$($c4[0]) exit=$($e4[0])" }

# The window must actually still be closing - a helper that hangs would show up as a huge elapsed time.
if ($e1[1] -gt 3000) { $mismatch += "exit took $($e1[1]) ms - the close may be stalling" }

if ($mismatch.Count -eq 0) {
    Write-Host "RESULT: PASS - every dialog result matches the no-animation control." -ForegroundColor Green
    exit 0
} else {
    foreach ($m in $mismatch) { Write-Host "  MISMATCH: $m" -ForegroundColor Red }
    Write-Host "RESULT: FAIL - exit motion changes what dialogs return. DO NOT SHIP." -ForegroundColor Red
    exit 1
}
