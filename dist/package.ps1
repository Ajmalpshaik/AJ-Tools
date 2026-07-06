param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version,
    [switch]$SkipBuild,
    [switch]$IncludeSymbols
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $repoRoot "src\AJ Tools.csproj"
$modernProjectPath = Join-Path $repoRoot "src\AJ Tools.Modern.csproj"
$buildOutputDir = Join-Path $repoRoot "src\bin\$Configuration"
$distDir = $scriptDir
$payloadRoot = Join-Path $distDir "Payload"
$resourcesSource = Join-Path $repoRoot "src\Resources"
$addinTemplate = Join-Path $repoRoot "Addin\AJ Tools.addin"
$modernRevitVersions = @(2025, 2026, 2027)
$blockedDllNames = @("RevitAPI.dll", "RevitAPIUI.dll")

function Resolve-DotNetPath {
    $localDotNet = Join-Path $repoRoot ".dotnet-sdk\dotnet.exe"
    if (Test-Path -LiteralPath $localDotNet) {
        return $localDotNet
    }

    $dotNetCmd = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if (-not $dotNetCmd) {
        $dotNetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    }

    if ($dotNetCmd) {
        return $dotNetCmd.Source
    }

    throw "dotnet SDK was not found. Install .NET 8 SDK for Revit 2025-2026 and .NET 10 SDK for Revit 2027, or place a local SDK at '$localDotNet'."
}

function Copy-PayloadFromOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDir
    )

    if (-not (Test-Path -LiteralPath (Join-Path $SourceDir "AJ Tools.dll"))) {
        throw "Build output missing AJ Tools.dll in '$SourceDir'."
    }

    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null

    $dllPayload = Get-ChildItem -LiteralPath $SourceDir -Filter *.dll -File |
        Where-Object { $blockedDllNames -notcontains $_.Name }
    $pdbPayload = Get-ChildItem -LiteralPath $SourceDir -Filter *.pdb -File -ErrorAction SilentlyContinue

    if (-not ($dllPayload | Where-Object { $_.Name -eq "AJ Tools.dll" })) {
        throw "AJ Tools.dll not found in payload source '$SourceDir'."
    }

    foreach ($dll in $dllPayload) {
        Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $DestinationDir $dll.Name) -Force
    }
    if ($IncludeSymbols) {
        foreach ($pdb in $pdbPayload) {
            Copy-Item -LiteralPath $pdb.FullName -Destination (Join-Path $DestinationDir $pdb.Name) -Force
        }
    }
}

if (-not $SkipBuild) {
    $buildToolPath = $null

    $msbuildCmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if (-not $msbuildCmd) {
        $msbuildCmd = Get-Command msbuild -ErrorAction SilentlyContinue
    }
    if ($msbuildCmd) {
        $buildToolPath = $msbuildCmd.Source
    } else {
        $vsWherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
        if (Test-Path -LiteralPath $vsWherePath) {
            $vsMsBuildPath = & $vsWherePath -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
            if (-not [string]::IsNullOrWhiteSpace($vsMsBuildPath)) {
                $buildToolPath = $vsMsBuildPath
            }
        }
    }
    if (-not $buildToolPath) {
        throw "MSBuild was not found. Install Visual Studio Build Tools, or build manually and rerun with -SkipBuild."
    }

    Write-Host "Building $Configuration configuration..."
    $buildArgs = @(
        $projectPath,
        "/t:Restore;Build",
        "/p:Configuration=$Configuration",
        "/p:Platform=AnyCPU",
        "/p:SkipRevitAddinDeploy=true",
        "/p:SkipAjToolsAutoDeploy=true"
    )

    & $buildToolPath @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $modernProjectPath)) {
        throw "Modern project missing at '$modernProjectPath'."
    }

    $dotNetPath = Resolve-DotNetPath
    foreach ($revitVersion in $modernRevitVersions) {
        Write-Host "Building modern Revit $revitVersion $Configuration configuration..."
        & $dotNetPath build $modernProjectPath -c $Configuration "/p:RevitVersion=$revitVersion" "/p:SkipRevitAddinDeploy=true" "/p:SkipAjToolsAutoDeploy=true"
        if ($LASTEXITCODE -ne 0) {
            throw "Modern Revit $revitVersion build failed with exit code $LASTEXITCODE."
        }
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $buildOutputDir "AJ Tools.dll"))) {
    throw "Build output missing AJ Tools.dll in '$buildOutputDir'."
}

