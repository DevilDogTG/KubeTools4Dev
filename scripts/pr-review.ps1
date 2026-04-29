<#
.SYNOPSIS
    Runs an AI code review on the current branch's open or draft Pull Request
    and posts the findings as a structured comment.

.DESCRIPTION
    Preflight checks (all must pass):
      1. gh CLI is present
      2. Not on main/master
      3. An open or draft PR exists for the current branch

    Review scope detection:
      - Reads the latest <!-- finish-feature-update --> comment to find the base
        SHA (<!-- head-sha: SHA -->) for the diff.  Falls back to main if no
        marker is found.
      - Reads the latest <!-- pr-review-findings --> comment to find the SHA
        and status of the most recent review pass.

    Skip conditions (bypassed with -Force):
      - Already approved at the current HEAD SHA.
      - Previous review found issues (needs-work) but no developer reply has
        been posted since that review comment.

    Review output:
      - 🔴 Critical / 🟡 Warning / 🔵 Info findings from the AI.
      - 🔴 or 🟡 findings → posts a "needs-work" comment.
      - Only 🔵 or no findings → posts an "approved" comment.

    Comment markers (machine-readable, invisible on GitHub):
      <!-- pr-review-findings -->       — marks this as a review comment
      <!-- review-status: approved -->  — or: needs-work
      <!-- review-sha: SHA -->          — HEAD SHA at time of review

    Re-review:
      Re-run the script after pushing new commits or after replying to a
      needs-work comment.  The script detects both cases automatically.

.PARAMETER Provider
    AI provider to use.  Defaults to Copilot.  Falls back to the other
    provider automatically if the preferred one is unavailable.

.PARAMETER Force
    Force a new review even if the PR is already approved or is still
    awaiting a developer reply.

.EXAMPLE
    # Review the current branch's PR using GitHub Copilot
    .\scripts\pr-review.ps1

.EXAMPLE
    # Re-review even if already approved
    .\scripts\pr-review.ps1 -Force

.EXAMPLE
    # Use Gemini as the preferred AI provider
    .\scripts\pr-review.ps1 -Provider Gemini
#>
[CmdletBinding()]
param (
    [Parameter()]
    [ValidateSet("Gemini", "Copilot")]
    [string]$Provider = "Copilot",

    [switch]$Force
)

# ── Encoding setup ────────────────────────────────────────────────────────────
chcp 65001 | Out-Null
$OutputEncoding             = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding  = [System.Text.Encoding]::UTF8
[Console]::InputEncoding   = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# ── Helpers ───────────────────────────────────────────────────────────────────
function Write-Step([string]$msg) { Write-Host "`n▶ $msg" -ForegroundColor Cyan }
function Write-Pass([string]$msg) { Write-Host "  ✅ $msg" -ForegroundColor Green }
function Write-Fail([string]$msg) { Write-Host "  ❌ $msg" -ForegroundColor Red }
function Write-Warn([string]$msg) { Write-Host "  ⚠️  $msg" -ForegroundColor Yellow }
function Write-Info([string]$msg) { Write-Host "  ℹ️  $msg" -ForegroundColor DarkCyan }

