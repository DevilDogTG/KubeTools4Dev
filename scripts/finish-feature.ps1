<#
.SYNOPSIS
    Runs preflight checks and creates or updates a GitHub PR for the current feature branch.

.DESCRIPTION
    Preflights (all must pass before a PR is touched):
      1. Clean working tree  – no uncommitted changes
      2. Rebase from main   – branch is up-to-date with origin/main
      3. Build              – zero warnings, zero errors (-warnaserror)
      4. Tests              – all xUnit tests pass

    After all preflights pass the script generates an AI-assisted PR description
    and creates (or updates) the GitHub PR.

.PARAMETER Title
    Override the PR title. Defaults to the current branch name.

.PARAMETER Provider
    AI provider to use for PR description generation. Defaults to Gemini.

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
    [string]$Provider = "Gemini",
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

# ── Helper ────────────────────────────────────────────────────────────────────
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

# ── 2. Rebase from main ───────────────────────────────────────────────────────
$CurrentBranch = (git branch --show-current)
if ($CurrentBranch -eq "main" -or $CurrentBranch -eq "master") {
    Write-Fail "You are on '$CurrentBranch'. Switch to a feature or bugfix branch first."
    exit 1
}

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

# ── 3. Build (zero warnings, zero errors) ────────────────────────────────────
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

# ── 4. Tests ──────────────────────────────────────────────────────────────────
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
    Write-Pass ($testSummary | Out-String).Trim()
}

# ── 5. Check for existing PR ──────────────────────────────────────────────────
Write-Step "Checking for existing PR on branch: $CurrentBranch..."
$ExistingPRJson = gh pr list --head $CurrentBranch --json number,state --jq ".[0]"
$ExistingPR = if (![string]::IsNullOrWhiteSpace($ExistingPRJson) -and $ExistingPRJson -ne "null") {
    $ExistingPRJson | ConvertFrom-Json
} else { $null }
$IsUpdate = $null -ne $ExistingPR -and ![string]::IsNullOrWhiteSpace($ExistingPR.number)

if ($IsUpdate) {
    Write-Pass "Found existing PR #$($ExistingPR.number) ($($ExistingPR.state)) — will update"
} else {
    Write-Pass "No existing PR — will create new"
}

# ── 6. Extract commits ────────────────────────────────────────────────────────
Write-Step "Extracting commits against main..."
$Commits = git log main..HEAD --oneline
if ([string]::IsNullOrWhiteSpace($Commits)) {
    Write-Fail "No commits found on this branch that are not in 'main'."
    exit 1
}
Write-Host ($Commits | Out-String)

# ── 7. Generate PR body via AI ────────────────────────────────────────────────
function Get-PRBody {
    param([string]$commits, [string]$preferred)

    $prompt = @"
You are an expert developer assistant. Generate a clean, professional GitHub Pull Request description in Markdown format based on the following commit messages.
IMPORTANT: Return ONLY the Markdown content. Do not include any conversational preamble (like 'Here is a suggested...'), do not include CLI progress indicators, and do not include the '---' separators at the start/end.

Commits:
$commits
"@

    $tools = if ($preferred -eq "Copilot") { @("Copilot", "Gemini") } else { @("Gemini", "Copilot") }
    $rawResult = ""

    foreach ($tool in $tools) {
        Write-Host "  Checking $tool..."

        if ($tool -eq "Gemini") {
            if (Get-Command gemini -ErrorAction SilentlyContinue) {
                try {
                    Write-Host "  Generating description using Gemini..."
                    $rawResult = ($prompt | gemini ask) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch {
                    Write-Warning "Gemini failed."
                }
            }
        }

        if ($tool -eq "Copilot") {
            $hasGhCopilot = (gh extension list | Select-String "copilot")
            $hasStandaloneCopilot = (Get-Command copilot -ErrorAction SilentlyContinue)

            if ($hasGhCopilot) {
                try {
                    Write-Host "  Generating description using GitHub Copilot extension..."
                    $rawResult = ($prompt | gh copilot explain --file -) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch {
                    Write-Warning "GitHub Copilot extension failed."
                }
            } elseif ($hasStandaloneCopilot) {
                try {
                    Write-Host "  Generating description using standalone Copilot CLI..."
                    $rawResult = (copilot --prompt $prompt) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch {
                    Write-Warning "Standalone Copilot CLI failed."
                }
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($rawResult)) {
        Write-Warning "All AI providers failed. Using basic commit list."
        return "## Changes`n`n" + ($commits -split "`n" | ForEach-Object { "- $_" } | Out-String)
    }

    Write-Host "  Cleaning up AI output..."
    $cleaned = $rawResult
    $cleaned = $cleaned -replace "\r\n", "`n"
    $cleaned = $cleaned -replace "\r", "`n"
    # Strip Copilot CLI agentic tool-use lines (box-drawing / bullet characters)
    $cleaned = $cleaned -replace "(?m)^[\u25CF\u2514\u251C\u2500\u2502\u252C\u2510\u250C\u2518\u2524\u253C]+.*$", ""
    # Remove common CLI artefacts
    $cleaned = $cleaned -replace "(?m)^---\s*$", ""
    $cleaned = $cleaned -replace "(?m)^Suggested PR description:\s*$", ""
    # Drop everything before the first Markdown heading
    $cleaned = $cleaned -replace "(?sm)^.*?(?=^#{1,6}\s|^Summary\b|^Changes\b)", ""
    # Collapse 3+ consecutive blank lines
    $cleaned = $cleaned -replace "(?m)(\n\s*){3,}", "`n`n"

    return $cleaned.Trim()
}

Write-Step "Generating PR description..."
$PRBody = Get-PRBody -commits $Commits -preferred $Provider

# ── 8. Create or update PR ────────────────────────────────────────────────────
$PRTitle = if ($Title) { $Title } else { $CurrentBranch }

Write-Step "$(if ($IsUpdate) { 'Updating' } else { 'Creating' }) Pull Request..."
$tempBodyFile = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($tempBodyFile, $PRBody, [System.Text.UTF8Encoding]::new($false))

    if ($IsUpdate) {
        gh pr edit $ExistingPR.number --title $PRTitle --body-file $tempBodyFile
    } else {
        $ghArgs = @("pr", "create", "--title", $PRTitle, "--body-file", $tempBodyFile)
        if ($Draft) { $ghArgs += "--draft" }
        & gh $ghArgs
    }
} finally {
    Remove-Item $tempBodyFile -ErrorAction SilentlyContinue
}

if ($LASTEXITCODE -eq 0) {
    $action = if ($IsUpdate) { "updated" } else { "created" }
    Write-Pass "Pull Request $action successfully!"
} else {
    Write-Fail "Failed to perform PR action via GitHub CLI."
    exit 1
}
