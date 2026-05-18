# Onboard Script: Centralized Agent Framework (Windows/PowerShell)
# Run this in a TARGET PROJECT WORKSPACE to bootstrap .agent-brains/ there.
#
# Prerequisites: Run scripts/install.ps1 ONCE on this machine first.
# That creates the symlink: ~/.agent-brains/ → <brains-repo>/src/
#
# This script:
#   1. Creates .agent-brains/{memory,plan,skills} in the current directory
#   2. Bootstraps .agent-brains/AGENT.md with workspace-level config
#   3. Creates provider entry points (GEMINI.md, .cursorrules, etc.)

$ErrorActionPreference = "Stop"

# Helper: backs up a file to <path>.bak.<timestamp> if it exists
function Backup-IfExists {
    param([string]$Path)
    if (Test-Path $Path) {
        $timestamp  = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupPath = "$Path.bak.$timestamp"
        Copy-Item -Path $Path -Destination $backupPath
        Write-Host "  Backed up: $Path → $backupPath" -ForegroundColor Yellow
    }
}

Write-Host "=== Centralized Agent Framework Onboarding ===" -ForegroundColor Blue

# 1. Create directory structure
Write-Host "Creating .agent-brains/ namespace..."
$directories = @(
    ".agent-brains/memory",
    ".agent-brains/plan/archive",
    ".agent-brains/skills"
)
foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# 2. Configure Global Brain Path
$defaultGlobalDir  = Join-Path $HOME ".agent-brains"
$defaultGlobalFile = Join-Path $defaultGlobalDir "GLOBAL_AGENT.md"
$globalPath = "~\.agent-brains\GLOBAL_AGENT.md"

Write-Host "Configuring Global Brain..."

if (Test-Path $defaultGlobalFile) {
    # Use tilde form — safe to commit, no home dir path exposed
    Write-Host "Detected global brain at $globalPath" -ForegroundColor Green
} else {
    throw "Global brain not found at expected location: $defaultGlobalFile. Please run scripts/install.ps1 first to set up the global brain symlink."
}

# 3. Bootstrap AGENT.md
$agentFile = ".agent-brains\AGENT.md"
Write-Host "Bootstrapping local workspace AGENT.md..."
$currentDirName = Split-Path (Get-Location) -Leaf
$agentContent = @"
---
version: 1.0
profiles:
  - base-developer
strict_override: false
---

# Workspace Instructions

## Overview
Project-specific context and local overrides for $currentDirName.

## Workspace Rules
- Follow the framework mechanics defined in ``$globalPath``.
- Maintain operational state strictly in .\.agent-brains\.
"@
Backup-IfExists $agentFile
Set-Content -Path $agentFile -Value $agentContent

# 4. Create Provider Entry Points
Write-Host "--- Provider Configuration ---" -ForegroundColor Cyan
Write-Host "Which AI providers do you use in this workspace? (y/n)"

$useGemini = Read-Host "Setup Gemini CLI (GEMINI.md)? [y/N]"
$useClaude = Read-Host "Setup Claude Code (CLAUDE.md)? [y/N]"
$useCursor = Read-Host "Setup Cursor (.cursorrules)? [y/N]"
$useCodex  = Read-Host "Setup Codex (.codexrules)? [y/N]"
$useCopilot = Read-Host "Setup GitHub Copilot (.github\copilot-instructions.md)? [y/N]"

Write-Host "Setting up selected provider entry points..."

# Gemini CLI
if ($useGemini -match "^[Yy]$") {
    Backup-IfExists "GEMINI.md"
    Copy-Item $agentFile "GEMINI.md"
    Write-Host "Created GEMINI.md" -ForegroundColor Green
}

# Claude Code
if ($useClaude -match "^[Yy]$") {
    $claudeContent = @"
# Mandate: Centralized Brains (Claude Code)
1. Read the global framework rules from ``$globalPath``.
2. Read the local workspace directives from ``.\.agent-brains\AGENT.md``.
3. Use ``.\.agent-brains\memory\`` for project context.
4. Always write plans to ``.\.agent-brains\plan\`` BEFORE writing code.
"@
    Backup-IfExists "CLAUDE.md"
    Set-Content -Path "CLAUDE.md" -Value $claudeContent
    Write-Host "Created CLAUDE.md" -ForegroundColor Green
}

# Cursor
if ($useCursor -match "^[Yy]$") {
    $cursorContent = @"
# Centralized Brains: Cursor Rules
1. Read the global framework rules at ``$globalPath``.
2. Read the local workspace directives at ``.\.agent-brains\AGENT.md``.
3. Adhere to the .agent-brains\ namespace for all operational state.
"@
    Backup-IfExists ".cursorrules"
    Set-Content -Path ".cursorrules" -Value $cursorContent
    Write-Host "Created .cursorrules" -ForegroundColor Green
}

# Codex
if ($useCodex -match "^[Yy]$") {
    $codexContent = @"
# Centralized Brains: Codex Rules
1. Read the global framework rules at ``$globalPath``.
2. Read the local workspace directives at ``.\.agent-brains\AGENT.md``.
3. Adhere to the .agent-brains\ namespace for all operational state.
"@
    Backup-IfExists ".codexrules"
    Set-Content -Path ".codexrules" -Value $codexContent
    Write-Host "Created .codexrules" -ForegroundColor Green
}

# Copilot
if ($useCopilot -match "^[Yy]$") {
    if (-not (Test-Path ".github")) {
        New-Item -ItemType Directory -Path ".github" -Force | Out-Null
    }
    $copilotContent = @"
# Mandate: Centralized Brains (Copilot)
1. Read the core rules from ``$globalPath``.
2. Read the local project directives from ``.\.agent-brains\AGENT.md``.
3. Use ``.\.agent-brains\memory\`` for project context.
4. Always write plans to ``.\.agent-brains\plan\`` BEFORE writing code.
"@
    Backup-IfExists ".github\copilot-instructions.md"
    Set-Content -Path ".github\copilot-instructions.md" -Value $copilotContent
    Write-Host "Created .github\copilot-instructions.md" -ForegroundColor Green
}

# 5. Initialize Roadmap and Memory
if (-not (Test-Path ".agent-brains\plan\backlog.md")) {
    "# Project Roadmap`n`n## Goals`n- [ ] Initial task" | Set-Content ".agent-brains\plan\backlog.md"
}

if (-not (Test-Path ".agent-brains\memory\overview.md")) {
    "# Project Overview`n`nInitial status." | Set-Content ".agent-brains\memory\overview.md"
}

Write-Host "=== Onboarding Complete ===" -ForegroundColor Green
Write-Host "Workspace is now aligned with Centralized Brains rules."
