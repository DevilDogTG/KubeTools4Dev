# Plan: Deployments Page

**Status:** active  
**Created:** 2026-05-20  
**Author role:** planner  
**Team:** dev-team

---

## Goal

Add a Deployments page to KubeTools4Dev that lists all Kubernetes Deployments in the connected cluster, displays per-deployment replica and image tag information, and exposes two per-row actions — **Rollout Restart** and **Edit** (change replica count or image tag) — both applied via the Kubernetes API. The page must be reachable from the left sidebar navigation alongside Pods and Services.

---

## Scope

### In Scope
- New sidebar entry: **Deployments** (index 3), inserted between Services (1) and Settings (3 → 4).
- New `DeploymentViewModel` — wraps a `V1Deployment`; exposes: name, namespace, desired replicas, ready replicas, available replicas, first-container image tag.
- New `DeploymentListViewModel` — manages the observable collection, filtering, live watch, and commands (RolloutRestart, Edit).
- New `DeploymentListView.axaml` — DataGrid-based view matching the visual style of `PodListView.axaml` / `ServiceListView.axaml`.
- New `EditDeploymentDialog.axaml` + `EditDeploymentDialogViewModel` — dialog for editing replica count and image tag; validates replica count ≥ 0.
- Three new methods on `IKubernetesService` and `KubernetesService`:
  - `GetDeploymentsAsync(string namespaceName)` → `IEnumerable<V1Deployment>`
  - `WatchDeploymentsAsync(string namespaceName, CancellationToken)` → `IAsyncEnumerable<(WatchEventType, V1Deployment)>`
  - `PatchDeploymentAsync(string namespaceName, string deploymentName, int replicas, string imageTag)` → `Task`
  - `RestartDeploymentAsync(string namespaceName, string deploymentName)` → `Task`
- `SidebarViewModel` updated: new `IsDeploymentsVisible` property, index shift for Settings (now index 4), and appropriate `OnSelectedNavIndexChanged` notification.
- `MainViewModel` updated: new `DeploymentList` property, `InitializeAsync()` call wired in `Connect()`.
- `App.axaml.cs` DI registration for `DeploymentListViewModel`.
- `MainWindow.axaml` updated: new sidebar `ListBoxItem` for Deployments; new `Panel` for `DeploymentListView`.
- Error surfacing: all async command failures must show a user-visible error message in the UI (consistent with any existing error notification pattern, or a simple in-view error TextBlock if none exists).
- Unit tests: `DeploymentListViewModelTests` and `DeploymentViewModelTests` in `KubeTools4Dev.Tests`; `SidebarViewModelTests` updated to cover new index.
- Build must pass at `0 warnings / 0 errors` with `-warnaserror`.

### Out of Scope
- Deployment creation or deletion.
- Multi-container image editing (only first container).
- Rollout history or rollback.
- Deployment YAML full-edit view.
- Port-forwarding for deployments.
- Namespace selector / filtering by namespace (beyond what already exists in the filter textbox).

---

## Acceptance Criteria

1. The left sidebar contains a **Deployments** entry that, when selected, shows `DeploymentListView` and hides Pods/Services/Settings views.
2. On successful connection, `DeploymentListViewModel.InitializeAsync()` is called and populates the list.
3. The DataGrid displays at minimum: **Namespace**, **Name**, **Desired**, **Ready**, **Available**, **Image Tag** columns.
4. The list filters by text matching Name or Namespace (case-insensitive), consistent with existing filter textbox pattern.
5. The live-watch loop (`WatchDeploymentsAsync`) updates the list incrementally without full reload.
6. The **Rollout Restart** button patches the deployment's `spec.template.metadata.annotations` with `kubectl.kubernetes.io/restartedAt = <now>` via the Kubernetes API; a success or error notification is shown.
7. The **Edit** button opens `EditDeploymentDialog`; the dialog pre-populates current replica count and image tag; both fields are editable.
8. Submitting the Edit dialog with a valid replica count (integer ≥ 0) and a non-empty image tag calls `PatchDeploymentAsync` and closes the dialog; the list row updates on the next watch event or immediate refresh.
9. Submitting the Edit dialog with an invalid replica count (non-integer or negative) shows a validation error and does not call the API.
10. Any Kubernetes API error in RolloutRestart or Edit is caught and displayed as a user-visible error message (not silently swallowed).
11. `IKubernetesService` declares `GetDeploymentsAsync`, `WatchDeploymentsAsync`, `PatchDeploymentAsync`, and `RestartDeploymentAsync`; `KubernetesService` implements all four.
12. `SidebarViewModel` emits `IsDeploymentsVisible = true` when `SelectedNavIndex == 3`; Settings is now index 4.
13. All existing tests continue to pass.
14. At least one xUnit test class covers `DeploymentViewModel` property mapping and `DeploymentListViewModel` command logic using NSubstitute fakes.
15. `dotnet build -warnaserror` produces 0 warnings and 0 errors.

