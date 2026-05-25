# Plan: Namespace Enhancements + Port-Forward Logging

**Status:** complete  
**Created:** 2026-05-25  
**Branch:** `feature/namespace-all-dynamic-pf-logging`
**PR:** #40 — https://github.com/DevilDogTG/KubeTools4Dev/pull/40 (✅ approved, ready to merge)

## Goal
(1) Add a virtual "all namespaces" entry per cluster in the sidebar. (2) Keep the namespace list live by watching Kubernetes namespace events. (3) Add richer logging to port-forward connections for future drop-investigation.

## Checklist

### Feature A — "all" Virtual Namespace Node
- [x] A1 `NamespaceNodeViewModel`: add `DisplayName` property (defaults to `Name`; optional `displayName` ctor param).
- [x] A2 `ClusterNodeViewModel.LoadNamespacesAsync()`: prepend `NamespaceNodeViewModel("", clusterId, cb, "(all namespaces)")`.
- [x] A3 `ClusterTreeView.axaml`: italic style for the "all" node.

### Feature B — Dynamic Namespace Watch
- [x] B1 `IKubernetesService`: add `WatchNamespacesAsync(CancellationToken)`.
- [x] B2 `KubernetesService`: implement using `WatchListNamespaceAsync`.
- [x] B3 `SettingsModel`: add `NamespacesSettings.WatchRetryDelayMilliseconds` (default 5000).
- [x] B4 `ClusterNodeViewModel`: start watch loop on connect; handle ADDED/DELETED; cancel on disconnect.
- [x] B5 `ClusterNodeViewModel.Dispose()`: cancel/dispose `_namespaceCts`.

### Issue C — Port-Forward Logging
- [x] C1 `HandleSingleConnectionAsync`: generate `connId`, record `startTime`, pass to copy methods.
- [x] C2 `finally` block: log connection lifetime and close reason.
- [x] C3 Add heartbeat task (every 5 min); use `Task.WhenAny` over all three tasks.
- [x] C4 Copy loops: include `connId` and exception type name in `LogStreamClosed`.
- [x] C5 New `[LoggerMessage]` methods: `LogConnectionLifetime`, `LogConnectionHeartbeat`.

### Tests
- [x] T1 `NamespaceNodeViewModelTests`: DisplayName defaults + override; "all" node passes `""` namespace.
- [x] T2 `ClusterNodeViewModelTests`: first namespace node is the "all" sentinel after connect.
- [x] T3 `ClusterNodeViewModelTests`: watch ADDED appends; DELETED removes; sentinel not duplicated.
- [x] T4 Port-forward: `LogConnectionLifetime` called; heartbeat fires.

### Build Verification
- [x] V1 `dotnet build -warnaserror` → 0W/0E.
- [x] V2 Core tests pass.
- [x] V3 UI tests pass.

### PR Review (sk-pr-review)
- [x] First pass: 1 🟡 warning (`[LoggerMessage]` for namespace watch failure log).
- [x] Fix: `ClusterNodeViewModel.LogMessages.cs` added; `LogPortForwardStopFailed` XML doc corrected.
- [x] Re-review: ✅ Approved (commit `3304d7f`).

## Progress Log

### 2026-05-25 — Full implementation + PR review cycle complete

**Features delivered (commit `3776de1`):**
- **A (all-ns sentinel):** `NamespaceNodeViewModel` gets optional `displayName` param + `DisplayName`/`IsAllNamespaces` props. `ClusterNodeViewModel.LoadNamespacesAsync` prepends `NamespaceNodeViewModel("", …, "(all namespaces)")`. `BoolToItalicConverter` styles it italic. Routes via existing `IsAllNamespaces("")` — no new API logic needed.
- **B (watch loop):** `WatchNamespacesAsync(CancellationToken) → IAsyncEnumerable<(WatchEventType, V1Namespace)>` added to interface + service. `ClusterNodeViewModel` starts `Task.Run(WatchNamespacesLoopAsync)` on connect, handles ADDED/DELETED events via `PostToUi`, cancels on disconnect/dispose. Retry delay via `NamespacesSettings.WatchRetryDelayMilliseconds` (default 5000 ms).
- **C (pf logging):** Per-connection `connId` (8-char hex), `startTime`, lifetime log in `finally`, 5-min heartbeat via `RunHeartbeatAsync` + `Task.WhenAny`, exception type in stream-closed logs.
- **Tests:** 47 → 58 Core tests (+11). 39 UI tests unchanged. All pass.

**PR review findings + fix (commit `3304d7f`):**
- 🟡 `_logger?.LogWarning(...)` → fixed with `ClusterNodeViewModel.LogMessages.cs` (`[LoggerMessage]` static partial, nullable-logger pattern: `if (_logger is not null) LogNamespaceWatchFailed(_logger, Id, ex)`).
- 🔵 Copy-paste XML doc on `LogPortForwardStopFailed` corrected.
- Re-review: ✅ Approved.

**Key decisions:**
- Sentinel uses `""` as namespace name — routes to cluster-wide API via pre-existing `IsAllNamespaces("")`, no new routing needed.
- Watch loop is fire-and-forget `_ = Task.Run(...)` to keep `ClusterNodeViewModel` self-contained; cancellation via `_namespaceCts`.
- `HeartbeatInterval` is non-readonly static (like `ConnectionTimeout`) to allow test override.
- `FakeClusterConnectionManager.ServiceFactory` delegate added for per-test mock service pre-configuration (required for `IAsyncEnumerable` watch event injection since NSubstitute returns null by default).
