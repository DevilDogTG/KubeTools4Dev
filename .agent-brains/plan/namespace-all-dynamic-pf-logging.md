# Plan: Namespace Enhancements + Port-Forward Logging

**Status:** active  
**Created:** 2026-05-25  
**Branch:** `feature/namespace-all-dynamic-pf-logging`

## Goal
(1) Add a virtual "all namespaces" entry per cluster in the sidebar. (2) Keep the namespace list live by watching Kubernetes namespace events. (3) Add richer logging to port-forward connections for future drop-investigation.

## Checklist

### Feature A — "all" Virtual Namespace Node
- [ ] A1 `NamespaceNodeViewModel`: add `DisplayName` property (defaults to `Name`; optional `displayName` ctor param).
- [ ] A2 `ClusterNodeViewModel.LoadNamespacesAsync()`: prepend `NamespaceNodeViewModel("", clusterId, cb, "(all namespaces)")`.
- [ ] A3 `ClusterTreeView.axaml`: italic style for the "all" node.

### Feature B — Dynamic Namespace Watch
- [ ] B1 `IKubernetesService`: add `WatchNamespacesAsync(CancellationToken)`.
- [ ] B2 `KubernetesService`: implement using `WatchListNamespaceAsync`.
- [ ] B3 `SettingsModel`: add `NamespacesSettings.WatchRetryDelayMilliseconds` (default 5000).
- [ ] B4 `ClusterNodeViewModel`: start watch loop on connect; handle ADDED/DELETED; cancel on disconnect.
- [ ] B5 `ClusterNodeViewModel.Dispose()`: cancel/dispose `_namespaceCts`.

### Issue C — Port-Forward Logging
- [ ] C1 `HandleSingleConnectionAsync`: generate `connId`, record `startTime`, pass to copy methods.
- [ ] C2 `finally` block: log connection lifetime and close reason.
- [ ] C3 Add heartbeat task (every 5 min); use `Task.WhenAny` over all three tasks.
- [ ] C4 Copy loops: include `connId` and exception type name in `LogStreamClosed`.
- [ ] C5 New `[LoggerMessage]` methods: `LogConnectionLifetime`, `LogConnectionHeartbeat`.

### Tests
- [ ] T1 `NamespaceNodeViewModelTests`: DisplayName defaults + override; "all" node passes `""` namespace.
- [ ] T2 `ClusterNodeViewModelTests`: first namespace node is the "all" sentinel after connect.
- [ ] T3 `ClusterNodeViewModelTests`: watch ADDED appends; DELETED removes; sentinel not duplicated.
- [ ] T4 Port-forward: `LogConnectionLifetime` called; heartbeat fires.

### Build Verification
- [ ] V1 `dotnet build -warnaserror` → 0W/0E.
- [ ] V2 Core tests pass.
- [ ] V3 UI tests pass.

## Progress Log
_Updated as steps complete._
