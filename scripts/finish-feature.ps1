<#
.SYNOPSIS
    Runs preflight checks and creates or updates a GitHub PR for the current feature branch.

.DESCRIPTION
    Preflights (all must pass before a PR is touched):
      1. Clean working tree  – no uncommitted changes
      2. Commits ahead of main – branch must have at least one commit not in main
      3. Rebase from main   – branch is up-to-date with origin/main
      4. Build              – zero warnings, zero errors (-warnaserror)
      5. Tests              – all xUnit tests pass

    First run (no PR yet):
      Creates a new PR with an AI-generated full description.
      Posts an initial marker comment containing the current HEAD SHA so future
      runs can detect exactly which commits are "new".

    Re-run (PR already exists):
      Does NOT overwrite the PR body.
      Instead, posts a structured update comment with:
        - Preflight results (build/test status)
        - List of commits added since the last finish-feature run
        - AI-generated summary of those new commits
        - Hidden machine-readable markers (<!-- finish-feature-update -->,
          <!-- head-sha: SHA -->) for use by a future pr-review script.
      If no new commits exist since the last update comment, exits cleanly
      without posting a duplicate.

    Future pr-review script contract (not implemented here):
      Detects new PR creation or a new <!-- finish-feature-update --> comment.
      Runs AI code-review on the diff since the SHA marker.
      Posts findings as <!-- pr-review-findings --> comment for developer action.

.PARAMETER Title
    Override the PR title. When omitted the script asks the AI to generate a
    conventional-commit style title (e.g. "feat(tests): add Core service coverage").
    Falls back to a sanitised branch name if AI generation fails.

.PARAMETER Provider
    AI provider to use for PR description generation. Defaults to Copilot.

.PARAMETER Draft
    Open the PR as a draft (ignored when updating an existing PR).

.PARAMETER SkipRebase
    Skip the rebase-from-main preflight step.

.PARAMETER SkipTests
    Skip the test-suite preflight step.
#>
[CmdletBinding()]
param (
    [string]$Title,
    [Parameter()]
    [ValidateSet("Gemini", "Copilot")]
    [string]$Provider = "Copilot",
    [switch]$Draft,
    [switch]$SkipRebase,
    [switch]$SkipTests
)

# ── Encoding setup ────────────────────────────────────────────────────────────
# chcp 65001 tells child processes (Gemini CLI, gh copilot …) to emit UTF-8.
# $OutputEncoding  = bytes PowerShell writes to child process stdin.
# [Console]::OutputEncoding = bytes PowerShell reads from child process stdout.
# All three must agree or multi-byte Unicode (emoji, etc.) gets garbled.
chcp 65001 | Out-Null
$OutputEncoding              = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding   = [System.Text.Encoding]::UTF8
[Console]::InputEncoding    = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# ── Helpers ───────────────────────────────────────────────────────────────────
function Write-Step([string]$msg) { Write-Host "`n▶ $msg" -ForegroundColor Cyan }
function Write-Pass([string]$msg) { Write-Host "  ✅ $msg" -ForegroundColor Green }
function Write-Fail([string]$msg) { Write-Host "  ❌ $msg" -ForegroundColor Red }

# ── 0. Tool prerequisites ─────────────────────────────────────────────────────
Write-Step "Checking required tools..."
if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Fail "GitHub CLI ('gh') is not installed or not in PATH."
    exit 1
}
Write-Pass "gh CLI found"

# ── 1. Clean working tree ─────────────────────────────────────────────────────
Write-Step "Checking for uncommitted changes..."
$gitStatus = git status --porcelain
if (![string]::IsNullOrWhiteSpace($gitStatus)) {
    Write-Fail "Working tree is dirty. Commit or stash your changes before finishing a feature."
    Write-Host $gitStatus
    exit 1
}
Write-Pass "Working tree is clean"

# ── 2. Branch guard ───────────────────────────────────────────────────────────
$CurrentBranch = (git branch --show-current)
if ($CurrentBranch -eq "main" -or $CurrentBranch -eq "master") {
    Write-Fail "You are on '$CurrentBranch'. Switch to a feature or bugfix branch first."
    exit 1
}

Write-Step "Checking commits ahead of main..."
$earlyCommitCheck = git log main..HEAD --oneline
if ([string]::IsNullOrWhiteSpace($earlyCommitCheck)) {
    Write-Fail "Nothing to PR — branch has no commits ahead of main."
    exit 1
}
Write-Pass "Branch has commits ahead of main"

