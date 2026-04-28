[CmdletBinding()]
param (
    [string]$Title,
    [Parameter()]
    [ValidateSet("Gemini", "Copilot")]
    [string]$Provider = "Gemini",
    [switch]$Draft
)

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
Generate a professional GitHub Pull Request description in Markdown format based on the following commit messages. 
The description should include:
- A high-level summary.
- A bulleted list of changes categorized by type (Features, Fixes, Refactoring, etc.).

Commits:
$commits
"@

    # Define tool order based on preference
    $tools = if ($preferred -eq "Copilot") { @("Copilot", "Gemini") } else { @("Gemini", "Copilot") }

    foreach ($tool in $tools) {
        Write-Host "Checking availability of $tool..."

        if ($tool -eq "Gemini") {
            if (Get-Command gemini -ErrorAction SilentlyContinue) {
                try {
                    Write-Host "Generating description using Gemini..."
                    $result = $prompt | gemini ask
                    if (![string]::IsNullOrWhiteSpace($result)) { return $result }
                } catch {
                    Write-Warning "Gemini failed to generate content."
                }
            } else {
                Write-Host "Gemini CLI ('gemini') not found."
            }
        }

        if ($tool -eq "Copilot") {
            # Check for gh copilot extension first
            $hasGhCopilot = (gh extension list | Select-String "copilot")
            $hasStandaloneCopilot = (Get-Command copilot -ErrorAction SilentlyContinue)

            if ($hasGhCopilot) {
                try {
                    Write-Host "Generating description using GitHub Copilot CLI (extension)..."
                    # Using 'gh copilot explain' with redirected input
                    $result = $prompt | gh copilot explain --file - 
                    if (![string]::IsNullOrWhiteSpace($result)) { return $result }
                } catch {
                    Write-Warning "GitHub Copilot extension failed."
                }
            } elseif ($hasStandaloneCopilot) {
                try {
                    Write-Host "Generating description using standalone Copilot CLI..."
                    # Correcting syntax based on error: use -i for input in non-interactive mode
                    $result = $prompt | copilot -i "suggest PR description"
                    if (![string]::IsNullOrWhiteSpace($result)) { return $result }
                } catch {
                    Write-Warning "Standalone Copilot CLI failed."
                }
            } else {
                Write-Host "Copilot CLI (gh extension or standalone) not found."
            }
        }
    }

    Write-Warning "All AI providers failed or are unavailable. Falling back to basic commit list."
    return "## Changes`n`n" + ($commits -split "`n" | ForEach-Object { "- $_" } | Out-String)
}

$PRBody = Get-PRBody -commits $Commits -preferred $Provider

# 5. Create Pull Request
$PRTitle = if ($Title) { $Title } else { $CurrentBranch }
$ghArgs = @("pr", "create", "--title", $PRTitle, "--body", $PRBody)

if ($Draft) {
    $ghArgs += "--draft"
}

Write-Host "Creating Pull Request on GitHub..."
& gh $ghArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "Pull Request created successfully!"
} else {
    throw "Failed to create Pull Request via GitHub CLI."
}