# ── AI invocation ─────────────────────────────────────────────────────────────
function Invoke-AI {
    param([string]$prompt, [string]$preferred)

    $tools = if ($preferred -eq "Copilot") { @("Copilot", "Gemini") } else { @("Gemini", "Copilot") }
    $rawResult = ""

    foreach ($tool in $tools) {
        Write-Host "  Checking $tool..." -ForegroundColor DarkGray

        if ($tool -eq "Gemini") {
            if (Get-Command gemini -ErrorAction SilentlyContinue) {
                try {
                    Write-Host "  Generating using Gemini..." -ForegroundColor DarkGray
                    $rawResult = ($prompt | gemini ask) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch { Write-Warning "Gemini failed." }
            }
        }

        if ($tool -eq "Copilot") {
            $hasGhCopilot         = (gh extension list | Select-String "copilot")
            $hasStandaloneCopilot = (Get-Command copilot -ErrorAction SilentlyContinue)
            if ($hasGhCopilot) {
                try {
                    Write-Host "  Generating using GitHub Copilot extension..." -ForegroundColor DarkGray
                    $rawResult = ($prompt | gh copilot explain --file -) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch { Write-Warning "GitHub Copilot extension failed." }
            } elseif ($hasStandaloneCopilot) {
                # Write prompt to a temp file to avoid command-line length limits
                $promptFile = [System.IO.Path]::GetTempFileName()
                try {
                    [System.IO.File]::WriteAllText($promptFile, $prompt, [System.Text.UTF8Encoding]::new($false))
                    Write-Host "  Generating using standalone Copilot CLI..." -ForegroundColor DarkGray
                    $rawResult = (copilot explain --file $promptFile) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch { Write-Warning "Standalone Copilot CLI failed." }
                finally { Remove-Item $promptFile -ErrorAction SilentlyContinue }
            }
        }
    }

    return $rawResult
}

function Format-AIOutput([string]$raw, [string]$fallback) {
    if ([string]::IsNullOrWhiteSpace($raw)) {
        Write-Warning "All AI providers failed. Using basic fallback."
        return $fallback
    }
    $cleaned = $raw
    $cleaned = $cleaned -replace "\r\n", "`n"
    $cleaned = $cleaned -replace "\r", "`n"
    $cleaned = $cleaned -replace "(?m)^[\u25CF\u2514\u251C\u2500\u2502\u252C\u2510\u250C\u2518\u2524\u253C]+.*$", ""
    $cleaned = $cleaned -replace "(?m)^---\s*$", ""
    $cleaned = $cleaned -replace "(?m)(\n\s*){3,}", "`n`n"
    return $cleaned.Trim()
}

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

# ── 0. Tool prerequisites ─────────────────────────────────────────────────────
Write-Step "Checking required tools..."
if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Fail "GitHub CLI ('gh') is not installed or not in PATH."
    exit 1
}
Write-Pass "gh CLI found"

# ── 1. Branch guard ───────────────────────────────────────────────────────────
$CurrentBranch = (git branch --show-current).Trim()
if ($CurrentBranch -eq "main" -or $CurrentBranch -eq "master") {
    Write-Fail "You are on '$CurrentBranch'. Switch to a feature or bugfix branch first."
    exit 1
}
Write-Step "Current branch: $CurrentBranch"
Write-Pass "Branch guard passed"

# ── 2. Find open or draft PR ──────────────────────────────────────────────────
Write-Step "Looking for open or draft PR on '$CurrentBranch'..."
$prJson = gh pr list --head $CurrentBranch --state open --json number,title,isDraft,url,headRefOid 2>$null
if ([string]::IsNullOrWhiteSpace($prJson) -or $prJson -eq "[]") {
    Write-Fail "No open or draft PR found for branch '$CurrentBranch'."
    Write-Info "Run 'scripts/finish-feature.ps1' first to create a PR."
    exit 1
}

$prs = $prJson | ConvertFrom-Json
if ($prs.Count -eq 0) {
    Write-Fail "No open or draft PR found for branch '$CurrentBranch'."
    exit 1
}

$PR = $prs[0]
$draftLabel = if ($PR.isDraft) { " [DRAFT]" } else { "" }
Write-Pass "Found PR #$($PR.number)$draftLabel — $($PR.title)"
Write-Info "URL: $($PR.url)"

$HeadSHA = (git rev-parse HEAD).Trim()
$HeadSHAShort = $HeadSHA.Substring(0, 7)

# ── 3. Read PR comments; extract state markers ────────────────────────────────
Write-Step "Reading PR comments to detect review state..."

$allCommentBodies = @()
try {
    $rawComments = gh pr view $PR.number --json comments --jq ".comments[].body" 2>$null
    if (![string]::IsNullOrWhiteSpace($rawComments)) {
        $allCommentBodies = $rawComments -split "(?=\n)" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    }
} catch {
    Write-Warn "Could not read PR comments — treating as first review."
}

# Extract base SHA from the latest finish-feature-update marker
$baseSHA = $null
$ffUpdateComments = $allCommentBodies | Where-Object { $_ -match "finish-feature-update" }
if ($ffUpdateComments) {
    $lastFF = ($ffUpdateComments | Select-Object -Last 1)
    if ($lastFF -match "<!--\s*head-sha:\s*([0-9a-f]{7,40})\s*-->") {
        $baseSHA = $Matches[1]
        Write-Info "Base SHA from finish-feature-update: $baseSHA"
    }
}
if (!$baseSHA) {
    Write-Warn "No finish-feature-update marker found — diffing against main."
    $baseSHA = "main"
}

# Extract last review status and SHA
$lastReviewSHA    = $null
$lastReviewStatus = $null
$lastReviewIndex  = -1
$reviewComments   = $allCommentBodies | Where-Object { $_ -match "pr-review-findings" }
if ($reviewComments) {
    $lastReview = ($reviewComments | Select-Object -Last 1)
    if ($lastReview -match "<!--\s*review-sha:\s*([0-9a-f]{7,40})\s*-->") {
        $lastReviewSHA = $Matches[1]
    }
    if ($lastReview -match "<!--\s*review-status:\s*(approved|needs-work)\s*-->") {
        $lastReviewStatus = $Matches[1]
    }
    Write-Info "Last review: SHA=$lastReviewSHA status=$lastReviewStatus"
}

# ── 4. Scope decision ─────────────────────────────────────────────────────────
Write-Step "Determining review scope..."

if (!$Force) {
    # Guard: already approved at this exact SHA
    if ($lastReviewStatus -eq "approved" -and $lastReviewSHA -eq $HeadSHA) {
        Write-Pass "PR is already approved at HEAD ($HeadSHAShort). Use -Force to re-review."
        exit 0
    }

    # Guard: needs-work but no developer reply since the last review
    if ($lastReviewStatus -eq "needs-work" -and $lastReviewSHA -eq $HeadSHA) {
        # Look for any non-bot comments after the last review comment.
        # We detect "after" by finding the review comment text and checking
        # that at least one comment exists after it in the list.
        $foundReview     = $false
        $hasDevReply     = $false
        foreach ($body in $allCommentBodies) {
            if ($foundReview) {
                # Any comment after the review comment counts as a dev reply
                if ($body -notmatch "pr-review-findings" -and
                    $body -notmatch "finish-feature-update" -and
                    ![string]::IsNullOrWhiteSpace($body)) {
                    $hasDevReply = $true
                    break
                }
            }
            if ($body -match "pr-review-findings" -and $body -match "review-sha:\s*$HeadSHA") {
                $foundReview = $true
            }
        }

        if (!$hasDevReply) {
            Write-Warn "Last review found issues (needs-work) at HEAD ($HeadSHAShort) and no developer reply detected."
            Write-Info "Reply to the review comment, push new commits, or use -Force to re-review."
            exit 0
        }
        Write-Info "Developer reply detected since last needs-work review — proceeding with re-review."
    }
}

if ($lastReviewSHA -ne $HeadSHA) {
    Write-Info "New commits since last review (or first review) — proceeding."
} else {
    Write-Info "Force flag set — re-reviewing at HEAD ($HeadSHAShort)."
}

# ── 5. Gather diff context ────────────────────────────────────────────────────
Write-Step "Gathering diff context (base: $baseSHA → HEAD)..."

$shortLog  = git log "$baseSHA..HEAD" --oneline 2>$null
$fullLog   = git log "$baseSHA..HEAD" --pretty=format:"%h %s%n%b" 2>$null
$diffStat  = git diff --stat "$baseSHA..HEAD" 2>$null
$rawDiff   = git diff "$baseSHA..HEAD" 2>$null

$DiffCap       = 10000
$diffTruncated = $false
if ($rawDiff.Length -gt $DiffCap) {
    $rawDiff       = $rawDiff.Substring(0, $DiffCap)
    $diffTruncated = $true
}

if ([string]::IsNullOrWhiteSpace($shortLog)) {
    Write-Fail "No commits found between '$baseSHA' and HEAD. Nothing to review."
    exit 1
}

$commitCount = ($shortLog -split "`n" | Where-Object { $_ }).Count
Write-Pass "$commitCount commit(s) to review; diff stat:`n$diffStat"

# ── 6. AI code review ─────────────────────────────────────────────────────────
Write-Step "Running AI code review..."

$truncNote = if ($diffTruncated) { "`n> ⚠️ Diff truncated at $DiffCap chars." } else { "" }

$reviewPrompt = @"
You are an expert senior engineer performing a thorough code review for **KubeTools4Dev**,
a cross-platform desktop application built with **Avalonia UI and C# (.NET 10)** that helps
developers manage Kubernetes resources from a GUI.

## Project Coding Standards (enforce strictly)
- **Primary constructors** preferred for all classes (enforced in .editorconfig).
- **[LoggerMessage] source-generated methods** for all structured logging (zero-allocation).
- **Nullable reference types** enabled — no unchecked null dereferences.
- **XML documentation comments** required on all public types and members (CS1591 enforced).
- **Build**: must have **0 warnings, 0 errors** (built with -warnaserror).
- **Tests**: xUnit + NSubstitute; all new logic must have test coverage.
- **DI lifetimes**: Singleton for services/ViewModels listed in App.axaml.cs; Transient for PodListViewModel, ServiceListViewModel.
- **Async patterns**: Kubernetes watch streams use IAsyncEnumerable<(WatchEventType, T)>.
- Prefer conventional-commit style for any message text (feat/fix/chore/docs/test/refactor).

## Review Instructions
Analyse the diff carefully and report every issue you find. Use EXACTLY this format:

### 🔴 Critical Issues
List blocking issues: bugs, null-reference risks, security flaws, broken async patterns, incorrect DI lifetimes.
Use a numbered list. If none, write: _None found._

### 🟡 Warnings
List important but non-blocking issues: missing XML docs, missing tests, deviation from coding standards, performance concerns.
Use a numbered list. If none, write: _None found._

### 🔵 Info / Suggestions
Optional improvements: naming, minor style, helpful refactors.
Use a numbered list. If none, write: _None found._

### ✅ Summary
Write 2–4 sentences summarising the overall quality of the changes.
State clearly whether the changes are safe to merge or need work.

## Rules
- Return **ONLY** the Markdown review — no preamble, no conversational text, no CLI artefacts.
- Be specific: cite exact file names, class names, method names, and line context from the diff.
- Do NOT invent issues you cannot see in the diff.
- Do NOT comment on formatting unless it violates the stated coding standards.

---

## Commits
$shortLog

## Full Commit Messages
$fullLog

## Files Changed (stat)
$diffStat

## Diff$truncNote
$rawDiff
"@

$rawReview = Invoke-AI -prompt $reviewPrompt -preferred $Provider
if ([string]::IsNullOrWhiteSpace($rawReview)) {
    Write-Fail "AI review generation failed — no output from any provider."
    exit 1
}

$reviewBody = Format-AIOutput -raw $rawReview -fallback "_(AI output unavailable)_"

# ── 7. Evaluate findings ──────────────────────────────────────────────────────
Write-Step "Evaluating findings..."

$hasCritical = $reviewBody -match "🔴" -and $reviewBody -notmatch "🔴[^\n]*_None found_"
$hasWarnings = $reviewBody -match "🟡" -and $reviewBody -notmatch "🟡[^\n]*_None found_"

# More robust: count non-"None found" lines under each severity section
function Test-HasRealFindings([string]$body, [string]$emoji) {
    $inSection = $false
    foreach ($line in ($body -split "`n")) {
        if ($line -match "^###.*$emoji") { $inSection = $true; continue }
        if ($inSection -and $line -match "^###") { break }
        if ($inSection -and $line -match "^\d+\." -and $line -notmatch "_None found_") {
            return $true
        }
    }
    return $false
}

$hasCritical = Test-HasRealFindings -body $reviewBody -emoji "🔴"
$hasWarnings = Test-HasRealFindings -body $reviewBody -emoji "🟡"
$reviewStatus = if ($hasCritical -or $hasWarnings) { "needs-work" } else { "approved" }

if ($reviewStatus -eq "approved") {
    Write-Pass "No critical or warning findings — PR qualifies for approval."
} else {
    $label = @()
    if ($hasCritical) { $label += "critical issues" }
    if ($hasWarnings) { $label += "warnings" }
    Write-Warn "Review found $($label -join ' and ') — status: needs-work."
}

# ── 8. Build and post review comment ─────────────────────────────────────────
Write-Step "Posting review comment on PR #$($PR.number)..."

$Timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:sszzz")
$truncBanner = if ($diffTruncated) { "`n> ⚠️ Diff was truncated at $DiffCap chars; some changes may not have been reviewed.`n" } else { "" }

if ($reviewStatus -eq "approved") {
    $statusBadge = "## ✅ Code Review — Approved"
    $statusLine  = "**Status**: ✅ Approved — this PR is ready to merge."
    $actionLine  = ""
} else {
    $statusBadge = "## 🔍 Code Review — Needs Work"
    $statusLine  = "**Status**: 🔄 Needs work — please address the findings above and re-run ``pr-review.ps1`` or push new commits."
    $actionLine  = @"

### What to do next
1. Address all 🔴 Critical issues (required before merge).
2. Address or acknowledge 🟡 Warnings.
3. Re-run ``.\scripts\pr-review.ps1`` after pushing fixes, or reply to this comment explaining your decisions.
"@
}

$commentBody = @"
$statusBadge

_Reviewed commits: **$baseSHA → $HeadSHAShort** at $Timestamp_
$truncBanner
---

$reviewBody
$actionLine
---

$statusLine

<!-- pr-review-findings -->
<!-- review-status: $reviewStatus -->
<!-- review-sha: $HeadSHA -->
"@

Submit-WithTempFile -content $commentBody -action {
    param($f)
    gh pr comment $PR.number --body-file $f
}

if ($LASTEXITCODE -eq 0) {
    if ($reviewStatus -eq "approved") {
        Write-Pass "Approval comment posted on PR #$($PR.number) 🎉"
    } else {
        Write-Warn "Needs-work comment posted on PR #$($PR.number). Address findings and re-run."
    }
} else {
    Write-Fail "Failed to post review comment."
    exit 1
}