---

## Task Checklist

> The Architect assigns each item to Developer(s). Items are ordered by dependency.

### 1 — Interface & Service Layer
- [x] **1.1** Add `GetDeploymentsAsync`, `WatchDeploymentsAsync`, `PatchDeploymentAsync`, and `RestartDeploymentAsync` signatures to `IKubernetesService`.
- [x] **1.2** Implement all four methods in `KubernetesService`, following the existing `GetServicesAsync` / `WatchServicesAsync` patterns. `PatchDeploymentAsync` must use a JSON Strategic Merge Patch. `RestartDeploymentAsync` must annotate `spec.template.metadata.annotations` with the restart timestamp.

### 2 — Core ViewModels
- [x] **2.1** Create `DeploymentViewModel` in `src/KubeTools4Dev/ViewModels/` wrapping `V1Deployment`, exposing observable properties: `Name`, `Namespace`, `DesiredReplicas`, `ReadyReplicas`, `AvailableReplicas`, `ImageTag`. Include an `Update(V1Deployment)` method.
- [x] **2.2** Update `SidebarViewModel` in `src/KubeTools4Dev.Core/ViewModels/SidebarViewModel.cs`: add `IsDeploymentsVisible` (index 2), shift Settings to index 3, fire property change notifications for the new property in `OnSelectedNavIndexChanged`.
- [x] **2.3** Update `SidebarViewModelTests` to cover `IsDeploymentsVisible` and the shifted Settings index.

### 3 — List ViewModel & Edit Dialog ViewModel
- [x] **3.1** Create `DeploymentListViewModel` in `src/KubeTools4Dev/ViewModels/` following `PodListViewModel` pattern: `_allDeployments` list, `Deployments` `ObservableCollection`, `FilterText`, `IsLoading`, `LastRefreshTime`, `DispatcherTimer`, `CancellationTokenSource`, `InitializeAsync()`, `WatchDeploymentsAsync(CancellationToken)`, `UpdateFilteredList()`, `IDisposable`.
- [x] **3.2** Add `[RelayCommand] RolloutRestartAsync(DeploymentViewModel)` to `DeploymentListViewModel`: calls `IKubernetesService.RestartDeploymentAsync`; on failure surfaces error.
- [x] **3.3** Add `[RelayCommand] EditDeploymentAsync(DeploymentViewModel)` to `DeploymentListViewModel`: opens `EditDeploymentDialog`; on dialog confirmation calls `IKubernetesService.PatchDeploymentAsync`; on failure surfaces error.
- [x] **3.4** Create `EditDeploymentDialogViewModel` with properties: `DeploymentName` (read-only display), `Replicas` (int, bindable), `ImageTag` (string, bindable), `ErrorMessage` (string, bindable), `[RelayCommand] Confirm`, `[RelayCommand] Cancel`. Confirm validates Replicas ≥ 0 and ImageTag non-empty.

### 4 — Views
- [x] **4.1** Create `DeploymentListView.axaml` in `src/KubeTools4Dev/Views/` mirroring `PodListView.axaml` structure (filter bar, DataGrid). DataGrid columns: Namespace, Name, Desired, Ready, Available, Image Tag, Actions (Rollout Restart button, Edit button).
- [x] **4.2** Create `EditDeploymentDialog.axaml` in `src/KubeTools4Dev/Views/` — a `Window` (or modal dialog) with: display label for deployment name, `NumericUpDown` for replica count, `TextBox` for image tag, error message `TextBlock`, Confirm and Cancel buttons.
- [x] **4.3** Update `MainWindow.axaml`: insert **Deployments** `ListBoxItem` (with `AlphaD` MaterialIcon) at index 2 in the sidebar `ListBox`; insert a `Panel` bound to `Sidebar.IsDeploymentsVisible` containing `<views:DeploymentListView DataContext="{Binding DeploymentList}"/>`.

