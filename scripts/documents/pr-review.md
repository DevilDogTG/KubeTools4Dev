# pr-review.ps1

> **Location**: `scripts/pr-review.ps1`
> **Purpose**: Run an AI-driven code review on the current branch's open or draft PR and post structured findings as a GitHub comment.

Run this script after `finish-feature.ps1` has created a PR (or after pushing new commits). It reviews the diff, categorises findings by severity, and posts a review comment. Re-run it after addressing feedback to start a new review cycle.

---

## Prerequisites

| Tool | Why |
|------|-----|
| [Git](https://git-scm.com/) | Diff and log commands |
| [GitHub CLI (`gh`)](https://cli.github.com/) | Reads PR comments, posts review comment |
| AI provider (one of the below) | Performs the code review |
| &nbsp;&nbsp;• `gh extension copilot` **or** `copilot` CLI | GitHub Copilot (default) |
| &nbsp;&nbsp;• `gemini` CLI | Google Gemini (alternative) |

An **open or draft PR** must already exist for the current branch (created by `finish-feature.ps1`).

---

## Usage

```powershell
# Standard review (Copilot, auto-detect scope)
.\scripts\pr-review.ps1

# Force re-review even if already approved or awaiting reply
.\scripts\pr-review.ps1 -Force

# Use Gemini as the preferred provider
.\scripts\pr-review.ps1 -Provider Gemini
```

---

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-Provider` | `Copilot` \| `Gemini` | `Copilot` | Preferred AI provider. Falls back to the other if unavailable. |
| `-Force` | switch | false | Skip all smart-skip guards and always post a new review. |

---

## Review Loop

```
.\scripts\pr-review.ps1
        │
        ▼
[Preflight]
  • gh CLI present?
  • Not on main/master?
  • Open or draft PR found for current branch?
        │ fail → exit 1
        ▼
[Detect Review State]
  • Read PR comments for latest <!-- finish-feature-update --> → base SHA
  • Read PR comments for latest <!-- pr-review-findings -->   → last review SHA + status
        │
        ├─ Already approved at HEAD? (unless -Force) → exit 0 ✅
        │
        ├─ Needs-work at HEAD, no dev reply? (unless -Force) → exit 0 ⏳
        │
        └─ New commits OR first review OR -Force → proceed
        │
        ▼
[Gather Diff Context]
  git log  base..HEAD --oneline
  git diff --stat base..HEAD
  git diff base..HEAD  (capped at 10 000 chars)
        │
        ▼
[AI Code Review]
  Prompt includes:
  • KubeTools4Dev context (.NET 10, Avalonia, C#)
  • Coding standards (primary constructors, [LoggerMessage],
    nullable, XML docs, DI lifetimes, xUnit tests)
  • Full diff + commit log + file stats
        │
        ▼
[Evaluate Findings]
  🔴 Critical or 🟡 Warning items?
        ├─ Yes → status = needs-work
        └─ No  → status = approved
        │
        ▼
[Post Comment]
  needs-work → findings list + "What to do next"
  approved   → approval statement + summary
  Both embed hidden markers (see below)
```

---

## Review Severity Levels

| Icon | Level | Examples |
|------|-------|---------|
| 🔴 | **Critical** | Bugs, null-reference risks, security flaws, broken async patterns, incorrect DI lifetimes |
| 🟡 | **Warning** | Missing XML docs, missing test coverage, coding standards deviation, performance concerns |
| 🔵 | **Info** | Optional improvements, naming suggestions, minor style notes |

A PR needs **no 🔴 or 🟡** findings to be marked approved.

---

## Comment Markers

These HTML comments are **invisible on GitHub** but allow the script to track review state across re-runs.

| Marker | Posted by | Meaning |
|--------|-----------|---------|
| `<!-- finish-feature-update -->` | `finish-feature.ps1` | Update event (provides base SHA) |
| `<!-- head-sha: SHA -->` | `finish-feature.ps1` | HEAD SHA at time of finish-feature run |
| `<!-- pr-review-findings -->` | `pr-review.ps1` | This comment is a review pass |
| `<!-- review-status: approved \| needs-work -->` | `pr-review.ps1` | Outcome of the review |
| `<!-- review-sha: SHA -->` | `pr-review.ps1` | HEAD SHA when this review was posted |

---

## Re-Review Triggers

The script automatically decides whether to post a new review:

| Situation | Behaviour |
|-----------|-----------|
| No previous review | Always reviews |
| New commits pushed since last review | Always reviews |
| Last review: `needs-work`, developer replied | Reviews again |
| Last review: `needs-work`, no developer reply | Skips (use `-Force` to override) |
| Last review: `approved` at current HEAD | Skips (use `-Force` to override) |

---

## Output Legend

```
▶  Step name              (cyan)   — a phase is starting
  ✅ Message              (green)  — step passed / approved
  ❌ Message              (red)    — step failed; script exits
  ⚠️  Message             (yellow) — warning / needs-work
  ℹ️  Message             (cyan)   — informational
```

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `No open or draft PR found` | PR not created yet | Run `.\scripts\finish-feature.ps1` first |
| `Already approved` on exit 0 | PR was previously approved at this SHA | Push new commits or use `-Force` |
| `Awaiting developer response` on exit 0 | Review posted but no reply yet | Reply to the review comment on GitHub, then re-run |
| AI review generation failed | No AI provider available | Install `gh extension install github/gh-copilot` or `gemini` CLI |
| Review diff seems incomplete | Diff exceeded 10 000 char cap | A truncation warning is included in the posted comment automatically |

---

## Typical Workflow

```
1. git checkout -b feature/my-feature
2. # … make changes, commit …
3. .\scripts\finish-feature.ps1        # Creates PR, posts marker comment
4. .\scripts\pr-review.ps1             # Reviews diff, posts findings
5. # Address 🔴 / 🟡 findings, commit fixes
6. .\scripts\finish-feature.ps1        # Posts update comment with new SHA
7. .\scripts\pr-review.ps1             # Re-reviews, posts approval if clean
8. # Merge PR ✅
```

---

## See Also

- [`finish-feature.ps1`](finish-feature.md) — Preflight + PR creation/update script that provides the base SHA for review.
