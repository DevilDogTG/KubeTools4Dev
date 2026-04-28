[CmdletBinding()]
param (
    [string]$Title,
    [Parameter()]
    [ValidateSet("Gemini", "Copilot")]
    [string]$Provider = "Gemini",
    [switch]$Draft
)

# Force the Windows console code page to UTF-8 (65001).
# This is the key step that tells external programs (Gemini CLI, gh copilot, etc.)
# to WRITE their stdout as UTF-8. Without it, they use the system OEM codepage
# (e.g. CP874 on Thai Windows), corrupting emoji before PowerShell ever reads them.
chcp 65001 | Out-Null

# $OutputEncoding  = bytes PowerShell writes to child process stdin (pipe).
# [Console]::OutputEncoding = bytes PowerShell reads from child process stdout.
# Both must match the code page set above.
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# 1. Environment Checks
if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI ('gh') is not installed or not in PATH."
}

# 2. Git Status Checks
$CurrentBranch = (git branch --show-current)
if ($CurrentBranch -eq "main" -or $CurrentBranch -eq "master") {
    throw "You are on the '$CurrentBranch' branch. Please switch to a feature or hotfix branch to create/update a PR."
}

$gitStatus = git status --porcelain
if (![string]::IsNullOrWhiteSpace($gitStatus)) {
    Write-Warning "Working directory has uncommitted changes. These will not be included in the PR description."
}

# 3. Check for Existing PR
Write-Host "Checking for existing PR for branch: $CurrentBranch..."
$ExistingPRJson = gh pr list --head $CurrentBranch --json number,state --jq ".[0]"
$ExistingPR = if (![string]::IsNullOrWhiteSpace($ExistingPRJson) -and $ExistingPRJson -ne "null") { $ExistingPRJson | ConvertFrom-Json } else { $null }
$IsUpdate = $null -ne $ExistingPR -and ![string]::IsNullOrWhiteSpace($ExistingPR.number)

if ($IsUpdate) {
    Write-Host "Found existing PR #$($ExistingPR.number) ($($ExistingPR.state)). Script will update this PR."
} else {
    Write-Host "No existing PR found. Script will create a new one."
}

# 4. Extract Commits
Write-Host "Extracting commit log against main..."
$Commits = git log main..HEAD --oneline
if ([string]::IsNullOrWhiteSpace($Commits)) {
    throw "No commits found on this branch that are not in 'main'."
}

# 5. Generate PR Body via AI
function Get-PRBody {
    param([string]$commits, [string]$preferred)
    
    $prompt = @"
You are an expert developer assistant. Generate a clean, professional GitHub Pull Request description in Markdown format based on the following commit messages.
IMPORTANT: Return ONLY the Markdown content. Do not include any conversational preamble (like 'Here is a suggested...'), do not include CLI progress indicators, and do not include the '---' separators at the start/end.

Commits:
$commits
"@

    # Define tool order based on preference
    $tools = if ($preferred -eq "Copilot") { @("Copilot", "Gemini") } else { @("Gemini", "Copilot") }
    $rawResult = ""

    foreach ($tool in $tools) {
        Write-Host "Checking availability of $tool..."

        if ($tool -eq "Gemini") {
            if (Get-Command gemini -ErrorAction SilentlyContinue) {
                try {
                    Write-Host "Generating description using Gemini..."
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
                    Write-Host "Generating description using GitHub Copilot extension..."
                    $rawResult = ($prompt | gh copilot explain --file -) -join "`n"
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch {
                    Write-Warning "GitHub Copilot extension failed."
                }
            } elseif ($hasStandaloneCopilot) {
                try {
                    Write-Host "Generating description using standalone Copilot CLI..."
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

    Write-Host "Cleaning up AI-generated content..."
    $cleaned = $rawResult

    # Normalize line endings to LF
    $cleaned = $cleaned -replace "\r\n", "`n"
    $cleaned = $cleaned -replace "\r", "`n"

    # Remove Copilot CLI agentic tool-use lines.
    # These are emitted as stdout and look like:
    #   "● Tool description └ shell command... └ N lines..."
    # The leading characters are Unicode box/bullet symbols (●=U+25CF, └=U+2514, ├=U+251C, etc.)
    $cleaned = $cleaned -replace "(?m)^[\u25CF\u2514\u251C\u2500\u2502\u252C\u2510\u250C\u2518\u2524\u253C]+.*$", ""

    # Remove common CLI status markers and separators
    $cleaned = $cleaned -replace "(?m)^---\s*$", ""
    $cleaned = $cleaned -replace "(?m)^Suggested PR description:\s*$", ""

    # Strip everything before the first Markdown heading or known section header.
    # (?sm): (?s) lets .* span newlines; (?m) makes ^ match at line starts.
    $cleaned = $cleaned -replace "(?sm)^.*?(?=^#{1,6}\s|^Summary\b|^Changes\b)", ""

    # Collapse 3+ consecutive blank lines down to one blank line
    $cleaned = $cleaned -replace "(?m)(\n\s*){3,}", "`n`n"

    return $cleaned.Trim()
}

$PRBody = Get-PRBody -commits $Commits -preferred $Provider

# 6. Create or Update Pull Request
$PRTitle = if ($Title) { $Title } else { $CurrentBranch }

# Write the body to a UTF-8 temp file and use --body-file.
# Passing multi-byte Unicode (emoji, special chars) as a --body argument is
# unreliable across PowerShell versions and terminal encodings; a file avoids
# all command-line argument encoding issues.
$tempBodyFile = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($tempBodyFile, $PRBody, [System.Text.UTF8Encoding]::new($false))

    if ($IsUpdate) {
        Write-Host "Updating Pull Request #$($ExistingPR.number) on GitHub..."
        gh pr edit $ExistingPR.number --title $PRTitle --body-file $tempBodyFile
    } else {
        Write-Host "Creating new Pull Request on GitHub..."
        $ghArgs = @("pr", "create", "--title", $PRTitle, "--body-file", $tempBodyFile)
        if ($Draft) { $ghArgs += "--draft" }
        & gh $ghArgs
    }
} finally {
    Remove-Item $tempBodyFile -ErrorAction SilentlyContinue
}

if ($LASTEXITCODE -eq 0) {
    $action = if ($IsUpdate) { "updated" } else { "created" }
    Write-Host "Pull Request $action successfully!"
} else {
    throw "Failed to perform PR action via GitHub CLI."
}
