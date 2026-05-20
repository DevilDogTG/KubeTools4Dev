# Plan: Fix Port-Forward Drops

**Status:** active
**Created:** 2026-05-20
**Branch:** [branch-name-pending]

## Goal
Make the port-forwarding service highly resilient to client aborted connections and long-lived idle WebSockets, preventing silent drops.

## Checklist
- [x] Create a new `bugfix/fix-port-forward-drops` branch.
- [x] Update `AcceptAndForwardConnectionsAsync` to ignore `ConnectionReset` and `ConnectionAborted` `SocketException`s, preventing the listener loop from breaking.
- [x] Enable `ReuseAddress` socket option in `StartServicePortForwardAsync` listener initialization to avoid `AddressAlreadyInUse` crashes on restarts.
- [x] Disable `timeoutCts` timer using `CancelAfter(Timeout.InfiniteTimeSpan)` in `HandleSingleConnectionAsync` once the WebSocket connects successfully.
- [x] Ensure `dotnet test` passes locally.

## Progress Log
_Updated as steps complete._
