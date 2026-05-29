# Backlog

## Pending
_(none)_

## In Progress
_(none)_

## Atomic Plans
_(none)_

## Completed (this session)
- [profile-supervisor](./profile-supervisor.md) — ▶ Forward on a profile enters supervised mode (auto-retry dropped forwards) with bounded backoff; tri-state Forward/Stop/Resume toggle; theme-aware banner. PR #46 ✅ merged.
- [refresh-memory-2026-05-29](./refresh-memory-2026-05-29.md) — Aligned `memory/overview.md` and backlog with `main` at v1.3.2.

## Archived Plans
- `archive/namespace-all-dynamic-pf-logging.md` — archived 2026-05-25. All-ns sentinel, live watch, pf logging. PR #40 ✅ approved, ready to merge.
- `archive/deployments-page.md` — archived 2026-05-25 (all steps complete, PR #32 merged).
- `archive/team-dispatch-poc.md` — archived 2026-05-25 (all steps complete, PRs #32/#34 merged).
- `archive/team-profile-composition.md` — archived 2026-05-25 (all steps complete, PR #34 merged).
- `archive/fix-port-forward-drops.md` — archived 2026-05-20 (PR #31 approved, pending merge).
- `archive/automated-publishing.md` — archived 2026-05-20 (Phase 3 deferred).
- `archive/modern-release-flow-status.md` — archived 2026-05-20 (Phase 1 & 2 complete).
- `archive/align-release-flow.md` — archived 2026-05-19 (work completed via PR #22).
- `archive/left-sidebar-navigation.md` — archived 2026-05-19 (completed and merged, PR #28).

## Completed
- [x] UI style consistency & deployment icon (PR #38 draft, ✅ approved 2026-05-25): RocketLaunch icon for Deployments, compact input/button normalization across all pages, ±-stepper in Settings/Pods, sidebar header removed, namespace children collapse by default, `FakeClusterConnectionManager` flaky-test fix, `SettingsViewModelTests` (8 tests), `Math.Clamp` unification. 86 tests, 0W/0E.
- [x] Multi-cluster tree navigation:`ClusterConnectionManager` per-cluster session pool, nested-`ItemsControl` sidebar (replaces Avalonia TreeView for tight indentation), Material elevation shadow removal via `ShadowAssist.ShadowDepth=Depth0`, `IDisposable` cascade `ClusterTreeViewModel` → `ClusterNodeViewModel`, `GetPortForwardService` nullable contract, UI-thread dispatch via captured `SynchronizationContext`. 77 → 78 tests (added `Dispose_UnsubscribesFromManagerEvent`). PR #36 ✅ Merged 2026-05-22.
- [x] Profile composition per role: `team.md` v2.0 `roles:` block, N-profile loading in `sk-team-start` v2.0 + `sk-team-dispatch` v1.2, prose cleanup in team-developer/team-reviewer profiles, workspace `profiles_append` example (QA PASS 2026-05-21). PR #34 ✅ Merged.
- [x] Agent team PoC + Deployments page: `sk-team-dispatch` extended to Copilot CLI, full pipeline run, Deployments page feature (QA PASS, 66/66 tests). PR #32 ✅ Merged. Release v1.2.7 published.
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
