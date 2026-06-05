# Plan: Port-Forward Supervisor Stability Reset

**Status:** complete
**Created:** 2026-06-05
**Branch:** bugfix/pf-supervisor-attempt-reset

## Goal
A long-running supervised port-forward must survive occasional drops indefinitely — only *rapid consecutive* failures should exhaust the retry budget and mark the profile Failed.

## Root Cause
`ProfilePortForwardSupervisor.RunSupervisedAsync` (lines 171-231): `attempt` increments monotonically and never resets. Each drop — however rare — permanently consumes 1 of `MaxAttempts = 10`. Over a long app run, cumulative transient drops (sleep/resume, API blips, pod redeploys) exhaust the budget → `Failed (10/10)` → `OnEntryExhausted` stops the whole profile. User must stop/start to reset.

Secondary: manual (unsupervised) forwards — `ServiceViewModel.StartForwarding` (lines 332-356) — never update status when `StartServicePortForwardAsync` *returns cleanly* (e.g., `AddressAlreadyInUse` break in `PortForwardService.cs:147-151`): UI shows zombie "Forwarding" on a dead listener.

## Checklist
- [x] S1 `StableRunThreshold` (internal static, default 2 min) added; `RunSupervisedAsync` times each run (Stopwatch) and resets `attempt = 0` after a stable run; `ComputeBackoff` clamps post-reset attempt 0 (`Math.Clamp` — would have crashed with `BackoffSchedule[-1]` otherwise); reset logged via `LogForwardRetryWindowReset`.
- [x] S2 Run-duration added to `LogForwardDropped` / `LogForwardCrashed`.
- [x] S3 Manual-path zombie fix in `ServiceViewModel`: clean return without cancellation toggles the row off ("Stopped"). Bonus: crash path no longer overwrites "Failed" with "Stopped"; post-cancel races no longer flash "Failed". Added `DispatchToUI`/`StartTimer`/`StopTimer` virtual seams.
- [x] S4 Tests: Core +2 (`StableRun_DoesNotConsumeRetryBudget`, `StableRunBetweenRapidFailures_ResetsAttemptWindow`, flake-checked 4×), UI +3 (`ServiceViewModelTests`: clean-return / crash / user-stop). Plus `ClusterNodeViewModelTests.WaitForAsync` hardened against concurrent-mutation flake.
- [x] S5 PR #56 — `sk-pr-review` ✅ approved at `ba4bdbb` (0🔴/0🟡/3🔵); rebase-merged 2026-06-05.

## Progress Log
- 2026-06-05: Plan created from session-start triage. Root cause verified by direct code reading.
- 2026-06-05: S1–S4 done in two atomic commits (`f8d2145` core, `6b440d4` ui) + flaky-test fix; PR #56 approved + rebase-merged (lands on main as `db1f9d1`/`88b839b`/`1d3825a`). Shipped in v1.3.6.
