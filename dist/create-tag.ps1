param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [switch]$Push
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Split-Path -Parent $scriptDir

if ($Version -match "^v") {
    $tagName = $Version
} else {
    $tagName = "v$Version"
}

if ($tagName -notmatch "^v\d+\.\d+\.\d+$") {
    throw "Invalid version format. Use X.Y.Z or vX.Y.Z (example: 1.2.0)."
}

$requestedVersion = $tagName.Substring(1)
$assemblyInfoPath = Join-Path $repoRoot "src\Properties\AssemblyInfo.cs"
if (-not (Test-Path -LiteralPath $assemblyInfoPath)) {
    throw "AssemblyInfo.cs not found at '$assemblyInfoPath'."
}

$versionLine = Select-String -Path $assemblyInfoPath -Pattern 'AssemblyVersion\("([^"]+)"\)' | Select-Object -First 1
if (-not $versionLine -or $versionLine.Matches.Count -eq 0) {
    throw "Unable to read AssemblyVersion from '$assemblyInfoPath'."
}

$assemblyVersion = $versionLine.Matches[0].Groups[1].Value
$assemblySegments = $assemblyVersion.Split(".")
if ($assemblySegments.Count -lt 3) {
    throw "AssemblyVersion '$assemblyVersion' is not in expected X.Y.Z.W format."
}

$assemblySemanticVersion = "{0}.{1}.{2}" -f $assemblySegments[0], $assemblySegments[1], $assemblySegments[2]
if ($assemblySemanticVersion -ne $requestedVersion) {
    throw "Requested tag '$tagName' does not match AssemblyVersion '$assemblySemanticVersion'. Update src\\Properties\\AssemblyInfo.cs first."
}

$existingLocal = git tag --list $tagName
if ($existingLocal) {
    throw "Tag '$tagName' already exists locally."
}

$existingRemote = git ls-remote --tags origin "refs/tags/$tagName" "refs/tags/$tagName^{}"
if ($existingRemote) {
    throw "Tag '$tagName' already exists on origin."
}

git tag -a $tagName -m "AJ Tools $tagName"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create git tag '$tagName'."
}

Write-Host "Created tag: $tagName"

if ($Push) {
    git push origin $tagName
    if ($LASTEXITCODE -ne 0) {
        throw "Tag created locally but push failed for '$tagName'."
    }
    Write-Host "Pushed tag to origin: $tagName"
}
