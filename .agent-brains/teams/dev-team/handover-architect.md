# Handover: Architect → Developer

**Team:** dev-team  
**Date:** 2026-05-20  
**From:** architect  
**To:** developer

---

## What Was Done

- Read all key existing files: `IKubernetesService.cs`, `KubernetesService.cs`, `PodViewModel.cs`, `ServiceViewModel.cs`, `PodListViewModel.cs`, `ServiceListViewModel.cs`, `SidebarViewModel.cs`, `MainViewModel.cs`, `PodDetailWindow.axaml`, `App.axaml.cs`.
- Confirmed all existing patterns (ViewModel assembly placement, watch loop structure, DI registration, dialog type, error handling).
- Appended `## Architecture Notes` (AN-01 through AN-09) to `plan/deployments-page.md`.
- Wrote ADR-002 to `memory/adr-002-deployments-patch-strategy.md` covering the patch format decisions.
- All 18 checklist items in the plan are now fully specified with zero architectural ambiguity remaining.

---

## Decisions Made

| Decision | What | Why |
|---|---|---|
| **Patch strategy (`PatchDeploymentAsync`)** | Strategic Merge Patch (`application/strategic-merge-patch+json`) | JSON Merge Patch replaces the entire `containers` array, deleting sidecars; Strategic Merge Patch uses `name` as merge key — safe for multi-container deployments. See ADR-002. |
| **Rollout restart mechanism** | JSON Merge Patch: set `kubectl.kubernetes.io/restartedAt` annotation on `spec.template.metadata.annotations` | Annotations are a map; merge patch adds/updates one key without touching others. Functionally identical to `kubectl rollout restart`. |
| **Error notification** | Per-ViewModel `ErrorMessage` `[ObservableProperty]` string, bound to in-view `TextBlock` | No existing notification service in the codebase. Minimal, testable, consistent with `EditDeploymentDialogViewModel` spec in the plan. |
| **Edit dialog type** | `Window` with `ShowDialog(owner)` | Existing codebase only has `PodDetailWindow` (a `Window`). No `ContentDialog` infrastructure exists. Consistent and low cost. |
| **ViewModel assembly** | `DeploymentViewModel`, `DeploymentListViewModel`, `EditDeploymentDialogViewModel` all in `src/KubeTools4Dev/ViewModels/` (UI assembly) | Matches `PodViewModel`, `ServiceViewModel`, `PodListViewModel`, `ServiceListViewModel`. Only `SidebarViewModel` lives in Core (no Avalonia/k8s deps). |
| **Watch loop resilience** | `while (!token.IsCancellationRequested)` + `catch (OCE) { break; }` + `Task.Delay(5000)` on error | Identical to `PodListViewModel.WatchPodsAsync` and `ServiceListViewModel.WatchServicesAsync`. No additional resilience library. |

---

## Open Questions / Blockers

None. All decisions are made. All interface signatures are specified in AN-06.

---

## Expected From Next Role (Developer)

- Implement all 18 checklist items in `plan/deployments-page.md` **in order** (interface first, then Core ViewModels, then List VM, then Views, then Wiring, then Tests).
- Follow architecture notes **exactly** — do not deviate from patch types, ViewModel assembly placement, or error notification pattern without updating the ADR.
- **Do not use JSON Merge Patch for `PatchDeploymentAsync`** — it will delete sidecar containers. Use Strategic Merge Patch (AN-01, ADR-002).
- Pre-read the deployment before patching to obtain `containers[0].name` for the Strategic Merge Patch container entry.
- Clear `ErrorMessage` at the **start** of each command (before the `try`) to avoid stale error display (AN-09, Risk R-04).
- Implement `IDisposable` on `DeploymentListViewModel` (cancel CTS in `Dispose(bool)`) to avoid watch loop leak (AN-09, Risk R-03).
- Run `dotnet build -warnaserror` (task 7.1) and `dotnet test` (task 7.2) before handing over.

---

## Key Files

| File | Role |
|---|---|
| `plan/deployments-page.md` | Full plan + 18-item checklist + all architecture notes (AN-01 to AN-09) |
| `memory/adr-002-deployments-patch-strategy.md` | ADR for patch format decision |
| `src/KubeTools4Dev.Core/Services/Interfaces/IKubernetesService.cs` | Add 4 new method signatures (exact signatures in AN-06) |
| `src/KubeTools4Dev.Core/Services/KubernetesService.cs` | Implement 4 new methods (implementation notes in AN-06) |
| `src/KubeTools4Dev.Core/ViewModels/SidebarViewModel.cs` | Add `IsDeploymentsVisible` (index 3), shift `IsSettingsVisible` to index 4 |
| `src/KubeTools4Dev/ViewModels/MainViewModel.cs` | Add `DeploymentListViewModel _deploymentList`; inject in ctor; call `InitializeAsync()` in `Connect()`; call `Dispose()` in `Cleanup()` |
| `src/KubeTools4Dev/App.axaml.cs` | Register `DeploymentListViewModel` as `Transient` |
| `src/KubeTools4Dev/Views/MainWindow.axaml` | Add Deployments sidebar `ListBoxItem` at index 3; add `DeploymentListView` panel |
| `src/KubeTools4Dev/Views/PodListView.axaml` | Reference for DataGrid + filter bar structure to mirror |
| `src/KubeTools4Dev/Views/PodDetailWindow.axaml` | Reference for Window dialog pattern to follow |
