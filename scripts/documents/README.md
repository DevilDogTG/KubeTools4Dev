# Scripts Documentation

This directory contains user-facing documentation for the developer workflow scripts in [`scripts/`](../).

---

## Active Scripts

| Script | Description | Docs |
|--------|-------------|------|
| [`finish-feature.ps1`](../finish-feature.ps1) | Run preflights (build, test, rebase) and create or update a GitHub PR with an AI-generated description. | [finish-feature.md](finish-feature.md) |
| [`pr-review.ps1`](../pr-review.ps1) | AI-driven code review — reviews the PR diff and posts structured findings (🔴 Critical / 🟡 Warning / 🔵 Info) as a GitHub comment. | [pr-review.md](pr-review.md) |

---

## Typical Workflow

```
git checkout -b feature/my-feature
# … commit your changes …
.\scripts\finish-feature.ps1    # preflight → create/update PR
.\scripts\pr-review.ps1         # AI review → post findings comment
# address any 🔴/🟡 findings, push fixes
.\scripts\finish-feature.ps1    # update comment with new SHA
.\scripts\pr-review.ps1         # re-review → approval comment ✅
# merge PR
```

---

## Obsoleted Scripts

The [`obsoleted/`](../obsoleted/) folder contains legacy release and build scripts that are no longer part of the active workflow. See [`obsoleted/README.md`](../obsoleted/README.md) for details.
