# Project Overview

KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# (.NET 10) for developers to interact with and manage Kubernetes resources.

## Project Structure
- **KubeTools4Dev**: The primary Avalonia application, housing the ViewModels, Views, and UI logic.
- **KubeTools4Dev.Core**: The core library containing models, configuration settings, and services (e.g., `KubernetesService`, `PortForwardService`, `SettingsService`).
- **KubeTools4Dev.Core.Tests**: xUnit + NSubstitute test project (23 tests: `SettingsModelTests`, `SettingsServiceTests`, `PortForwardServiceTests`). Added `InternalsVisibleTo` in Core csproj.

## Developer Workflow (Modern Release Flow)
Trunk-based development off `main`.
- **Feature script**: `scripts/finish-feature.ps1` — runs preflight checks (clean tree, rebase from main, `-warnaserror` build, `dotnet test`) then creates/updates the PR via `gh`.
- **PR creation**: Generates AI title (conventional-commit style) + rich body (Overview / What's Changed / Files Changed / Testing) using full commit log + `git diff --stat` + truncated diff (6000 char cap).
- **PR update (re-run)**: Posts a structured `<!-- finish-feature-update -->` comment with preflight results, new commits, AI summary, and `<!-- head-sha: SHA -->` marker. Does NOT overwrite the PR body.
- **PR review script**: `scripts/pr-review.ps1` — reads `<!-- finish-feature-update -->` marker for base SHA, runs AI code review, posts `<!-- pr-review-findings -->` comment with 🔴/🟡/🔵 severity findings and `<!-- review-status: approved|needs-work -->` + `<!-- review-sha: SHA -->` markers. Detects developer replies for re-review loop.
- **AI encoding**: All scripts set `chcp 65001` + `$OutputEncoding` + `[Console]::OutputEncoding` to UTF-8 to prevent emoji corruption on Thai Windows (CP874 OEM codepage).
- **Release**: Phase 3 (automated release pipeline) is deferred.

## Current Open PRs
- PR #18 (`feature/test-coverage`) — test coverage + finish-feature script enhancements. Awaiting merge.
- `feature/pr-review-script` — new `scripts/pr-review.ps1` AI code review script. Awaiting PR.
