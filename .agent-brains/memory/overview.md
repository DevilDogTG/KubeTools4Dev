# Project Overview

KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# (.NET 10) for developers to interact with and manage Kubernetes resources.

## Project Structure
- **KubeTools4Dev**: The primary Avalonia application, housing the ViewModels, Views, and UI logic.
- **KubeTools4Dev.Core**: The core library containing models, configuration settings, and services (e.g., `KubernetesService`, `PortForwardService`, `SettingsService`).
- **KubeTools4Dev.Core.Tests**: xUnit + NSubstitute test project (85 tests across 8 files). `InternalsVisibleTo` in Core csproj. Fakes live in `Fakes/`.
- **KubeTools4Dev.Tests**: xUnit + NSubstitute test project targeting the UI assembly (69 tests across 6 files). `InternalsVisibleTo` in KubeTools4Dev csproj. Tests use `protected virtual DispatchToUIAsync` (or `DispatchToUI`) hook to bypass Avalonia dispatcher.

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
- **`[LoggerMessage]` for nullable loggers**: ViewModels with `ILogger<T>?` (optional) cannot use instance `[LoggerMessage]` directly. Pattern: `private static partial void LogXxx(ILogger logger, ...)` — static form takes ILogger as first param. Call site: `if (_logger is not null) LogXxx(_logger, ...)`. See `ClusterNodeViewModel.LogMessages.cs`.
- **NSubstitute + `IAsyncEnumerable` default** — NSubstitute returns `null` for `IAsyncEnumerable` — `await foreach` on null throws NRE. Tests must inject a configured enumerable via `ServiceFactory` on `FakeClusterConnectionManager`.
- **`FakeClusterConnectionManager.ServiceFactory`**: `Func<string, IKubernetesService>?` — when set, used instead of a plain `Substitute.For<IKubernetesService>()`. Required for any test that needs to pre-configure mock watch streams or service behavior before `ConnectClusterAsync`.
- **Shared-state isolation**: Test classes mutating `static` state implement `IDisposable` to restore original value.
- **FakeClusterConnectionManager**: Do NOT use `async Task` with `Task.Yield()` in fake constructors — this causes `AsyncTestSyncContext` to defer the continuation to the thread pool, making tests flaky. Use synchronous initialization instead.
- **xUnit `AsyncTestSyncContext` + captured `SynchronizationContext.Current`**: ViewModels in `Core` that capture `SynchronizationContext.Current` at construction time for UI dispatching will pick up xUnit's `AsyncTestSyncContext` inside `async Task` tests. `Post()` on that context defers work to the thread pool, causing tests to assert before the post runs. Mitigation: in the dispatcher (e.g., `OnClusterStatusChanged`), add a same-context shortcut — `if (_uiContext is null || SynchronizationContext.Current == _uiContext) applyInline(); else _uiContext.Post(...)`. This keeps tests synchronous while still dispatching properly in production. See `ClusterNodeViewModel.cs`.

## Current Version
- v1.3.3 (released 2026-05-29)

