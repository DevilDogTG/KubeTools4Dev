# Atomic Plan: `scripts/pr-review.ps1` — AI Code Review Script

## Status: ✅ Implemented

## Goal
Create a PowerShell script that performs an AI-driven code review on an open or draft PR,
posts structured findings as a comment, and supports re-review loops driven by developer replies
or new commits.

## Checklist

- [x] Script skeleton — param block, UTF-8 encoding, helpers (`Write-Step/Pass/Fail/Warn/Info`)
- [x] `Invoke-AI` / `Clean-AIOutput` / `Submit-WithTempFile` helpers (mirrored from finish-feature.ps1)
- [x] Preflight: `gh` tool present, not on main/master, find open/draft PR
- [x] Comment parsing: scan for `<!-- finish-feature-update -->` (base SHA) and `<!-- pr-review-findings -->` (last review SHA + status)
- [x] Scope decision: already-approved guard, needs-work-awaiting-reply guard, force override
- [x] Diff/context gathering: `git diff`, `git log`, `git diff --stat`, 10k char cap
- [x] AI review prompt: project context + coding standards + 🔴/🟡/🔵 severity structure
- [x] Findings evaluation: `Test-HasRealFindings` function, derive `approved` vs `needs-work`
- [x] Comment posting: format review comment, embed markers, post via `gh pr comment --body-file`

## Comment Markers

| Marker | Posted by | Meaning |
|--------|-----------|---------|
| `<!-- finish-feature-update -->` | finish-feature.ps1 | Update event |
| `<!-- head-sha: SHA -->` | finish-feature.ps1 | Base SHA for review diff |
| `<!-- pr-review-findings -->` | pr-review.ps1 | A review pass was posted |
| `<!-- review-status: approved\|needs-work -->` | pr-review.ps1 | Outcome |
| `<!-- review-sha: SHA -->` | pr-review.ps1 | HEAD SHA at review time |

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Provider` | `Copilot` | AI provider: `Copilot` \| `Gemini` |
| `-Force` | false | Re-review even if approved or awaiting reply |

## Usage

```powershell
# Standard review
.\scripts\pr-review.ps1

# Force re-review
.\scripts\pr-review.ps1 -Force

# Use Gemini
.\scripts\pr-review.ps1 -Provider Gemini
```

## Files

- `scripts/pr-review.ps1` — new script (no existing files modified)

---
*Created: 2026-04-28*
