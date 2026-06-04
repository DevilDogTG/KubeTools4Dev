
---
version: 1.1
profiles:
  - base-developer
  - csharp-developer
  - github-scm
  - kubernetes-devops
strict_override: false
---

# Workspace Instructions

## Overview
KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# (.NET 10) for managing Kubernetes resources.

## System Constraints
- **Filesystem-First Planning**: You are FORBIDDEN from executing code changes until a corresponding plan exists in `./.agent-brains/plan/`.
- **Distributed Planning**: High-level roadmap in `backlog.md`; detailed checklists in dedicated atomic plan files.
- **Automatic Handover**: Execute `project-handover` skill and provide Handover Memo at session end.

# Mandate: Filesystem-First Planning
You MUST NOT keep your plans in internal context only.
1. Read `\./.agent-brains/AGENT.md\`.
2. Write plan to `\./.agent-brains/plan/\` BEFORE writing code.
3. Update memory/overview.md at session end.

## Workspace Rules
<!-- begin:framework -->
<!-- Global and profile rules are active automatically. Add project-specific overrides here. -->
<!-- end:framework -->

<!-- [MAINTAINER ACTION] planning convention promoted to GLOBAL_AGENT.md §4.5.1 (2026-06-04) -->
<!-- vars.<NAME> gate for not-yet-ready GHA jobs documented in github-scm profile -->


## Git Workflow
- Always create a new branch (`feature/` or `bugfix/` prefix) before making any code changes.
- Use the `finish-feature` skill to run preflights and create/update PRs.
- Use the `pr-review` skill to run an AI code review and post findings on a PR.
- Never commit directly to `main`.

## Coding Standards
- Build must have **0 warnings, 0 errors** (`-warnaserror`).
- Use `[LoggerMessage]` source-generated methods for structured logging.
- **`[LoggerMessage]` with nullable `ILogger`**: When a class has an optional `ILogger<T>?` field, use a `static partial` method with the logger as the first parameter: `private static partial void LogXxx(ILogger logger, ...)`. Guard the call site: `if (_logger is not null) LogXxx(_logger, ...)`. See `ClusterNodeViewModel.LogMessages.cs` for the pattern.
- **VM event subscription on Singletons**: Any ViewModel that subscribes to an event on a `Singleton` service (e.g., `IClusterConnectionManager.ClusterStatusChanged`) MUST implement `IDisposable` and unsubscribe in `Dispose()`. Parent VMs that re-create child VMs (e.g., `ClusterTreeViewModel.RebuildTree` rebuilding `ClusterNodeViewModel` instances) MUST dispose the outgoing children before clearing/replacing the collection — otherwise the Singleton retains references and old VMs keep firing phantom `PropertyChanged` events on a dead UI tree.

## Testing
- Test project: `src/KubeTools4Dev.Core.Tests` (xUnit + NSubstitute, net10.0).
- Run: `dotnet test src/KubeTools4Dev.Core.Tests/KubeTools4Dev.Core.Tests.csproj`
- All tests must pass before finishing a feature.
- **Socket/network seams**: When testing services that use raw .NET socket or network primitives, prefer **Subclass-and-Override** (`protected internal virtual` methods) over introducing new interfaces. Avoids interface proliferation for infrastructure concerns that are inherently hard to mock.
- **Shared-state isolation**: xUnit test classes that mutate any `static` or shared state (e.g., `PortForwardService.ConnectionTimeout`) **must** implement `IDisposable` and restore the original value in `Dispose()` to be safe under parallel test execution.
- **NSubstitute + `IAsyncEnumerable`**: NSubstitute returns `null` by default for `IAsyncEnumerable<T>` return types. `await foreach` on null throws `NullReferenceException`. Always inject a configured enumerable via `FakeClusterConnectionManager.ServiceFactory` or an explicit `.Returns(...)` before exercising code that calls `await foreach` on a mocked method.
- **`FakeClusterConnectionManager.ServiceFactory`**: Set `_manager.ServiceFactory = (clusterId) => myMock` before calling `ConnectClusterAsync` to inject a pre-configured `IKubernetesService` substitute. Required for any test that configures `WatchNamespacesAsync` or other `IAsyncEnumerable`-returning members.
- **Fakes folder**: Reusable test subclasses and fakes live in `src/KubeTools4Dev.Core.Tests/Fakes/`. Extend existing fakes (e.g., `TestablePortForwardService`) rather than re-implementing mocks inline.

## PR Comment Contracts
- `finish-feature` skill posts `<!-- finish-feature-update -->` + `<!-- head-sha: SHA -->` on PRs.
- `pr-review` skill detects these markers → reviews diff → posts `<!-- pr-review-findings -->` + `<!-- review-status: approved|needs-work -->` + `<!-- review-sha: SHA -->`.

