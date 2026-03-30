param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [switch]$Push
)

$ErrorActionPreference = "Stop"

if ($Version -match "^v") {
    $tagName = $Version
} else {
    $tagName = "v$Version"
}

if ($tagName -notmatch "^v\d+\.\d+\.\d+$") {
    throw "Invalid version format. Use X.Y.Z or vX.Y.Z (example: 1.2.0)."
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
