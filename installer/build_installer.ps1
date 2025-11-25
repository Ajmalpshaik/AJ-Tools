# Build Inno Setup installer for AJ Tools
$innoCompiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$script = 'AJToolsInstaller.iss'
$cwd = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $cwd
if (-Not (Test-Path $innoCompiler)) { Write-Error "Inno Setup compiler not found at $innoCompiler. Install Inno Setup or edit the path in this script."; exit 1 }
& "$innoCompiler" $script
Pop-Location
