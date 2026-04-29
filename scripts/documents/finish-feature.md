# finish-feature.ps1

> **Location**: `scripts/finish-feature.ps1`
> **Purpose**: Run all preflight checks and create or update the GitHub Pull Request for your current feature branch.

This is the **primary developer workflow script**. Run it when you are ready to open a PR or push an update.

---

## Prerequisites

| Tool | Why |
|------|-----|
| [Git](https://git-scm.com/) | Source control — must be on a feature/bugfix branch |
| [GitHub CLI (`gh`)](https://cli.github.com/) | Creates and updates PRs |
| [.NET 10 SDK](https://dotnet.microsoft.com/) | Build + test gates |
| AI provider (one of the below) | Generates PR title & description |
| &nbsp;&nbsp;• `gh extension copilot` **or** `copilot` CLI | GitHub Copilot (default) |
| &nbsp;&nbsp;• `gemini` CLI | Google Gemini (alternative) |

---

## Usage

```powershell
# Minimal — AI title, Copilot provider, open PR (not draft)
.\scripts\finish-feature.ps1

# Custom title
.\scripts\finish-feature.ps1 -Title "feat(core): improve port-forward retry logic"

# Open as draft
.\scripts\finish-feature.ps1 -Draft

# Use Gemini instead of Copilot
.\scripts\finish-feature.ps1 -Provider Gemini

# Skip rebase step (e.g. on a clean isolated branch)
.\scripts\finish-feature.ps1 -SkipRebase

# Skip test run (use sparingly)
.\scripts\finish-feature.ps1 -SkipTests
```

---

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-Title` | string | _(AI-generated)_ | Override the PR title. Must be ≤ 72 chars, conventional-commit format recommended. |
| `-Provider` | `Copilot` \| `Gemini` | `Copilot` | AI provider for title & description generation. Falls back to the other if unavailable. |
| `-Draft` | switch | false | Open the PR as a draft. Ignored on re-runs (PR already exists). |
| `-SkipRebase` | switch | false | Skip the rebase-from-main preflight step. |
| `-SkipTests` | switch | false | Skip the `dotnet test` preflight step. |

---

## Preflight Steps

All six steps must pass before the PR is created or updated. Any failure exits immediately with a non-zero code.

| # | Check | What it does |
|---|-------|-------------|
| 1 | **Tools** | Verifies `gh` CLI is installed and in PATH. |
| 2 | **Clean tree** | Ensures no uncommitted changes (`git status --porcelain`). |
| 3 | **Branch guard** | Ensures you are not on `main` or `master`, and that the branch has at least one commit ahead of `main`. |
| 4 | **Rebase from main** | Fetches `origin/main` and rebases your branch if it is behind. _(skippable with `-SkipRebase`)_ |
| 5 | **Build** | Runs `dotnet build -warnaserror` — must produce **0 warnings, 0 errors**. |
| 6 | **Tests** | Runs `dotnet test` — all xUnit tests must pass. _(skippable with `-SkipTests`)_ |

---

## Behaviour: First Run (no PR yet)

1. Gathers commit log, diff stat, and diff (truncated at 6 000 chars).
2. Generates a conventional-commit style PR title via AI (falls back to sanitised branch name).
3. Generates a full PR description via AI using the template:
   - **Overview** — 2-3 sentence summary
   - **What's Changed** — grouped bullet points with real file/class/method names
   - **Files Changed** — table of New / Modified / Deleted files
   - **Testing** — how to verify the changes
4. Creates the PR with `gh pr create`.
5. Posts an **initial marker comment** with the HEAD SHA so future re-runs can detect exactly which commits are new.

---

## Behaviour: Re-run (PR already exists)

The PR body is **never overwritten**. Instead, a structured update comment is posted containing:

- ✅ Preflight results (build & test status)
- List of new commits since the last `finish-feature` run
- AI-generated summary of those new commits

If no new commits have been pushed since the last update comment, the script exits cleanly without posting a duplicate.

---

## Comment Markers

The script embeds hidden HTML comments in PR comments. These are **invisible on GitHub** but machine-readable by `pr-review.ps1`.

| Marker | Meaning |
|--------|---------|
| `<!-- finish-feature-update -->` | This comment is a finish-feature update event. |
| `<!-- head-sha: SHA -->` | The HEAD SHA at the time this comment was posted. Used by `pr-review.ps1` to determine the diff base. |

---

## Output Legend

```
▶  Step name              (cyan)   — a phase is starting
  ✅ Message              (green)  — step passed
  ❌ Message              (red)    — step failed; script exits
  ⚠️  Message             (yellow) — warning; script continues
```

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `Working tree is dirty` | Uncommitted changes | `git add . && git commit` or `git stash` |
| `Nothing to PR` | No commits ahead of `main` | Make at least one commit on your branch |
| `Branch is N commit(s) behind` | `main` moved forward | Script auto-rebases; if conflicts arise, resolve and re-run |
| Build gate fails | Code warnings or errors | Fix all warnings (`-warnaserror` is enforced) |
| AI generation produces empty output | No AI provider available | Install `gh extension install github/gh-copilot` or `gemini` CLI |
| Duplicate update comment | Re-run with no new commits | Not a bug — script exits cleanly without posting |

---

## See Also

- [`pr-review.ps1`](pr-review.md) — AI code review that reads `finish-feature` markers.
