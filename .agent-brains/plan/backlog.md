# Backlog

## Pending
- [ ] Merge `feature/test-coverage` into `main` (PR #18 — open, ready for review).
- [ ] Monitor bug reports and address UI/UX improvements.

## In Progress
- [ ] `feat/left-sidebar-navigation` — collapsible left sidebar replacing top TabControl. See `left-sidebar-navigation.md`.

## Atomic Plans
- `modern-release-flow-status.md` — Phase 1 & 2 complete.
- `left-sidebar-navigation.md` — collapsible left sidebar (🔄 active).

## Completed
- [x] Initialized workspace via Centralized Agent Framework `project-initializer` skill.
- [x] Synchronized `main` and `develop` and deleted local `develop`.
- [x] Cleaned up 9 stale local branches (`features/*`, `hotfix/*`, `release/*`).
- [x] Phase 1: Branching strategy setup complete.
- [x] Phase 2: AI-Powered PR Automation complete.
- [x] Added initial test coverage (23 tests: SettingsModel, SettingsService, PortForwardService).
- [x] Fixed 6 Avalonia AVLN5001 warnings (TextBox.Watermark → PlaceholderText).
- [x] Implemented `finish-feature` skill (v2) — self-contained preflight + PR create/update workflow; replaced `scripts/finish-feature.ps1`.
- [x] Implemented `pr-review` skill — self-contained AI code review; replaced `scripts/pr-review.ps1`.
- [x] Removed `scripts/` directory — all workflows now live in `.agent-brains/skills/`.

## Deferred
- **Phase 3: Streamlined Release Automation** — changelog gen, version bump, GitHub release. Needs scoping discussion.
