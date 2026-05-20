# Plan: Fix Port-Forward Drops

**Status:** complete
**Created:** 2026-05-20
**Branch:** `bugfix/fix-port-forward-drops` → PR #31 (Draft, ✅ Approved)

## Goal
Make the port-forwarding service highly resilient to client aborted connections and long-lived idle WebSockets, preventing silent drops.

## Checklist
- [x] Create a new `bugfix/fix-port-forward-drops` branch.
- [x] Update `AcceptAndForwardConnectionsAsync` to ignore `ConnectionReset` and `ConnectionAborted` `SocketException`s, preventing the listener loop from breaking.
- [x] Enable `ReuseAddress` socket option in `StartServicePortForwardAsync` listener initialization to avoid `AddressAlreadyInUse` crashes on restarts.
- [x] Disable `timeoutCts` timer using `CancelAfter(Timeout.InfiniteTimeSpan)` in `HandleSingleConnectionAsync` once the WebSocket connects successfully.
- [x] Ensure `dotnet test` passes locally.
- [x] Address `sk-pr-review` findings: add edge-case tests for `ConnectionReset` and WebSocket timeout.
- [x] Refactor `TestablePortForwardService` to `Fakes/` folder; implement `IDisposable` on test class to restore `ConnectionTimeout`.
- [x] Update README to document resilient sessions feature and replace stale `scripts/` section with `sk-` agent skills.
- [x] Re-run `sk-pr-review` — ✅ Approved, zero outstanding findings.

## Progress Log
- 2026-05-20: Created draft PR [#31](https://github.com/DevilDogTG/KubeTools4Dev/pull/31) following successful execution of the `finish-feature` preflight checks.
- 2026-05-20: All `sk-pr-review` findings resolved. 34 tests pass. PR #31 is ✅ Approved and ready to merge. README updated. `TestablePortForwardService` extracted to `Fakes/`. `IDisposable` test isolation added for `ConnectionTimeout`.
