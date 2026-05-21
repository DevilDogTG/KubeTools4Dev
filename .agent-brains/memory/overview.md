# Project Overview

KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# (.NET 10) for developers to interact with and manage Kubernetes resources.

## Project Structure
- **KubeTools4Dev**: The primary Avalonia application, housing the ViewModels, Views, and UI logic.
- **KubeTools4Dev.Core**: The core library containing models, configuration settings, and services (e.g., `KubernetesService`, `PortForwardService`, `SettingsService`).
- **KubeTools4Dev.Core.Tests**: xUnit + NSubstitute test project (66 tests). `InternalsVisibleTo` in Core csproj. Fakes live in `Fakes/`.
- **KubeTools4Dev.Tests**: xUnit + NSubstitute test project targeting the UI assembly. `InternalsVisibleTo` in KubeTools4Dev csproj. Tests use `protected virtual DispatchToUIAsync` hook to bypass Avalonia dispatcher.

## Developer Workflow (Modern Release Flow)
Trunk-based development off `main`.
- **Release workflow**: `sk-finish-feature` agent skill — runs preflight checks (clean tree, rebase from main, `-warnaserror` build, `dotnet test`) then creates/updates the PR via `gh`.
- **PR review**: `sk-pr-review` agent skill — reads `<!-- finish-feature-update -->` marker for base SHA, runs AI code review, posts `<!-- pr-review-findings -->` comment with 🔴/🟡/🔵 severity findings and markers. Detects re-run guard via `review-sha`.
- **Version Management**: Uses a hybrid model where both `version.json` (at root) and `src/KubeTools4Dev/KubeTools4Dev.csproj` are maintained.
- **Automated Releases**:
  - `release.yml`: Manual trigger for version bumping (patch/minor/major). Updates both version files and opens a release PR.
  - `tag.yml`: Automatically tags the merge commit with `vX.Y.Z` when a release PR is merged into `main`.
- **AI encoding**: All scripts set UTF-8 encoding to prevent emoji corruption on Thai Windows (CP874 OEM codepage).

## Testing Patterns
- **Subclass-and-Override**: Services using raw .NET socket/network primitives expose `protected internal virtual` methods (e.g., `AcceptSocketAsync`, `ConnectWebSocketAsync`). Tests subclass via `Fakes/TestablePortForwardService`. See ADR-001.
- **Shared-state isolation**: Test classes mutating `static` state implement `IDisposable` to restore original value.
- **Fakes folder**: `src/KubeTools4Dev.Core.Tests/Fakes/` — extend rather than re-implement.

## Current Version
- v1.2.7 (released 2026-05-21)

## Open PRs
_(none as of 2026-05-21)_

## Recently Merged
- PR #34 (`chore/archive-profile-composition`) — merged 2026-05-21. Agent-brains state files for profile-composition pipeline.
- PR #33 (`release/v1.2.7`) — merged 2026-05-21. Bump version to 1.2.7.
- PR #32 (`feature/deployments-page`) — merged 2026-05-21. Deployments page: list view, Rollout Restart, Edit (replica count + image tag). 66 tests.
- PR #31 (`bugfix/fix-port-forward-drops`) — merged 2026-05-20. Resilient listener loop, ReuseAddress, no idle timeout.

## Agent Team Framework (`.agent-brains/`)
- **Team**: `dev-team` — 5-role pipeline (Planner → Architect → Developer → Reviewer → QA)
- **Team config**: `teams/dev-team/team.md` v2.0 — `roles:` YAML block with `profiles:` lists per role
- **Workspace override**: `.agent-brains/teams/dev-team/team.md` — `profiles_append: [csharp-developer]` for developer role → effective set: `[base-developer, team-developer, csharp-developer]`
- **Skills**: `sk-team-start` v2.0 (N-profile loading), `sk-team-dispatch` v1.2 (Copilot CLI support, profile separators)
- **Dispatch**: Uses Copilot CLI `task(agent_type: "general-purpose", mode: "background")` per role

## ADRs
- `memory/adr-001-subclass-override-network-seams.md` — Subclass-and-Override for testing raw socket/network seams.
- `memory/adr-002-deployments-patch-strategy.md` — Patch strategy for Kubernetes deployment updates.