### 5 — Wiring
- [x] **5.1** Update `MainViewModel`: add `[ObservableProperty] DeploymentListViewModel _deploymentList`; inject `DeploymentListViewModel` in constructor; call `await DeploymentList.InitializeAsync()` in `Connect()`; call `DeploymentList?.Dispose()` in `Cleanup()`.
- [x] **5.2** Register `DeploymentListViewModel` as `Transient` in `App.axaml.cs` `ConfigureServices`.

### 6 — Tests
- [x] **6.1** Create `DeploymentViewModelTests` in `src/KubeTools4Dev.Tests/` covering: property mapping from `V1Deployment`, `Update()` re-maps properties, image tag reads `spec.template.spec.containers[0].image`, null Containers returns empty string.
- [x] **6.2** Create `DeploymentListViewModelTests` covering: `RolloutRestartCommand` calls `RestartDeploymentAsync` on the service; `RolloutRestartCommand` surfaces error when service throws; `EditDeploymentCommand` calls `PatchDeploymentAsync` with correct args on confirm; `EditDeploymentDialogViewModel` validate rejects negative replicas and empty image tag.

### 7 — Build Verification
- [x] **7.1** Run `dotnet build -warnaserror` from solution root; fix all warnings/errors to reach 0W/0E.
- [x] **7.2** Run `dotnet test`; all tests (existing + new) must pass.

---

## Dependencies (Existing Classes to Extend)

| Artifact | Location | Change Required |
|---|---|---|
| `IKubernetesService` | `src/KubeTools4Dev.Core/Services/Interfaces/IKubernetesService.cs` | Add 4 new method signatures |
| `KubernetesService` | `src/KubeTools4Dev.Core/Services/KubernetesService.cs` | Implement 4 new methods |
| `SidebarViewModel` | `src/KubeTools4Dev.Core/ViewModels/SidebarViewModel.cs` | Add `IsDeploymentsVisible`, shift Settings index |
| `SidebarViewModelTests` | `src/KubeTools4Dev.Core.Tests/ViewModels/SidebarViewModelTests.cs` | Cover new index |
| `MainViewModel` | `src/KubeTools4Dev/ViewModels/MainViewModel.cs` | Add `DeploymentList` property + init call |
| `MainWindow.axaml` | `src/KubeTools4Dev/Views/MainWindow.axaml` | Sidebar entry + content panel |
| `App.axaml.cs` | `src/KubeTools4Dev/App.axaml.cs` | DI registration |

---

## Open Questions

_(none — all requirements derived from the task description)_

---

## Deviations

> **Author role:** developer  
> **Date:** 2026-05-20

| ID | Plan Item | Deviation | Reason |
|---|---|---|---|
| D-01 | 2.2 / 4.3 | `IsDeploymentsVisible` mapped to index **2**, `IsSettingsVisible` shifted to index **3** (not index 3/4 as the task prompt stated) | The plan document itself lists Deployments between Services (1) and Settings (2→3). Confirmed correct by reading the existing sidebar: Pods=0, Services=1, Deployments=2, Settings=3. The prompt text contained an off-by-one that contradicted the plan. Indices are sequential 0-based. |
| D-02 | 3.1 | `RefreshIntervalSeconds` property omitted | Deployments have no metrics polling interval — the watch loop provides live updates. Adding a tunable timer interval would require `ISettingsService` coupling not in scope. The 30-second `DispatcherTimer` tick simply calls `UpdateRefreshTime()`. |
| D-03 | 3.1 | `DispatcherTimer` created in `InitializeAsync()`, not in constructor | Enables test-time instantiation of `DeploymentListViewModel` without Avalonia platform initialization. |

---

## Architecture Notes

> **Author role:** architect  
> **Date:** 2026-05-20  
> **ADR:** [`adr-002-deployments-patch-strategy.md`](../memory/adr-002-deployments-patch-strategy.md)

---

### AN-01: Patch Strategy for `PatchDeploymentAsync`

**Decision:** Use **Strategic Merge Patch** (`application/strategic-merge-patch+json`) via `V1Patch`.