# ── 3. Rebase from main ───────────────────────────────────────────────────────
if ($SkipRebase) {
    Write-Host "`n⚠️  Rebase step skipped (-SkipRebase)" -ForegroundColor Yellow
} else {
    Write-Step "Fetching latest origin/main..."
    git fetch origin main 2>&1 | Out-Null

    $behind = git rev-list HEAD..origin/main --count
    if ([int]$behind -gt 0) {
        Write-Host "  Branch is $behind commit(s) behind origin/main. Rebasing..."
        $rebaseOutput = git rebase origin/main 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "Rebase failed. Resolve conflicts then re-run, or use 'git rebase --abort' to cancel."
            Write-Host ($rebaseOutput | Out-String)
            exit 1
        }
        Write-Pass "Rebased successfully"

        Write-Host "  Pushing rebased branch to origin..."
        git push --force-with-lease origin $CurrentBranch 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "Force-push after rebase failed. Check remote permissions or re-run with -SkipRebase."
            exit 1
        }
        Write-Pass "Branch pushed"
    } else {
        Write-Pass "Branch is already up-to-date with origin/main"
    }
}

# ── 4. Build (zero warnings, zero errors) ────────────────────────────────────
Write-Step "Building solution (-warnaserror)..."
$solutionFile = Get-ChildItem -Path (Split-Path $PSScriptRoot -Parent) -Filter "*.sln" -Recurse |
    Select-Object -First 1
if ($null -eq $solutionFile) {
    Write-Fail "No .sln file found in the repository root."
    exit 1
}

$buildOutput = dotnet build $solutionFile.FullName -warnaserror 2>&1
$buildSuccess = $LASTEXITCODE -eq 0
$buildSummary = $buildOutput | Select-String "(Warning\(s\)|Error\(s\)|Build succeeded|FAILED)" |
    ForEach-Object { $_.Line.Trim() }

if (-not $buildSuccess) {
    Write-Fail "Build failed (errors or warnings detected):"
    $buildOutput | Select-String "(warning|error)\s" | ForEach-Object { Write-Host "  $_" }
    Write-Host ($buildSummary | Out-String)
    exit 1
}
Write-Pass ($buildSummary -join " | ")

# ── 5. Tests ──────────────────────────────────────────────────────────────────
$testResultLine = "⏭️ skipped (-SkipTests)"
if ($SkipTests) {
    Write-Host "`n⚠️  Test step skipped (-SkipTests)" -ForegroundColor Yellow
} else {
    Write-Step "Running tests..."
    $testOutput = dotnet test $solutionFile.FullName --no-build --logger "console;verbosity=minimal" 2>&1
    $testSuccess = $LASTEXITCODE -eq 0
    $testSummary = $testOutput | Select-String "(passed|failed|skipped|Test Run)" |
        ForEach-Object { $_.Line.Trim() } | Select-Object -Last 5

    if (-not $testSuccess) {
        Write-Fail "Tests failed:"
        $testOutput | Select-String "Failed" | ForEach-Object { Write-Host "  $_" }
        Write-Host ($testSummary | Out-String)
        exit 1
    }
    $testResultLine = "✅ " + ($testSummary | Out-String).Trim()
    Write-Pass ($testSummary | Out-String).Trim()
}

# ── 6. Check for existing PR ──────────────────────────────────────────────────
Write-Step "Checking for existing PR on branch: $CurrentBranch..."
$ExistingPRJson = gh pr list --head $CurrentBranch --json number,state --jq ".[0]"
$ExistingPR = if (![string]::IsNullOrWhiteSpace($ExistingPRJson) -and $ExistingPRJson -ne "null") {
    $ExistingPRJson | ConvertFrom-Json
} else { $null }
$IsUpdate = $null -ne $ExistingPR -and ![string]::IsNullOrWhiteSpace($ExistingPR.number)

if ($IsUpdate) {
    Write-Pass "Found existing PR #$($ExistingPR.number) ($($ExistingPR.state)) — will post update comment"
} else {
    Write-Pass "No existing PR — will create new"
}

