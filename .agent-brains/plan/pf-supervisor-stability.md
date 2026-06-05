# Plan: Port-Forward Supervisor Stability Reset

**Status:** active
**Created:** 2026-06-05
**Branch:** bugfix/pf-supervisor-attempt-reset

## Goal
A long-running supervised port-forward must survive occasional drops indefinitely — only *rapid consecutive* failures should exhaust the retry budget and mark the profile Failed.

## Root Cause
`ProfilePortForwardSupervisor.RunSupervisedAsync` (lines 171-231): `attempt` increments monotonically and never resets. Each drop — however rare — permanently consumes 1 of `MaxAttempts = 10`. Over a long app run, cumulative transient drops (sleep/resume, API blips, pod redeploys) exhaust the budget → `Failed (10/10)` → `OnEntryExhausted` stops the whole profile. User must stop/start to reset.

Secondary: manual (unsupervised) forwards — `ServiceViewModel.StartForwarding` (lines 332-356) — never update status when `StartServicePortForwardAsync` *returns cleanly* (e.g., `AddressAlreadyInUse` break in `PortForwardService.cs:147-151`): UI shows zombie "Forwarding" on a dead listener.

## Checklist
- [ ] S1 Add `StableRunThreshold` (internal static, default 2 min) to `ProfilePortForwardSupervisor`. In `RunSupervisedAsync`, time each `StartServicePortForwardAsync` call; if it ran ≥ threshold before dropping, reset `attempt = 0` (the drop that follows counts as attempt 1 of a fresh window). Log the reset.
- [ ] S2 Include run-duration in `LogForwardDropped` / `LogForwardCrashed` so future drop diagnosis has data.
- [ ] S3 Manual-path zombie fix: in `ServiceViewModel.StartForwarding`, after `await StartServicePortForwardAsync` returns without cancellation, post `Status = "Stopped"`, `IsForwarding = false`, stop timer.
- [ ] S4 Tests (Core): (a) forward runs ≥ threshold then drops repeatedly > MaxAttempts times → never reaches `Failed`, attempt counter observed resetting; (b) rapid failures (< threshold) still exhaust at 10 → `Failed`; (c) reset logs snapshot state correctly. Tests (UI): manual clean-return → Status "Stopped" + toggle off. Use existing `FakePortForwardService` / supervisor test patterns (`ProfilePortForwardSupervisorTests.cs`).
- [ ] S5 Preflight + PR via sk-finish-feature.

## Progress Log
- 2026-06-05: Plan created from session-start triage. Root cause verified by direct code reading.