**Rationale:** The patch must update both `spec.replicas` (a scalar) and `spec.template.spec.containers[0].image` (inside an array). JSON Merge Patch replaces arrays wholesale — a patch containing only the first container would silently delete any sidecar containers. Strategic Merge Patch uses the `name` field as the merge key for the `containers` list (as declared in the Kubernetes API OpenAPI schema), so only the named container is updated. This is the same mechanism used by `kubectl set image` and `kubectl patch`.

JSON Patch (RFC 6902) was rejected because it is path-index-based (`/spec/template/spec/containers/0/image`) and would break silently if a controller ever reorders containers. Strategic Merge Patch by `name` is robust against container reordering.

**Patch body shape (Strategic Merge Patch):**

```json
{
  "spec": {
    "replicas": <int>,
    "template": {
      "spec": {
        "containers": [
          {
            "name": "<first-container-name>",
            "image": "<imageTag>"
          }
        ]
      }
    }
  }
}
```

The `name` field **must** be included in the container entry so the Kubernetes server can locate the correct container via the strategic merge key. `KubernetesService.PatchDeploymentAsync` must first read `spec.template.spec.containers[0].name` to populate this field, then issue the patch.

**API call:**  
`await Client.AppsV1.PatchNamespacedDeploymentAsync(new V1Patch(body, V1Patch.PatchType.StrategicMergePatch), deploymentName, namespaceName)`

---

### AN-02: Rollout Restart Mechanism for `RestartDeploymentAsync`

**Decision:** JSON Merge Patch (`application/merge-patch+json`) on `spec.template.metadata.annotations` with the annotation `kubectl.kubernetes.io/restartedAt` set to the current UTC timestamp in ISO 8601 format.

**Rationale:** Annotations are a `map<string,string>`, so JSON Merge Patch (which merges maps key-by-key) correctly adds/updates only the target annotation without touching others. This is exactly what `kubectl rollout restart` does internally — no server-side rollout mechanism needs to be invoked directly.

**Patch body shape:**

```json
{
  "spec": {
    "template": {
      "metadata": {
        "annotations": {
          "kubectl.kubernetes.io/restartedAt": "2026-05-20T10:00:00Z"
        }
      }
    }
  }
}
```

**Timestamp format:** `DateTime.UtcNow.ToString("o")` (round-trip / ISO 8601 with timezone).

**API call:**  
`await Client.AppsV1.PatchNamespacedDeploymentAsync(new V1Patch(body, V1Patch.PatchType.MergePatch), deploymentName, namespaceName)`

---

### AN-03: Error Notification Pattern

**Decision:** Per-ViewModel `ErrorMessage` observable string property bound to an in-view `TextBlock`. No shared notification service is introduced.

**Rationale:** The existing codebase has no `INotificationService`, toast mechanism, or `MessageBox` wrapper. Adding one would be out of scope for this feature. The `EditDeploymentDialogViewModel` already mandates an `ErrorMessage` property (plan item 3.4). Extending the same pattern to `DeploymentListViewModel` for command failures is minimal, consistent, and testable without Avalonia platform initialization.

**Implementation:**

- `DeploymentListViewModel` exposes `[ObservableProperty] private string _errorMessage = string.Empty;`
- `EditDeploymentDialogViewModel` exposes `[ObservableProperty] private string _errorMessage = string.Empty;`
- Both views bind a `TextBlock` to `ErrorMessage` with `IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"`.
- On any `Exception` caught in `RolloutRestartCommand` or `EditDeploymentCommand`, set `ErrorMessage = ex.Message` (and log via `_logger.LogError`). On success, clear to `string.Empty`.
- `ErrorMessage` is NOT reset between separate command invocations automatically; it clears only on a new successful operation or on explicit user re-invocation.

**Risk:** Long API error messages may overflow the TextBlock. Mitigation: wrap the TextBlock with `TextWrapping="Wrap"` and a fixed `MaxHeight`.

---

### AN-04: Edit Dialog Type

**Decision:** `Window` with `ShowDialog(owner)`.

**Rationale:** The codebase's only existing modal is `PodDetailWindow`, which is a plain Avalonia `Window`. Introducing `ContentDialog` would require `DialogHost.Avalonia` or Avalonia's built-in `ContentDialog` (available from Avalonia 11.x but not yet used anywhere in the project), adding an untested dependency path. A `Window` with `ShowDialog` provides native modal behavior on all platforms, and the Developer can follow the existing `OpenPodDetailWindow` pattern directly (resolve VM from DI or factory, construct `Window`, call `ShowDialog`).