# ── 7. Gather git context for AI ─────────────────────────────────────────────
# Returns a hashtable: FullLog, DiffStat, Diff (capped), ShortLog
function Get-GitContext {
    param([string]$fromRef, [string]$toRef = "HEAD")

    $fullLog  = git log "${fromRef}..${toRef}" --format="commit %H%nauthor: %an <%ae>%ndate: %ai%n%nSubject: %s%n%n%b%n---" 2>&1
    $diffStat = git diff "${fromRef}..${toRef}" --stat 2>&1
    $fullDiff = git diff "${fromRef}..${toRef}" 2>&1
    $shortLog = git log "${fromRef}..${toRef}" --oneline 2>&1

    # Cap diff at 6000 chars to avoid AI token limits
    $maxDiffChars = 6000
    $diffText = ($fullDiff | Out-String)
    $diffTruncated = $false
    if ($diffText.Length -gt $maxDiffChars) {
        $diffText = $diffText.Substring(0, $maxDiffChars)
        $diffTruncated = $true
    }

    return @{
        FullLog       = ($fullLog  | Out-String).Trim()
        DiffStat      = ($diffStat | Out-String).Trim()
        Diff          = $diffText.Trim()
        DiffTruncated = $diffTruncated
        ShortLog      = ($shortLog | Out-String).Trim()
    }
}

