# Project Overview

KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# (.NET 10) for developers to interact with and manage Kubernetes resources.

## Project Structure
- **KubeTools4Dev**: The primary Avalonia application, housing the ViewModels, Views, and UI logic.
- **KubeTools4Dev.Core**: The core library containing models, configuration settings, and services (e.g., `KubernetesService`, `PortForwardService`, `SettingsService`).
- **KubeTools4Dev.Core.Tests**: xUnit + NSubstitute test project (35 tests: `SettingsModelTests`, `SettingsServiceTests`, `PortForwardServiceTests`, `SidebarViewModelTests`). `InternalsVisibleTo` in Core csproj. Fakes live in `Fakes/` (e.g., `TestablePortForwardService`).
- **KubeTools4Dev.Tests**: xUnit + NSubstitute test project targeting the UI assembly (31 tests: `PodDetailViewModelTests`, `DeploymentViewModelTests`, `DeploymentListViewModelTests`). `InternalsVisibleTo` in KubeTools4Dev csproj.

## Developer Workflow (Modern Release Flow)
Trunk-based development off `main`.
- **Release workflow**: `sk-finish-feature` agent skill — runs preflight checks (clean tree, rebase from main, `-warnaserror` build, `dotnet test`) then creates/updates the PR via `gh`.
- **PR review**: `sk-pr-review` agent skill — reads `<!-- finish-feature-update -->` marker for base SHA, runs AI code review, posts `<!-- pr-review-findings -->` comment with 🔴/🟡/🔵 severity findings and markers. Detects re-run guard via `review-sha`.
- **Version Management**: Uses a hybrid model where both `version.json` (at root) and `src/KubeTools4Dev/KubeTools4Dev.csproj` are maintained.
- **Automated Releases**:
  - `release.yml`: Manual trigger for version bumping (patch/minor/major). Updates both version files and opens a release PR.
  - `tag.yml`: Automatically tags the merge commit with `vX.Y.Z` when a release PR is merged into `main`.
- **AI encoding**: All scripts set UTF-8 encoding to prevent emoji corruption on Thai Windows (CP874 OEM codepage).
- **Release**: Phase 3 (automated release pipeline) is deferred.

## Testing Patterns
- **Subclass-and-Override**: Services using raw .NET socket/network primitives expose `protected internal virtual` methods (e.g., `AcceptSocketAsync`, `ConnectWebSocketAsync`). Tests subclass via `Fakes/TestablePortForwardService`. See ADR-001.
- **Shared-state isolation**: Test classes mutating `static` state implement `IDisposable` to restore original value.
- **Fakes folder**: `src/KubeTools4Dev.Core.Tests/Fakes/` — extend rather than re-implement.

## Current Open PRs
- PR #32 (`feature/deployments-page`) — ✅ **Approved** (review-sha: `a2d4437`). Adds Deployments page with Rollout Restart and Edit actions. 66/66 tests. Ready to merge.
- PR #31 (`bugfix/fix-port-forward-drops`) — ✅ Approved. Fixes silent port-forward drops. 35 Core tests. README updated. Ready to merge.

## Recently Merged
- PR #28 (`feature/pod-detail-popup-window`) — merged 2026-05-19. Replaced split panel with independent non-modal popup windows for logs and describe. Added `KubeTools4Dev.Tests` project (10 tests). Total test count was 42.
- `chore/session-init-rules` — pushed 2026-05-19. Standardized session initialization behavior in `AGENT.md` and `GEMINI.md`.

## ADRs
- `memory/adr-001-subclass-override-network-seams.md` — Subclass-and-Override chosen over interface extraction for testing raw socket/network seams in `PortForwardService`.
- `memory/adr-002-deployments-patch-strategy.md` — Strategic Merge Patch for `PatchDeploymentAsync`; JSON Merge Patch for `RestartDeploymentAsync`.

## Known Codebase-Wide Deviations (tracked, not blocking)
- **Primary constructors**: `PodListViewModel`, `ServiceListViewModel`, `DeploymentListViewModel`, `DeploymentViewModel`, `EditDeploymentDialogViewModel` use traditional constructors. `KubernetesService` uses primary constructor. No consistent standard enforced yet.
- **`[LoggerMessage]`**: All ViewModels and services use direct `_logger.LogXxx(...)` calls. No source-generated log methods anywhere in the codebase.