**Tradeoffs vs ContentDialog:**

| Criterion | Window + ShowDialog | ContentDialog |
|---|---|---|
| Existing pattern | ✅ Used for PodDetailWindow | ❌ Not used anywhere |
| Platform behavior | ✅ Native modal | ⚠️ Rendered in-app overlay |
| Task-returning API | ✅ `await window.ShowDialog<bool?>(owner)` | ✅ Returns result |
| Test isolation | ✅ VM is testable without window | ✅ Same |
| Implementation cost | ✅ Low (known pattern) | ⚠️ Higher (new infra) |

`EditDeploymentDialog` will expose a `DialogResult` property (`bool?`) set by Confirm/Cancel commands, and `ShowDialog` will return after the window closes.

**Pattern to follow:** `DeploymentListViewModel.EditDeploymentCommand` creates/resolves `EditDeploymentDialogViewModel`, constructs `new EditDeploymentDialog(vm)`, calls `await dialog.ShowDialog<bool?>(ownerWindow)`, and if result is `true`, calls `PatchDeploymentAsync`.

---

### AN-05: ViewModel Assembly Placement

**Decision:** Both `DeploymentViewModel` and `DeploymentListViewModel` live in **`src/KubeTools4Dev/ViewModels/`** (UI assembly, `KubeTools4Dev` project).

**Rationale:** Confirmed by reading the existing code:
- `PodViewModel` → `src/KubeTools4Dev/ViewModels/` (UI assembly)
- `ServiceViewModel` → `src/KubeTools4Dev/ViewModels/` (UI assembly)
- `PodListViewModel` → `src/KubeTools4Dev/ViewModels/` (UI assembly)
- `ServiceListViewModel` → `src/KubeTools4Dev/ViewModels/` (UI assembly)
- Only `SidebarViewModel` is in `src/KubeTools4Dev.Core/ViewModels/` because it has zero Avalonia/k8s/Serilog dependencies and is used in the Core test project.

`DeploymentViewModel` wraps `V1Deployment` (a k8s SDK type) and `DeploymentListViewModel` depends on Avalonia threading (`Dispatcher.UIThread`) — both belong in the UI assembly.

`EditDeploymentDialogViewModel` also lives in the UI assembly (`src/KubeTools4Dev/ViewModels/`).

---

### AN-06: IKubernetesService Interface Method Signatures

The following four methods must be added to `IKubernetesService` and implemented in `KubernetesService`. All follow the existing Pods/Services naming and signature conventions exactly.

```csharp
/// <summary>
/// Gets the deployments asynchronous.
/// </summary>
/// <param name="namespaceName">Name of the namespace. Pass empty string or "*" for all namespaces.</param>
/// <returns>A list of deployments in the specified namespace.</returns>
Task<IEnumerable<V1Deployment>> GetDeploymentsAsync(string namespaceName = "default");

/// <summary>
/// Watches the deployments asynchronous.
/// </summary>
/// <param name="namespaceName">Name of the namespace. Pass empty string or "*" for all namespaces.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>An async enumerable of watch events containing the event type and the affected deployment.</returns>
IAsyncEnumerable<(WatchEventType Type, V1Deployment Item)> WatchDeploymentsAsync(
    string namespaceName = "default",
    CancellationToken cancellationToken = default);

/// <summary>
/// Patches the deployment asynchronous. Updates replica count and/or image tag on the first container
/// using a Strategic Merge Patch so that other containers are not affected.
/// </summary>
/// <param name="namespaceName">Name of the namespace.</param>
/// <param name="deploymentName">Name of the deployment.</param>
/// <param name="replicas">The desired replica count (must be ≥ 0).</param>
/// <param name="imageTag">The full image tag to apply to the first container (e.g. "nginx:1.25").</param>
/// <returns>A task representing the asynchronous patch operation.</returns>
Task PatchDeploymentAsync(string namespaceName, string deploymentName, int replicas, string imageTag);

/// <summary>
/// Restarts the deployment asynchronous. Applies a JSON Merge Patch setting the
/// <c>kubectl.kubernetes.io/restartedAt</c> annotation on the pod template metadata,
/// which causes a rolling restart equivalent to <c>kubectl rollout restart</c>.
/// </summary>
/// <param name="namespaceName">Name of the namespace.</param>
/// <param name="deploymentName">Name of the deployment.</param>
/// <returns>A task representing the asynchronous restart operation.</returns>
Task RestartDeploymentAsync(string namespaceName, string deploymentName);
```

