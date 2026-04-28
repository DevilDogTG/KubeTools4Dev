# Backlog

## Pending
- [ ] Merge `feature/test-coverage` into `main` (PR #18 — open, ready for review).
- [ ] Monitor bug reports and address UI/UX improvements.

## In Progress
- [ ] PR for `feature/pr-review-script` — `scripts/pr-review.ps1` AI code review script.

## Atomic Plans
- `modern-release-flow-status.md` — Phase 1 & 2 complete.
- `pr-review-script.md` — AI code review script (✅ implemented, awaiting PR merge).

## Completed
- [x] Initialized workspace via Centralized Agent Framework `project-initializer` skill.
- [x] Synchronized `main` and `develop` and deleted local `develop`.
- [x] Cleaned up 9 stale local branches (`features/*`, `hotfix/*`, `release/*`).
- [x] Implemented `scripts/create-pr.ps1` with Gemini/Copilot support and fallback logic.
- [x] Fixed `create-pr.ps1` emoji/Unicode encoding (chcp 65001 + [Console]::OutputEncoding + --body-file).
- [x] Created `scripts/finish-feature.ps1` — full preflight workflow script:
    - Clean tree check
    - No-commit guard (abort if nothing ahead of main)
    - Rebase from origin/main with force-push
    - `dotnet build -warnaserror` gate
    - `dotnet test` gate
    - AI-generated PR title (conventional-commit format, fallback to branch name)
    - Rich PR description (full commit log + diff stat + truncated diff)
    - Update comment on re-run (machine-parseable markers for future pr-review script)
- [x] Added initial test coverage (23 tests: SettingsModel, SettingsService, PortForwardService).
- [x] Fixed 6 Avalonia AVLN5001 warnings (TextBox.Watermark → PlaceholderText).
- [x] Phase 1: Branching strategy setup complete.
- [x] Phase 2: AI-Powered PR Automation complete.

## Deferred
- **Phase 3: Streamlined Release Automation** — changelog gen, version bump, GitHub release. Needs scoping discussion.
