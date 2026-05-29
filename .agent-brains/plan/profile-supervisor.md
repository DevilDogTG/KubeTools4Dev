# Plan: Profile Port-Forward Supervisor

**Status:** completed
**Created:** 2026-05-29
**Branch:** feature/profile-supervisor

## Goal
Make ▶ Start on a profile enter supervised mode (auto-retry dropped forwards); ■ Stop exits. Manual one-off forwards stay manual. Full design in `~/.claude/plans/moonlit-rolling-seahorse.md` (approved 2026-05-29).

## Decisions confirmed
- **Exhaustion**: when any entry hits max retries → stop the whole profile + error banner.
- **Manual override**: user toggling a supervised row off → unsupervise that one entry permanently + info banner. Other entries keep running.
- **Persistence across app restart**: deferred (follow-up).

## Checklist (Phases)
- [x] **P1 Core types + supervisor** — `IProfilePortForwardSupervisor`, `ProfilePortForwardSupervisor`, log messages, `SupervisedForwardState` enum, `SupervisedForwardSnapshot` + `ProfileFailureReason` records. Wired into `PortForwardServiceFactory` and `ClusterConnectionManager`. 13 Core tests + `FakePortForwardService` fake.
- [x] **P2 UI wiring (VM)** — `ServiceListViewModel.StartProfile/StopProfile` delegate to supervisor; subscribe to `EntryStateChanged` + `ProfileStoppedDueToFailure`; `BannerMessage` / `BannerSeverity` / `DismissBannerCommand`; `protected virtual DispatchToUI` test hook; updated `Cleanup` to detach. 9 UI tests.
- [x] **P3 `ServiceViewModel` manual-vs-supervised gate** — `IsSupervised` + `OnSupervisedStopRequested`; setter routes supervised toggle-off through supervisor; manual ToggleSwitch path unchanged for unsupervised rows.
- [x] **P4 Banner UI (XAML)** — banner panel in `ServiceListView.axaml` with severity-driven Classes (info/warning/error) and dismiss button.
- [x] **P5 Status strings** — `"Retrying (n/max)"`, `"Failed (n/max)"`, `"Unsupervised"` (applied in `ApplyEntrySnapshot`).
- [x] **P6 Verify** — `dotnet build -warnaserror` 0W/0E. Full test pass: Core 84/84 (71 + 13), UI 66/66 (57 + 9). Manual smoke remains for user (requires a live cluster).

## Progress Log
- 2026-05-29: Plan approved, branch `feature/profile-supervisor` created.
- 2026-05-29: P1 — Core supervisor + 13 tests landed, all green. P1 wired through `IPortForwardServiceFactory.CreateSupervisor` and `ClusterConnectionManager.GetProfileSupervisor`.
- 2026-05-29: P2 — `ServiceListViewModel` now async, delegates Start/Stop/Delete to supervisor, owns banner state + `DispatchToUI` test hook.
- 2026-05-29: P3 — `ServiceViewModel.IsSupervised` + `OnSupervisedStopRequested`; setter logic so supervised toggle-off does not double-stop.
- 2026-05-29: P4 — Banner XAML added: Border with severity classes, dismiss button; HasBannerMessage/IsBanner{Info,Warning,Error} computed bindings on VM.
- 2026-05-29: P5 — Status strings come from `ApplyEntrySnapshot` switch.
- 2026-05-29: P6 — Full build 0W/0E on `-warnaserror`; Core 84, UI 66, total 150 tests passing. Awaiting user smoke test against a live cluster.
- 2026-05-29: User feedback — banner unreadable on dark theme; want re-supervise path; single Forward↔Stop toggle; duration timer for supervised rows; atomic commits. Addressed via theme-aware fills, `OnSupervisedResumeRequested`, single `ToggleProfileCommand`, `StartDurationTimerIfStopped`. 153 tests passing.
- 2026-05-29: Tri-state toggle landed (`▶ Forward` / `■ Stop` / `▶ Resume`); orange tint when has-unsupervised; banner message updated. After-Resume color bug fixed by replacing `ToggleButton` with `Button` (class-driven styling, no `IsChecked` race).
- 2026-05-29: PR #46 opened as draft via `sk-finish-feature`. https://github.com/DevilDogTG/KubeTools4Dev/pull/46
- 2026-05-29: `sk-pr-review` posted findings — needs-work. 🔴 critical: `OnEntryExhausted` leaves stale entries in `_entries` blocking subsequent profile restart. https://github.com/DevilDogTG/KubeTools4Dev/pull/46#issuecomment-4570843087
- 2026-05-29: Review fixes landed in 3 atomic commits — exhaustion cleanup + regression test (`da3feb8`), primary constructor + StopAll disposal (`c933e6b`), resume-fallback banner + UI-thread doc (`02e5a35`). 85 Core + 69 UI tests passing.
- 2026-05-29: `sk-pr-review` re-run → **approved** at 02e5a35. https://github.com/DevilDogTG/KubeTools4Dev/pull/46#issuecomment-4571419703
- 2026-05-29: Session closed via `sk-session-end`. PR #46 awaiting manual smoke + merge; no remaining blockers.
- 2026-05-29: **PR #46 merged into `main`.** Remote branch deleted; main at `1a8c791`. Plan archived.