**Implementation notes for `KubernetesService`:**

- `GetDeploymentsAsync`: branch on `IsAllNamespaces(namespaceName)`, call `Client.AppsV1.ListDeploymentForAllNamespacesAsync()` or `Client.AppsV1.ListNamespacedDeploymentAsync(namespaceName)`, return `.Items`.
- `WatchDeploymentsAsync`: same branch, call `Client.AppsV1.WatchListDeploymentForAllNamespacesAsync` or `Client.AppsV1.WatchListNamespacedDeploymentAsync`. Use `[EnumeratorCancellation]` on `cancellationToken`, same `async IAsyncEnumerable` pattern as `WatchPodsAsync`.
- `PatchDeploymentAsync`: must first call `Client.AppsV1.ReadNamespacedDeploymentAsync(deploymentName, namespaceName)` to get `containers[0].name`, then build the Strategic Merge Patch body as JSON, call `PatchNamespacedDeploymentAsync` with `V1Patch.PatchType.StrategicMergePatch`.
- `RestartDeploymentAsync`: build JSON Merge Patch body with timestamp, call `PatchNamespacedDeploymentAsync` with `V1Patch.PatchType.MergePatch`.

---

### AN-07: DeploymentViewModel Properties

```csharp
// Backing field (private V1Deployment _deployment)
// Constructor: public DeploymentViewModel(V1Deployment deployment)
// Calls Update(deployment) from constructor

[ObservableProperty] private string _name = string.Empty;
[ObservableProperty] private string _namespace = string.Empty;
[ObservableProperty] private int _desiredReplicas;
[ObservableProperty] private int _readyReplicas;
[ObservableProperty] private int _availableReplicas;
[ObservableProperty] private string _imageTag = string.Empty;

// Mapping from V1Deployment:
// Name          → deployment.Metadata.Name
// Namespace     → deployment.Metadata.NamespaceProperty
// DesiredReplicas   → deployment.Spec.Replicas ?? 0
// ReadyReplicas     → deployment.Status.ReadyReplicas ?? 0
// AvailableReplicas → deployment.Status.AvailableReplicas ?? 0
// ImageTag      → deployment.Spec.Template.Spec.Containers.FirstOrDefault()?.Image ?? string.Empty

// Public method: void Update(V1Deployment deployment)
// — re-maps all properties from the new object (same logic as PodViewModel.Update)
```

**Null-safety rules:** All `?.` chains must fall back to `0` (int) or `string.Empty` (string). Do not throw from `Update`.

---

### AN-08: Watch Loop Resilience

**Decision:** Adopt the same reconnect pattern as `ServiceListViewModel.WatchServicesAsync` — outer `while (!token.IsCancellationRequested)` with `catch (OperationCanceledException) { break; }` and `await Task.Delay(5000, token)` on all other exceptions. No additional circuit-breaker or max-retry limit is required for this feature (consistent with the Pod watch which also has no limit).

**Rationale:** `PodListViewModel.WatchPodsAsync` uses this same pattern. Adding a separate resilience library (Polly) is out of scope. A 5-second back-off on error is sufficient for a dev-tools application.

**Important:** The watch task is fire-and-forget (`_ = WatchDeploymentsAsync(token)`) inside `InitializeAsync`, same as `PodListViewModel`. The cancellation token from `_cancellationTokenSource` handles shutdown. On re-connect, `_cancellationTokenSource` must be cancelled and replaced before a new watch is started (see `ServiceListViewModel.InitializeAsync` for the pattern).

---

### AN-09: Risk Register

| ID | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R-01 | Strategic Merge Patch silently discards image update if first-container read returns stale data | Low | Medium | Read deployment immediately before patch; do not cache container name |
| R-02 | `ShowDialog` owner resolution fails if `MainWindow` is not yet set | Low | Low | Guard with null check; fall back to `Show()` (same guard as `OpenPodDetailWindow`) |
| R-03 | Watch loop leaks if `Cleanup()` is not called on navigation away | Low | Medium | `DeploymentListViewModel` must implement `IDisposable` and cancel CTS in `Dispose(bool)` |
| R-04 | `ErrorMessage` not cleared between commands gives stale error appearance | Medium | Low | Clear `ErrorMessage = string.Empty` at the start of each command before the `try` block |
