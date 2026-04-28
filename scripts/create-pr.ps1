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
    throw "You are on the '$CurrentBranch' branch. Please switch to a feature or hotfix branch to create a PR."
}

$gitStatus = git status --porcelain
if (![string]::IsNullOrWhiteSpace($gitStatus)) {
    Write-Warning "Working directory has uncommitted changes. These will not be included in the PR description."
}

Write-Host "Preparing PR for branch: $CurrentBranch"

# 3. Extract Commits
Write-Host "Extracting commit log against main..."
$Commits = git log main..HEAD --oneline
if ([string]::IsNullOrWhiteSpace($Commits)) {
    throw "No commits found on this branch that are not in 'main'."
}

# 4. Generate PR Body via AI
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

    # Clean up the AI output:
    # 1. Remove conversational preambles
    # 2. Remove common CLI artifacts/encoding glitches (like the boxes/lines)
    # 3. Trim whitespace
    Write-Host "Cleaning up AI-generated content..."
    
    $cleaned = $rawResult
    # Remove everything before the first "##" or "Summary" if it looks like a preamble
    if ($cleaned -match "(?s).*?(##.*|Summary.*)") {
        $cleaned = $Matches[1]
    }
    
    # Strip common garbage characters if they exist at the start
    $cleaned = $cleaned -replace "^[\s\S]*?(?=##|###|Summary|Changes)", ""
    
    # Remove any trailing "---" or "Suggested PR" labels
    $cleaned = $cleaned -replace "(?m)^---.*$", ""
    $cleaned = $cleaned -replace "Suggested PR description:", ""

    return $cleaned.Trim()
}

$PRBody = Get-PRBody -commits $Commits -preferred $Provider

# 5. Create Pull Request
$PRTitle = if ($Title) { $Title } else { $CurrentBranch }
$ghArgs = @("pr", "create", "--title", $PRTitle, "--body", $PRBody)

if ($Draft) {
    $ghArgs += "--draft"
}

Write-Host "Creating Pull Request on GitHub..."
# Using UTF8 for the final GH command to ensure the description is readable on the web
& gh $ghArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "Pull Request created successfully!"
} else {
    throw "Failed to create Pull Request via GitHub CLI."
}