Get-ChildItem -LiteralPath $distDir -File |
    Where-Object { $_.Extension -in @(".dll", ".pdb") } |
    Remove-Item -Force
Remove-Item -LiteralPath $payloadRoot -Recurse -Force -ErrorAction SilentlyContinue

$legacyPayloadDir = Join-Path $payloadRoot "2020-2024"
Copy-PayloadFromOutput -SourceDir $buildOutputDir -DestinationDir $legacyPayloadDir

# Keep the legacy payload at the package root as a fallback for older install scripts.
Copy-PayloadFromOutput -SourceDir $buildOutputDir -DestinationDir $distDir

foreach ($revitVersion in $modernRevitVersions) {
    $modernOutputDir = Join-Path $repoRoot "src\bin\Modern\$revitVersion\$Configuration"
    Copy-PayloadFromOutput -SourceDir $modernOutputDir -DestinationDir (Join-Path $payloadRoot $revitVersion)
}

if (-not (Test-Path -LiteralPath $payloadRoot)) {
    throw "Payload folder was not created."
}

if (-not (Test-Path -LiteralPath $resourcesSource)) {
    throw "Resources folder not found at '$resourcesSource'."
}
Remove-Item -LiteralPath (Join-Path $distDir "Resources") -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath $resourcesSource -Destination $distDir -Recurse -Force

if (-not (Test-Path -LiteralPath $addinTemplate)) {
    throw "Add-in manifest template missing at '$addinTemplate'."
}
Copy-Item -LiteralPath $addinTemplate -Destination (Join-Path $distDir "AJ Tools.addin") -Force

if ([string]::IsNullOrWhiteSpace($Version)) {
    $assemblyInfoPath = Join-Path $repoRoot "src\Properties\AssemblyInfo.cs"
    $versionLine = Select-String -Path $assemblyInfoPath -Pattern 'AssemblyVersion\("([^"]+)"\)' | Select-Object -First 1
    if ($versionLine -and $versionLine.Matches.Count -gt 0) {
        $fullVersion = $versionLine.Matches[0].Groups[1].Value
        $segments = $fullVersion.Split(".")
        if ($segments.Count -ge 3) {
            $Version = "{0}.{1}.{2}" -f $segments[0], $segments[1], $segments[2]
        } else {
            $Version = $fullVersion
        }
    } else {
        $Version = Get-Date -Format "yyyy.MM.dd"
    }
}

$releaseDir = Join-Path $distDir "release"
$packageRootName = "AJ-Tools-v$Version"
$packageRootPath = Join-Path $releaseDir $packageRootName
$zipPath = Join-Path $releaseDir "$packageRootName.zip"

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Get-ChildItem -LiteralPath $releaseDir -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $packageRootPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $packageRootPath -Force | Out-Null

$staticFiles = @(
    "install.ps1",
    "install.cmd",
    "install-all-users.cmd",
    "uninstall.ps1",
    "uninstall.cmd",
    "uninstall-all-users.cmd",
    "AJ Tools.addin"
)

foreach ($name in $staticFiles) {
    $source = Join-Path $distDir $name
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required file '$name' missing in dist."
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $packageRootPath $name) -Force
}

foreach ($dll in (Get-ChildItem -LiteralPath $distDir -Filter *.dll -File)) {
    Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $packageRootPath $dll.Name) -Force
}
if ($IncludeSymbols) {
    foreach ($pdb in (Get-ChildItem -LiteralPath $distDir -Filter *.pdb -File -ErrorAction SilentlyContinue)) {
        Copy-Item -LiteralPath $pdb.FullName -Destination (Join-Path $packageRootPath $pdb.Name) -Force
    }
}

Copy-Item -LiteralPath (Join-Path $distDir "Resources") -Destination $packageRootPath -Recurse -Force
Copy-Item -LiteralPath $payloadRoot -Destination $packageRootPath -Recurse -Force

Compress-Archive -LiteralPath $packageRootPath -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Package prepared successfully."
Write-Host "Payload folder: $packageRootPath"
Write-Host "Release zip   : $zipPath"
Write-Host "Revit payloads: 2020-2024 (.NET Framework), 2025 (.NET 8), 2026 (.NET 8), 2027 (.NET 10)"
if ($IncludeSymbols) {
    Write-Host "Symbols       : included (.pdb)"
} else {
    Write-Host "Symbols       : excluded (use -IncludeSymbols to include .pdb)"
}
Write-Host "Next step     : Upload the zip to public installer releases: https://github.com/Ajmalpshaik/AJ-Tools-Installer/releases"
