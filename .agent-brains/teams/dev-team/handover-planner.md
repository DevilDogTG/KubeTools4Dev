# Handover: Planner → Architect

**Team:** dev-team  
**Date:** 2026-05-20  
**From:** planner  
**To:** architect

---

## What Was Done

- Read existing reference files: `KubernetesService.cs`, `IKubernetesService.cs`, `PodListViewModel.cs`, `PodViewModel.cs`, `ServiceListViewModel.cs`, `MainViewModel.cs`, `SidebarViewModel.cs`, `MainWindow.axaml`, `App.axaml.cs`.
- Identified all touch points required for the Deployments page feature.
- Produced `plan/deployments-page.md` with goal, scope (in/out), 15 acceptance criteria, 7 ordered task groups (18 checklist items), and a dependency table.
- Updated `plan/backlog.md` — `deployments-page.md` added under "In Progress".

---

## Decisions Made

- **Settings index shifts from 2 → 4** so Deployments can occupy index 3. This is a breaking change to `SidebarViewModel`; `SidebarViewModelTests` must be updated accordingly.
- **Four new `IKubernetesService` methods** — `GetDeploymentsAsync`, `WatchDeploymentsAsync`, `PatchDeploymentAsync`, `RestartDeploymentAsync` — rather than extending an existing method, to keep the interface consistent with the existing Pods/Services pattern.
- **Edit dialog is a separate Window/modal** (consistent with `PodDetailWindow` pattern) rather than an inline panel.
- **Only first container** image tag is editable — multi-container editing is out of scope per task description.
- **Tests in `KubeTools4Dev.Tests`** (the UI-assembly test project), not `KubeTools4Dev.Core.Tests`, since the new ViewModels live in the UI assembly — consistent with `PodDetailViewModelTests`.
- **Error surfacing** left as a UI-layer decision for the Architect/Developer — the plan mandates a user-visible message but does not prescribe the mechanism (the Architect should decide: status bar, overlay, dialog, or in-view TextBlock).

---

## Open Questions / Blockers

- None.

---

## Expected From Next Role (Architect)

- Architecture notes appended to the plan file (e.g., chosen patch strategy for `PatchDeploymentAsync`, error notification pattern decision, dialog type — `Window` vs `ContentDialog`).
- At least one ADR for any significant design decision (e.g., patch format choice, error notification mechanism).
- Interface definitions (method signatures with XML doc) for the four new `IKubernetesService` methods before Developer begins implementation.

---

## Key Files

- `.agent-brains/plan/deployments-page.md` — active plan with full checklist
- `.agent-brains/plan/backlog.md` — updated
- `src/KubeTools4Dev.Core/Services/Interfaces/IKubernetesService.cs` — needs 4 new signatures
- `src/KubeTools4Dev.Core/ViewModels/SidebarViewModel.cs` — needs index update
- `src/KubeTools4Dev/ViewModels/MainViewModel.cs` — needs `DeploymentList` property
- `src/KubeTools4Dev/Views/MainWindow.axaml` — needs sidebar entry + content panel
