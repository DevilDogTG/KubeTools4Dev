# Backlog

## Pending
- **Profile composition per role** — Extend `dev-team/team.md` (L0 + L2) to support `profiles: []` list per role and `profiles_append: []` for workspace augmentation. Update `sk-team-start` to load and merge N profiles in order (last wins on conflict). Replaces current prose "All base-developer rules apply" with a mechanical declaration. Design settled in session 2026-05-20; implement in a dedicated session on its own branch (touches only global `~/.agent-brains/` files, not repo code).

## In Progress
- `plan/team-dispatch-poc.md` — PoC: automated agent team dispatch pipeline + Deployments page feature.
- `plan/deployments-page.md` — Deployments page: list view, Rollout Restart action, Edit dialog (replica count + image tag).

## Atomic Plans
_(none)_

## Archived Plans
- `archive/fix-port-forward-drops.md` — archived 2026-05-20 (PR #31 approved, pending merge).
- `archive/automated-publishing.md` — archived 2026-05-20 (Phase 3 deferred).
- `archive/modern-release-flow-status.md` — archived 2026-05-20 (Phase 1 & 2 complete).
- `archive/align-release-flow.md` — archived 2026-05-19 (work completed via PR #22).
- `archive/left-sidebar-navigation.md` — archived 2026-05-19 (completed and merged, PR #28).

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
- [x] Added `SidebarViewModel` unit tests + extracted sidebar state to Core (32 tests).
- [x] Applied pr-review findings to `feature/pod-detail-popup-window`: `[LoggerMessage]`, primary constructors, 10 new unit tests in `KubeTools4Dev.Tests`. Build 0W/0E, tests 42/42.
- [x] Merged PR #28 — replace split panel with independent popup windows for logs/describe (+ `KubeTools4Dev.Tests` project). Total: 42 tests.
- [x] Fixed silent port-forward drops (PR #31 ✅ Approved): resilient listener loop, ReuseAddress, no idle timeout. Added edge-case tests (34 total). Refactored fakes. Updated README.
