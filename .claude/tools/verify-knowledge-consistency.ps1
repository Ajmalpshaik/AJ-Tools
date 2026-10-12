<#
.SYNOPSIS
    Checks the AJ Tools knowledge system (.claude/skills, .claude/knowledge) for consistency
    drift - broken cross-references, missing frontmatter.

.DESCRIPTION
    This project runs on a "living document" convention: skills and knowledge files cross-link each
    other and get updated in place over time. Nothing enforces those links stay valid as files get
    renamed, moved, split, or retired - this script is that enforcement, run on demand (part of an
    ajtools-knowledge-sync pass, or whenever something feels stale).

    Checks performed:
      1. Every .claude/skills/*/SKILL.md has YAML frontmatter with both "name" and "description".
      2. Every markdown-style relative link [text](path) inside .claude/skills/**/*.md,
         .claude/knowledge/*.md, and the root CLAUDE.md resolves to a real file.
      3. Every skill name and .md file CLAUDE.md mentions in backticks exists.
      4. Every memory file has its pointer line in MEMORY.md, and every pointer resolves.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .claude\tools\verify-knowledge-consistency.ps1
#>

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$claudeDir = Join-Path $repoRoot ".claude"

$issues = New-Object System.Collections.Generic.List[string]

function Get-MarkdownLinkTargets {
    param([string]$Content)
    $matches = [regex]::Matches($Content, '\[[^\]]*\]\(([^)]+)\)')
    $targets = @()
    foreach ($m in $matches) {
        $target = $m.Groups[1].Value
        if ($target -match '^https?://' -or $target.StartsWith('#')) { continue }
        $targets += ($target -split '#')[0]
    }
    return $targets
}

Write-Host "=== 1. Skill frontmatter ===" -ForegroundColor Cyan
$skillFiles = Get-ChildItem -Path (Join-Path $claudeDir "skills") -Filter "SKILL.md" -Recurse -ErrorAction SilentlyContinue
foreach ($skill in $skillFiles) {
    $content = Get-Content -Path $skill.FullName -Raw
    if ($content -notmatch '(?s)^---\r?\n(.*?)\r?\n---') {
        $issues.Add("MISSING FRONTMATTER: " + $skill.FullName)
        continue
    }
    $frontmatter = $Matches[1]
    if ($frontmatter -notmatch '(?m)^name:\s*\S+') {
        $issues.Add("MISSING name in frontmatter: " + $skill.FullName)
    }
    if ($frontmatter -notmatch '(?m)^description:\s*\S+') {
        $issues.Add("MISSING description in frontmatter: " + $skill.FullName)
    }
}
Write-Host ("Checked {0} skill file(s)." -f $skillFiles.Count)

Write-Host "`n=== 2. Markdown link targets ===" -ForegroundColor Cyan
$mdFiles = @()
$mdFiles += Get-ChildItem -Path (Join-Path $claudeDir "skills") -Filter "*.md" -Recurse -ErrorAction SilentlyContinue
$mdFiles += Get-ChildItem -Path (Join-Path $claudeDir "knowledge") -Filter "*.md" -Recurse -ErrorAction SilentlyContinue
$rootClaudeMd = Join-Path $repoRoot "CLAUDE.md"
if (Test-Path $rootClaudeMd) { $mdFiles += Get-Item $rootClaudeMd }

$linkCount = 0
foreach ($md in $mdFiles) {
    $content = Get-Content -Path $md.FullName -Raw
    $targets = Get-MarkdownLinkTargets -Content $content
    foreach ($target in $targets) {
        $linkCount++
        $resolved = Join-Path (Split-Path -Parent $md.FullName) $target
        if (-not (Test-Path $resolved)) {
            $issues.Add("BROKEN LINK in " + $md.FullName + ": '" + $target + "' -> " + $resolved)
        }
    }
}
Write-Host ("Checked {0} link(s) across {1} markdown file(s)." -f $linkCount, $mdFiles.Count)

Write-Host "`n=== 3. CLAUDE.md backtick mentions (skills + knowledge files) ===" -ForegroundColor Cyan
# Added 2026-07-28: the 2026-07-22 Brain-split cleanup deleted reply-style.md and two skills that
# CLAUDE.md still referenced in backticks - invisible to check 2, which only sees [text](path) links.
# This check scans CLAUDE.md for `ajtools-...` skill tokens and `<name>.md` file tokens and verifies
# each exists on disk, so a delete/rename that orphans the working agreement gets caught immediately.
if (Test-Path $rootClaudeMd) {
    $claudeContent = Get-Content -Path $rootClaudeMd -Raw
    $backtickTokens = [regex]::Matches($claudeContent, '`([^`\r\n]+)`') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    $mentionCount = 0

    foreach ($token in $backtickTokens) {
        # Skill-name tokens: exactly "ajtools-<something>", no dot, no slash, no space
        if ($token -match '^ajtools-[a-z0-9-]+$') {
            $mentionCount++
            $skillPath = Join-Path $claudeDir ("skills\" + $token + "\SKILL.md")
            if (-not (Test-Path $skillPath)) {
                $issues.Add("CLAUDE.md MENTIONS MISSING SKILL: '" + $token + "' has no .claude/skills/" + $token + "/SKILL.md")
            }
            continue
        }
        # Markdown-file tokens: a bare or relative-path .md name (skip generic names that exist in many places)
        if ($token -match '^[A-Za-z0-9_./\\-]+\.md$') {
            $fileName = Split-Path $token -Leaf
            if ($fileName -in @("README.md", "SKILL.md", "CLAUDE.md", "MEMORY.md")) { continue }
            $mentionCount++
            $candidates = @(
                (Join-Path $repoRoot $token),
                (Join-Path $repoRoot $fileName),
                (Join-Path $claudeDir ("knowledge\" + $fileName)),
                (Join-Path $claudeDir ("knowledge\" + ($token -replace '/', '\')))
            )
            $found = $false
            foreach ($candidate in $candidates) {
                if (Test-Path $candidate) { $found = $true; break }
            }
            if (-not $found) {
                $issues.Add("CLAUDE.md MENTIONS MISSING FILE: '" + $token + "' not found at repo root or .claude/knowledge")
            }
        }
    }
    Write-Host ("Checked {0} backtick mention(s) in CLAUDE.md." -f $mentionCount)
} else {
    $issues.Add("MISSING FILE: root CLAUDE.md not found")
}

Write-Host "`n=== 4. Memory index vs memory folder ===" -ForegroundColor Cyan
# Added 2026-07-28: a memory file that never gets its one-line pointer in MEMORY.md is never recalled
# by any future session - it silently doesn't exist. Machine-specific path, so skip (not fail) when absent.
$memoryDir = "C:\Users\ajmal\.claude\projects\D--06-Development-01-AJ-Tools-Revit-Addins\memory"
if (Test-Path $memoryDir) {
    $memoryIndexPath = Join-Path $memoryDir "MEMORY.md"
    if (Test-Path $memoryIndexPath) {
        $memoryIndexContent = Get-Content -Path $memoryIndexPath -Raw
        $memoryFiles = Get-ChildItem -Path $memoryDir -Filter "*.md" -File | Where-Object { $_.Name -ne "MEMORY.md" }
        foreach ($memFile in $memoryFiles) {
            if ($memoryIndexContent -notmatch [regex]::Escape($memFile.Name)) {
                $issues.Add("MEMORY NOT IN INDEX: " + $memFile.Name + " exists but has no pointer line in MEMORY.md (it will never be recalled)")
            }
        }
        $indexRefs = [regex]::Matches($memoryIndexContent, '\]\(([^)]+\.md)\)') | ForEach-Object { $_.Groups[1].Value }
        foreach ($ref in $indexRefs) {
            if (-not (Test-Path (Join-Path $memoryDir $ref))) {
                $issues.Add("INDEX REFERENCES MISSING MEMORY: '" + $ref + "' listed in MEMORY.md but not found on disk")
            }
        }
        Write-Host ("Checked {0} memory file(s) against MEMORY.md." -f $memoryFiles.Count)
    } else {
        $issues.Add("MISSING FILE: MEMORY.md not found in " + $memoryDir)
    }
} else {
    Write-Host "Memory folder not present on this machine - skipped."
}

Write-Host "`n=== Result ===" -ForegroundColor Cyan
if ($issues.Count -eq 0) {
    Write-Host "All checks passed - no drift found." -ForegroundColor Green
    exit 0
} else {
    Write-Host ("{0} issue(s) found:" -f $issues.Count) -ForegroundColor Yellow
    foreach ($issue in $issues) { Write-Host ("  - " + $issue) -ForegroundColor Yellow }
    exit 1
}