## Open PRs
_(none — PR #49 merged 2026-06-04)_

## Linux / WSL Packaging Notes
- **Cross-platform settings path**: `Environment.SpecialFolder.ApplicationData` already resolves correctly on Linux to `$XDG_CONFIG_HOME` (`~/.config`). No OS detection needed in `Program.cs` for the user settings location.
- **`OutputType=WinExe` is portable**: `dotnet publish -r linux-x64 --self-contained` on a `WinExe` csproj produces a valid Linux ELF binary. The `WinExe` subsystem flag is honored only by the Windows PE linker; on other targets it's ignored.
- **Velopack on Linux**: `Velopack.VelopackApp.Build().Run()` is a silent no-op when the app is not launched via a Velopack-managed install. Safe to leave in `Main()` for cross-platform builds — no `OperatingSystem.IsWindows()` gate needed.
- **`libicu` Depends string for self-contained .NET .deb**: must list every major version across supported distros. Current working alternative: `libicu76 | libicu74 | libicu72 | libicu71 | libicu70 | libicu67 | libicu66`. Mapping: Ubuntu 20.04→66, Debian 11→67, Ubuntu 22.04→70, Debian 12→72, Ubuntu 24.04→74, Debian 13→76. `apt install` fails with "unmet dependencies" if the running distro's version isn't in the list — caught in WSL Debian 13 smoke test when the original narrow list omitted 76.
- **Hand-rolled `dpkg-deb`** chosen over Velopack-Linux: keeps the Windows Velopack flow untouched and avoids coupling to Velopack's still-evolving Linux CLI. Layout: `/opt/kubetools4dev/` (publish output), `/usr/bin/kubetools4dev` (shell launcher, NOT a symlink, so `argv[0]` stays sane under WSLg), `/usr/share/applications/*.desktop`, `/usr/share/icons/hicolor/256x256/apps/*.png`. Script at `packaging/linux/build-deb.sh` with `dpkg-deb --root-owner-group`.
- **gh-pages empty-dir gotcha**: `git checkout --orphan gh-pages && git rm -rf . && mkdir -p dists/...` does NOT result in `dists/` being tracked — git ignores empty directories on commit. Workflows that write to nested paths on `gh-pages` must `mkdir -p` before redirecting, or seed `.gitkeep` files. Caught in PR #49 verification on 2026-06-04 before the first apt-repo CI run.

## Recently Merged
- PR #49 (`feature/linux-deb-installer`) — rebase-merged 2026-06-04 (merge commit `41f8fb4`). Linux `.deb` installer + GPG-signed apt repo on GitHub Pages: Phase A (publish-linux job + dpkg-deb assembler) + Phase B scaffolding (publish-apt-repo job gated on `vars.APT_REPO_ENABLED`, maintainer guide). Maintainer completed Part 1 of the guide same day; dists-mkdir CI fix landed in the same PR. Next: tag `v1.3.4-rc1` to exercise the apt-repo pipeline end-to-end (B7).
- PR #46 (`feature/profile-supervisor`) — merged 2026-05-29. Profile port-forward supervisor: ▶ Forward on a profile enters supervised mode (auto-retry dropped forwards with bounded exponential backoff, max 10 attempts). Tri-state Forward/Stop/Resume toggle. Banner notifications with theme-aware colors. Exhaustion stops the whole profile + error banner. Manual one-off port-forwards stay unsupervised. 154 tests (85 Core + 69 UI), 0W/0E.
- PR #45 (`release/v1.3.2`) — merged 2026-05-27. Bump version to 1.3.2.
- PR #44 (`feature/port-forward-profiles`) — merged 2026-05-27. Port-forward profile system: replaces "Forward All / Stop All" with named per-cluster profiles. Users create profiles specifying which services to forward and activate a full profile with one click. Includes UX fixes and removes deprecated Exclude feature. 128 tests (71 Core + 57 UI), 0W/0E.
- PR #43 (`release/v1.3.1`) — merged 2026-05-26. Bump version to 1.3.1.
- PR #42 (`bugfix/fix-ci-delete-namespace-watch-test`) — merged 2026-05-26. Resolves CI flaky test `WatchNamespacesAsync_DeletedEvent_RemovesExistingNode`.
- PR #41 (`release/v1.3.0`) — merged 2026-05-25. Bump version to 1.3.0.
- PR #40 (`feature/namespace-all-dynamic-pf-logging`) — merged 2026-05-25. All-ns sentinel node, live namespace watch (WatchNamespacesAsync), port-forward lifecycle logging (connId, duration, heartbeat).
- PR #38 (`feature/ui-style-consistency`) — merged 2026-05-25. UI style consistency: `RocketLaunch` Deployment icon, compact input/button sizing across all pages, ±-stepper controls, sidebar header removed, namespace children collapse by default, `Math.Clamp` stepper unification.
- PR #36 (`feature/multi-cluster-tree-nav`) — merged 2026-05-22. Multi-cluster support: per-cluster `ClusterConnectionManager`, VS Code-style nested-`ItemsControl` sidebar (replaces TreeView), `IDisposable` cascade from `ClusterTreeViewModel`, captured-`SynchronizationContext` UI dispatch.
- PR #34 (`chore/archive-profile-composition`) — merged 2026-05-21. Agent-brains state files for profile-composition pipeline.
- PR #32 (`feature/deployments-page`) — merged 2026-05-21. Deployments page: list view, Rollout Restart, Edit (replica count + image tag).
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
