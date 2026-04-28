[CmdletBinding()]
param (
    [string]$Title,
    [Parameter()]
    [ValidateSet("Gemini", "Copilot")]
    [string]$Provider = "Gemini",
    [switch]$Draft
)

# Force UTF8 encoding for all output to prevent unreadable characters in PRs
$OutputEncoding = [System.Text.UTF8Encoding]::new()
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
                    $rawResult = $prompt | gemini ask
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
                    $rawResult = $prompt | gh copilot explain --file - 
                    if (![string]::IsNullOrWhiteSpace($rawResult)) { break }
                } catch {
                    Write-Warning "GitHub Copilot extension failed."
                }
            } elseif ($hasStandaloneCopilot) {
                try {
                    Write-Host "Generating description using standalone Copilot CLI..."
                    $rawResult = copilot --prompt $prompt
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
    
    # Robustly remove preambles using -replace instead of $Matches indexing
    # This strips everything before the first line that starts with #, Summary, or Changes
    $cleaned = $cleaned -replace "(?s)^.*?(?=^#|^Summary|^Changes)", ""
    
    # Remove common CLI status markers and separators
    $cleaned = $cleaned -replace "(?m)^---.*$", ""
    $cleaned = $cleaned -replace "Suggested PR description:", ""

    return $cleaned.Trim()
}

$PRBody = Get-PRBody -commits $Commits -preferred $Provider

# 6. Create or Update Pull Request
$PRTitle = if ($Title) { $Title } else { $CurrentBranch }

if ($IsUpdate) {
    Write-Host "Updating Pull Request #$($ExistingPR.number) on GitHub..."
    gh pr edit $ExistingPR.number --title $PRTitle --body $PRBody
} else {
    Write-Host "Creating new Pull Request on GitHub..."
    $ghArgs = @("pr", "create", "--title", $PRTitle, "--body", $PRBody)
    if ($Draft) { $ghArgs += "--draft" }
    & gh $ghArgs
}

if ($LASTEXITCODE -eq 0) {
    $action = if ($IsUpdate) { "updated" } else { "created" }
    Write-Host "Pull Request $action successfully!"
} else {
    throw "Failed to perform PR action via GitHub CLI."
}