# ── 8. AI text generation ─────────────────────────────────────────────────────
function Invoke-AI {
    param([string]$prompt, [string]$preferred)

    $tools = if ($preferred -eq "Copilot") { @("Copilot", "Gemini") } else { @("Gemini", "Copilot") }
    $rawResult = ""

    foreach ($tool in $tools) {
        Write-Host "  Checking $tool..."

        if ($tool -eq "Gemini") {
            if (Get-Command gemini -ErrorAction SilentlyContinue) {
                try {
                    Write-Host "  Generating using Gemini..."
                    $rawResult = ($prompt | gemini ask) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch { Write-Warning "Gemini failed." }
            }
        }

        if ($tool -eq "Copilot") {
            $hasGhCopilot = (gh extension list | Select-String "copilot")
            $hasStandaloneCopilot = (Get-Command copilot -ErrorAction SilentlyContinue)

            if ($hasGhCopilot) {
                try {
                    Write-Host "  Generating using GitHub Copilot extension..."
                    $rawResult = ($prompt | gh copilot explain --file -) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch { Write-Warning "GitHub Copilot extension failed." }
            } elseif ($hasStandaloneCopilot) {
                try {
                    Write-Host "  Generating using standalone Copilot CLI..."
                    $rawResult = (copilot --prompt $prompt) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch { Write-Warning "Standalone Copilot CLI failed." }
            }
        }
    }

    return $rawResult
}

function Format-AIOutput([string]$raw, [string]$fallback) {
    if ([string]::IsNullOrWhiteSpace($raw)) {
        Write-Warning "All AI providers failed. Using basic commit list."
        return "## Changes`n`n" + ($fallback -split "`n" | ForEach-Object { "- $_" } | Out-String)
    }

    $cleaned = $raw
    $cleaned = $cleaned -replace "\r\n", "`n"
    $cleaned = $cleaned -replace "\r", "`n"
    $cleaned = $cleaned -replace "(?m)^[\u25CF\u2514\u251C\u2500\u2502\u252C\u2510\u250C\u2518\u2524\u253C]+.*$", ""
    $cleaned = $cleaned -replace "(?m)^---\s*$", ""
    $cleaned = $cleaned -replace "(?m)^Suggested PR description:\s*$", ""
    $cleaned = $cleaned -replace "(?sm)^.*?(?=^#{1,6}\s|^Overview\b|^Summary\b|^Changes\b)", ""
    $cleaned = $cleaned -replace "(?m)(\n\s*){3,}", "`n`n"
    return $cleaned.Trim()
}

# Generates a conventional-commit PR title via AI; falls back to branch-name heuristic.
function Get-PRTitle {
    param([string]$branch, [string]$shortLog, [string]$diffStat, [string]$preferred)

    $titlePrompt = @"
You are a developer writing a GitHub PR title.
Based on the commits and changed files below, write ONE single-line PR title in conventional commit format.
Format: type(scope): short description
- type: feat | fix | refactor | chore | docs | test | ci
- scope: short area name (e.g. tests, scripts, ui, settings) — optional but preferred
- description: lowercase, imperative, ≤ 55 chars after the prefix
Rules: Return ONLY the title line. No markdown, no quotes, no explanation, no newline.

Commits:
$shortLog

Files changed:
$diffStat
"@

    $raw = (Invoke-AI -prompt $titlePrompt -preferred $preferred)
    if (![string]::IsNullOrWhiteSpace($raw)) {
        # Take only the first non-empty line and strip any markdown/quotes
        $line = ($raw -split "`n" | Where-Object { $_.Trim() -ne "" } | Select-Object -First 1).Trim()
        $line = $line -replace '^[`"''*]+|[`"''*]+$', ''  # strip surrounding markdown chars
        if ($line.Length -le 72 -and $line -match '^(feat|fix|refactor|chore|docs|test|ci)') {
            return $line
        }
    }

    # Fallback: derive from branch name
    $stripped = $branch -replace '^(feature|feat|bugfix|hotfix|fix|chore|docs|test|release)[/\-]', ''
    $stripped = $stripped -replace '[/\-]', ' '
    $stripped = $stripped.Trim()
    $prefix   = switch -Regex ($branch) {
        '^(feature|feat)/'  { 'feat' }
        '^(bugfix|hotfix|fix)/' { 'fix' }
        '^chore/'           { 'chore' }
        '^docs/'            { 'docs' }
        '^test/'            { 'test' }
        default             { 'chore' }
    }
    return "$prefix`: $stripped"
}

# ── 9. Write body/comment to a temp file and submit ──────────────────────────
function Submit-WithTempFile {
    param([string]$content, [scriptblock]$action)
    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        [System.IO.File]::WriteAllText($tempFile, $content, [System.Text.UTF8Encoding]::new($false))
        & $action $tempFile
    } finally {
        Remove-Item $tempFile -ErrorAction SilentlyContinue
    }
}

$HeadSHA = (git rev-parse HEAD).Trim()
$Timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:sszzz")
$BuildResultLine = "✅ " + ($buildSummary -join " | ")

if ($IsUpdate) {
    # ── Update path: post a new comment ──────────────────────────────────────

    Write-Step "Reading existing PR comments to detect last update SHA..."
    $commentsJson = gh pr view $ExistingPR.number --json comments --jq ".comments[].body" 2>$null
    $lastSHA = $null
    if (![string]::IsNullOrWhiteSpace($commentsJson)) {
        $allBodies = $commentsJson -split "(?=^)" | Where-Object { $_ -match "finish-feature-update" }
        if ($allBodies) {
            $lastComment = ($allBodies | Select-Object -Last 1)
            if ($lastComment -match "<!--\s*head-sha:\s*([0-9a-f]{7,40})\s*-->") {
                $lastSHA = $Matches[1]
            }
        }
    }

    # Determine "new" commits since last update (or all vs main if first update)
    $fromRef = if ($lastSHA) { $lastSHA } else { "main" }
    if ($lastSHA) {
        Write-Host "  Last update SHA: $lastSHA"
    } else {
        Write-Host "  No previous update comment found — using all commits vs main"
    }

    $ctx = Get-GitContext -fromRef $fromRef
    if ([string]::IsNullOrWhiteSpace($ctx.ShortLog)) {
        Write-Pass "No new commits since last update comment — nothing to post."
        exit 0
    }

    Write-Host "  New commits:`n$($ctx.ShortLog)"

    $truncNote = if ($ctx.DiffTruncated) { "`n> ⚠️ Diff truncated at 6000 chars." } else { "" }
    Write-Step "Generating AI summary for new commits..."
    $updatePrompt = @"
You are an expert developer reviewing a pull request update for KubeTools4Dev,
a cross-platform desktop app that simplifies Kubernetes development tasks (Avalonia UI, .NET 10).

Write a concise update summary in Markdown describing what changed in these new commits.
Return ONLY the Markdown — no preamble, no '---' separators, no CLI artefacts.
Be specific: mention actual file names, class names, method names, or test names you can see.

## New Commits
$($ctx.ShortLog)

## Full Commit Messages
$($ctx.FullLog)

## Files Changed
$($ctx.DiffStat)

## Diff$truncNote
$($ctx.Diff)
"@
    $rawAI = Invoke-AI -prompt $updatePrompt -preferred $Provider
    $aiSummary = Format-AIOutput -raw $rawAI -fallback $ctx.ShortLog

    $commitLines = ($ctx.ShortLog -split "`n" | Where-Object { $_ } | ForEach-Object { "- ``$_``" }) -join "`n"
    $commentBody = @"
## 🔄 Update — $Timestamp

### Preflight Results
- 🏗️ Build: $BuildResultLine
- 🧪 Tests: $testResultLine

### New Commits
$commitLines

### Summary
$aiSummary

<!-- finish-feature-update -->
<!-- head-sha: $HeadSHA -->
"@

    Write-Step "Posting update comment on PR #$($ExistingPR.number)..."
    Submit-WithTempFile -content $commentBody -action {
        param($f)
        gh pr comment $ExistingPR.number --body-file $f
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Pass "Update comment posted on PR #$($ExistingPR.number)"
    } else {
        Write-Fail "Failed to post PR comment."
        exit 1
    }

} else {
    # ── Create path: new PR with full AI description ──────────────────────────

    Write-Step "Gathering git context (commits, diff stat, diff)..."
    $ctx = Get-GitContext -fromRef "main"
    Write-Host "  $($ctx.ShortLog.Split("`n").Count) commit(s), diff stat:`n$($ctx.DiffStat)"

    # Generate title (skip if -Title was provided)
    $PRTitle = $Title
    if ([string]::IsNullOrWhiteSpace($PRTitle)) {
        Write-Step "Generating PR title..."
        $PRTitle = Get-PRTitle -branch $CurrentBranch -shortLog $ctx.ShortLog `
                               -diffStat $ctx.DiffStat -preferred $Provider
        Write-Pass "Title: $PRTitle"
    }

    $truncNote = if ($ctx.DiffTruncated) { "`n> ⚠️ Diff truncated at 6000 chars." } else { "" }
    Write-Step "Generating PR description..."
    $createPrompt = @"
You are an expert developer writing a GitHub Pull Request description for KubeTools4Dev,
a cross-platform desktop app that simplifies Kubernetes development tasks (Avalonia UI, .NET 10).

Generate a detailed, specific PR description in Markdown using EXACTLY these sections:

## Overview
2-3 sentences summarising the overall purpose and impact of this PR.

## What's Changed
Group changes by area (e.g. Testing, Scripts, UI, Core, Config). Use bullet points.
Be specific — mention actual file names, class names, method/function names, and test names you can see in the diff.

## Files Changed
A table with columns: File | Change | Description
Change = New / Modified / Deleted

## Testing
Describe how to verify the changes (commands to run, what to check, etc.)

Rules:
- Return ONLY the Markdown content — no preamble, no conversational text, no '---' separators
- Only describe what you can actually see in the diff — do NOT invent or guess content
- Leave no section empty

## Commits
$($ctx.ShortLog)

## Full Commit Messages
$($ctx.FullLog)

## Files Changed (stat)
$($ctx.DiffStat)

## Diff$truncNote
$($ctx.Diff)
"@
    $rawAI = Invoke-AI -prompt $createPrompt -preferred $Provider
    $PRBody = Format-AIOutput -raw $rawAI -fallback $ctx.ShortLog

    Write-Step "Creating Pull Request..."
    Submit-WithTempFile -content $PRBody -action {
        param($f)
        $ghArgs = @("pr", "create", "--title", $PRTitle, "--body-file", $f)
        if ($Draft) { $ghArgs += "--draft" }
        & gh $ghArgs
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Failed to create PR via GitHub CLI."
        exit 1
    }
    Write-Pass "Pull Request created successfully!"

    # Post initial marker comment so the first re-run can diff correctly
    Write-Step "Posting initial marker comment..."
    $markerBody = "<!-- finish-feature-update -->`n<!-- head-sha: $HeadSHA -->"
    $NewPRJson = gh pr list --head $CurrentBranch --json number --jq ".[0].number"
    if (![string]::IsNullOrWhiteSpace($NewPRJson) -and $NewPRJson -ne "null") {
        Submit-WithTempFile -content $markerBody -action {
            param($f)
            gh pr comment $NewPRJson --body-file $f
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Pass "Marker comment posted (HEAD: $HeadSHA)"
        } else {
            Write-Host "  ⚠️  Could not post marker comment — re-runs will fall back to all commits vs main." -ForegroundColor Yellow
        }
    }
}
