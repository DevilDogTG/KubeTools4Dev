# ADR-001: Subclass-and-Override for Network Seam Testing

**Date:** 2026-05-20
**Status:** Accepted
**Branch:** `bugfix/fix-port-forward-drops`

## Context

`PortForwardService` uses raw .NET `Socket` and Kubernetes WebSocket extension methods (`WebSocketNamespacedPodPortForwardAsync`) inside private methods. These cannot be mocked by NSubstitute because:
- `Socket` is a concrete sealed class.
- `WebSocketNamespacedPodPortForwardAsync` is a static extension method on `IKubernetes`.

When fixing silent port-forward drops, we needed to write edge-case unit tests for two behaviours:
1. That the listener loop continues after a client `ConnectionReset` `SocketException`.
2. That the WebSocket connection honours the `ConnectionTimeout` and exits gracefully when it hangs.

## Options Considered

### Option A — `ITcpListenerFactory` interface
Introduce an interface wrapping `Socket.AcceptAsync` and inject it into `PortForwardService`.
- ✅ Clean dependency inversion
- ❌ Adds infrastructure interface for a single internal call; pollutes the public API surface

### Option B — `IKubernetesWebSocketFactory` interface
Introduce a wrapper around the Kubernetes extension method.
- ✅ Clean
- ❌ Duplicates `IKubernetesService` responsibilities; adds another indirection layer

### Option C — **Subclass-and-Override** (chosen)
Extract the infrastructure calls into `protected internal virtual` methods (`AcceptSocketAsync`, `ConnectWebSocketAsync`, `GetPodNameAsync`). Tests subclass `PortForwardService` via `TestablePortForwardService` in `Fakes/` and override these hooks with mock delegates.
- ✅ No new interfaces
- ✅ Production path unchanged (virtual dispatch with single override)
- ✅ Test subclass lives entirely in the test project — zero production surface change
- ❌ `protected internal virtual` slightly widens visibility; acceptable for a sealed DI-registered service

## Decision

**Option C — Subclass-and-Override.**

For services that depend on raw .NET runtime infrastructure (sockets, streams, OS handles), Subclass-and-Override is preferred over interface extraction. The `TestablePortForwardService` class in `src/KubeTools4Dev.Core.Tests/Fakes/` serves as the canonical pattern for future tests in this service.

## Consequences

- `AcceptSocketAsync`, `ConnectWebSocketAsync`, and `GetPodNameAsync` are `protected internal virtual` in `PortForwardService`.
- `AcceptAndForwardConnectionsAsync` and `HandleSingleConnectionAsync` are `internal` (accessible to the test project via `InternalsVisibleTo`).
- `ConnectionTimeout` is `internal static` (non-readonly) to allow test-speed overrides; test classes **must** implement `IDisposable` to restore it.
- Future socket/network edge-case tests **must** extend `TestablePortForwardService` rather than re-implementing mock logic inline.
