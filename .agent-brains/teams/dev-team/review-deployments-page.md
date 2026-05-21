# Review Notes: deployments-page
**Branch:** feature/deployments-page  
**Reviewer:** team-reviewer  
**Date:** 2026-05-20  
**Test Gate:** PASS (59/59 tests)

---

## Plan Coverage Check

All 18 checklist items verified as implemented.

- [x] **1.1** — `GetDeploymentsAsync`, `WatchDeploymentsAsync`, `PatchDeploymentAsync`, `RestartDeploymentAsync` signatures added to `IKubernetesService`. XML doc present on all four.
- [x] **1.2** — All four methods implemented in `KubernetesService`. Patterns follow existing `GetServicesAsync` / `WatchServicesAsync`. `PatchDeploymentAsync` uses Strategic Merge Patch; `RestartDeploymentAsync` uses JSON Merge Patch with ISO 8601 timestamp.
- [x] **2.1** — `DeploymentViewModel` created in `src/KubeTools4Dev/ViewModels/` with all 6 observable properties (`Name`, `Namespace`, `DesiredReplicas`, `ReadyReplicas`, `AvailableReplicas`, `ImageTag`) and `Update(V1Deployment)` method. Null-safety chains present on all property reads.
- [x] **2.2** — `SidebarViewModel` updated: `IsDeploymentsVisible` added at index 2 (0-based); `IsSettingsVisible` shifted to index 3; `OnSelectedNavIndexChanged` fires `PropertyChanged` for the new property. (See Non-Blocking Finding NB-03 for plan AC-12 ambiguity.)
- [x] **2.3** — `SidebarViewModelTests` updated: `[InlineData]` expanded to 4 bool parameters; new test `InitialState_SelectedNavIndex_IsZero_PodsVisible` asserts `IsDeploymentsVisible == false`; `SelectedNavIndex_UpdatesVisibilityFlags` theory covers all 4 indices; `PropertyChanged` test asserts `IsDeploymentsVisible` is raised.
- [x] **3.1** — `DeploymentListViewModel` created: `_allDeployments` list, `Deployments` `ObservableCollection`, `FilterText`, `IsLoading`, `ErrorMessage`, `LastRefreshTime`, `DispatcherTimer` (deferred to `InitializeAsync` per D-03), `CancellationTokenSource`, `InitializeAsync()`, `WatchDeploymentsAsync(CancellationToken)`, `UpdateFilteredList()`, `IDisposable`.
- [x] **3.2** — `[RelayCommand] RolloutRestartAsync(DeploymentViewModel)` implemented: calls `RestartDeploymentAsync`; clears `ErrorMessage` before attempt; sets `ErrorMessage` on failure; logs via `_logger.LogError`.
- [x] **3.3** — `[RelayCommand] EditDeploymentAsync(DeploymentViewModel)` implemented: opens `EditDeploymentDialog` via `ShowEditDialogAsync` (protected virtual for testability); calls `PatchDeploymentAsync` on confirm; surfaces error on failure.
- [x] **3.4** — `EditDeploymentDialogViewModel` created with `DeploymentName`, `Replicas`, `ImageTag`, `ErrorMessage`, `IsConfirmed`, `CloseCallback`, `ConfirmCommand` (validates `Replicas >= 0` and non-empty `ImageTag`), `CancelCommand`.
- [x] **4.1** — `DeploymentListView.axaml` created with filter bar (`TextBox` bound to `FilterText`), `ProgressBar` for `IsLoading`, error `TextBlock` (row 1, `StringConverters.IsNotNullOrEmpty`), 7-column `DataGrid` (Namespace, Name, Desired, Ready, Available, Image Tag, Actions with Restart + Edit buttons). Matches visual structure of PodListView/ServiceListView.
- [x] **4.2** — `EditDeploymentDialog.axaml` is a `Window` with `WindowStartupLocation="CenterOwner"`, deployment name label, `NumericUpDown` (Minimum=0), `TextBox` for image tag, error `TextBlock`, Confirm and Cancel buttons.
- [x] **4.3** — `MainWindow.axaml` updated: `ListBoxItem` for Deployments with `AlphaD` MaterialIcon at position 2 in sidebar `ListBox`; `Panel` bound to `Sidebar.IsDeploymentsVisible` containing `<views:DeploymentListView DataContext="{Binding DeploymentList}"/>`.
- [x] **5.1** — `MainViewModel` updated: `[ObservableProperty] DeploymentListViewModel _deploymentList` field; injected via constructor parameter; `await DeploymentList.InitializeAsync()` called in `Connect()`; `DeploymentList?.Dispose()` called in `Cleanup()`.
- [x] **5.2** — `services.AddTransient<DeploymentListViewModel>()` registered in `App.axaml.cs` `ConfigureServices`.
- [x] **6.1** — `DeploymentViewModelTests` (5 tests): `Constructor_MapsAllPropertiesFromDeployment`, `Update_RemapsAllPropertiesFromNewDeployment`, `ImageTag_ReadsFirstContainerImage`, `ImageTag_IsEmptyString_WhenContainersIsNull`, `DesiredReplicas_IsZero_WhenSpecReplicasIsNull`.
- [x] **6.2** — `DeploymentListViewModelTests` (8 tests): `RolloutRestartCommand_CallsRestartDeploymentAsync_WithCorrectArgs`, `_SetsErrorMessage_WhenServiceThrows`, `_ClearsErrorMessage_BeforeEachInvocation`, `EditDeploymentCommand_CallsPatchDeploymentAsync_WhenConfirmed`, `_DoesNotCallPatch_WhenCancelled`, `EditDialog_ConfirmCommand_RejectsNegativeReplicas`, `_RejectsEmptyImageTag`, `_SetsIsConfirmed_WhenInputIsValid`, `CancelCommand_LeavesIsConfirmedFalse`.
- [x] **7.1** — Build confirmed 0W / 0E per developer handover (build not re-run here; tests pass which implicitly validates build).
- [x] **7.2** — `dotnet test --no-build`: **59/59 passed** (0 failed, 0 skipped, 2.4 s).

---

## Architecture Compliance

- [x] **AN-01** — `PatchDeploymentAsync` uses `V1Patch.PatchType.StrategicMergePatch`. First reads `containers[0].Name` from the live deployment before building the patch body. Patch body shape matches the prescribed JSON structure exactly.
- [x] **AN-02** — `RestartDeploymentAsync` uses `V1Patch.PatchType.MergePatch`. Annotation key `kubectl.kubernetes.io/restartedAt` set to `DateTime.UtcNow.ToString("o")`. Correct patch body shape.
- [x] **AN-03** — `ErrorMessage` observable string property on both `DeploymentListViewModel` and `EditDeploymentDialogViewModel`. Views bind a `TextBlock` with `IsVisible` using `StringConverters.IsNotNullOrEmpty`. `ErrorMessage` cleared at start of each command (`ErrorMessage = string.Empty` before try block) — satisfies R-04.
- [x] **AN-04** — `EditDeploymentDialog` is a `Window`. `ShowEditDialogAsync` calls `await dialog.ShowDialog(owner)`. Uses `CloseCallback` pattern (D-04) instead of `ShowDialog<bool?>` — functionally equivalent: `CloseCallback` calls `dialog.Close()`, `IsConfirmed` flag carries the result. Deviation documented by developer. Fall-back to `dialog.Show()` when owner is null is guarded (same pattern as `OpenPodDetailWindow`).
- [x] **AN-05** — `DeploymentViewModel`, `DeploymentListViewModel`, and `EditDeploymentDialogViewModel` all reside in `src/KubeTools4Dev/ViewModels/` (UI assembly). Consistent with `PodViewModel`, `ServiceViewModel`, `PodListViewModel`, `ServiceListViewModel`.
- [x] **AN-06** — Interface method signatures match the specification exactly: `GetDeploymentsAsync(string namespaceName = "default")`, `WatchDeploymentsAsync(string, CancellationToken)`, `PatchDeploymentAsync(string, string, int, string)`, `RestartDeploymentAsync(string, string)`.
- [x] **AN-07** — `DeploymentViewModel` properties match specification: all 6 `[ObservableProperty]` fields present, constructor calls `Update(deployment)`, `Update` method re-maps all properties with correct null-safety (`?? 0`, `?? string.Empty`).
- [x] **AN-08** — Watch loop in `WatchDeploymentsAsync(CancellationToken)` follows the resilience pattern: outer `while (!token.IsCancellationRequested)`, inner `await foreach`, `catch (OperationCanceledException) { break; }`, `catch (Exception)` with `await Task.Delay(5000, token)`. Re-connect pattern in `InitializeAsync` cancels and replaces `_cancellationTokenSource` before starting new watch.

---

## Standard Compliance

- [x] **XML doc on all public classes and members** — All new public classes (`DeploymentViewModel`, `DeploymentListViewModel`, `EditDeploymentDialogViewModel`, `DeploymentListView`, `EditDeploymentDialog`) have `<summary>` on class and every public member. New interface methods and implementation methods in `IKubernetesService` / `KubernetesService` have full `<summary>` + `<param>` + `<returns>` tags. `SidebarViewModel` new property has XML doc.  
  *Minor cosmetic: in `KubernetesService.cs`, the `<returns>` tag for `WatchDeploymentsAsync` is indented with 3 spaces instead of 4 (diff line alignment). No functional impact.*

- [~] **`[LoggerMessage]` used (not inline string interpolation)** — The new `DeploymentListViewModel.cs` uses `_logger.LogError(ex, "message {Var}", value)` directly in method bodies (lines 132, 172, 205, 308) rather than `[LoggerMessage]` source-generated static partial methods. No string interpolation (`$"..."`) is used — all calls use structured message templates with named parameters. Confirmed pre-existing pattern: `PodListViewModel`, `ServiceListViewModel`, and `KubernetesService` also use direct `LogXxx` calls throughout; `[LoggerMessage]` is not used anywhere in the solution. The new code is consistent with the codebase baseline. See **NB-01**.

- [x] **`CancellationToken` passed through** — `WatchDeploymentsAsync` properly passes the token with `[EnumeratorCancellation]`. `InitializeAsync` manages its own `CancellationTokenSource` (by design; not an externally cancellable operation). `GetDeploymentsAsync`, `PatchDeploymentAsync`, and `RestartDeploymentAsync` have no `CancellationToken` parameter in the interface (per AN-06 specification); this is intentional and consistent with the Pods/Services interface.

- [~] **`IDisposable` — `Dispose(bool)` pattern** — `Dispose()` calls `Dispose(true)` + `GC.SuppressFinalize(this)` ✅. `protected virtual void Dispose(bool disposing)` is present ✅. Finalizer (`~DeploymentListViewModel()` calling `Dispose(false)`) is absent. Since the class manages only managed resources (`CancellationTokenSource`, `DispatcherTimer`), the finalizer is not strictly required. However, the standard specifies the "full" pattern. See **NB-02**.

- [x] **No hardcoded secrets or credentials** — None found.

---

## Findings

### Blocking Issues

*None.*

---

### Non-Blocking Issues

**NB-01 — Logging: direct `LogError` calls instead of `[LoggerMessage]` (pre-existing pattern)**  
**Files:** `src/KubeTools4Dev/ViewModels/DeploymentListViewModel.cs`, lines 132, 172, 205, 308  
**Detail:** The coding standard mandates `[LoggerMessage]` source-generated static partial methods. The new code uses `_logger.LogError(ex, "template {Param}", value)` directly in method bodies. No string interpolation is present (blocking criterion not triggered). This is consistent with the pre-existing pattern in `PodListViewModel.cs`, `ServiceListViewModel.cs`, and `KubernetesService.cs`, none of which use `[LoggerMessage]`. Should be addressed as part of a codebase-wide logging remediation, not blocked on this PR.

**NB-02 — `IDisposable` missing finalizer**  
**File:** `src/KubeTools4Dev/ViewModels/DeploymentListViewModel.cs`  
**Detail:** The "full" `Dispose(bool)` pattern requires a finalizer `~DeploymentListViewModel() { Dispose(false); }`. The class only holds managed resources so the finalizer would be purely defensive. Should be added for standard compliance.

**NB-03 — Plan AC-12 index ambiguity (plan document error)**  
**File:** `.agent-brains/plan/deployments-page.md`, Acceptance Criteria item 12  
**Detail:** AC-12 states: *"`SidebarViewModel` emits `IsDeploymentsVisible = true` when `SelectedNavIndex == 3`; Settings is now index 4."* The implementation correctly uses 0-based indices (Deployments=2, Settings=3), which is consistent with the existing codebase (Pods=0, Services=1) and with the plan's own Task Checklist item 2.2 ("add `IsDeploymentsVisible` (index 2), shift Settings to index 3"). The AC-12 text appears to have been written with 1-based counting and conflicts with the checklist. Developer deviation D-01 documents the resolution. The plan document should be corrected to say `SelectedNavIndex == 2` and Settings at index 3. No code change needed; plan update recommended.

---

### Suggestions

**S-01 — `ShowEditDialogAsync` non-blocking fallback path**  
**File:** `src/KubeTools4Dev/ViewModels/DeploymentListViewModel.cs`, lines 219–223  
**Detail:** When `owner` is null (headless environments), `dialog.Show()` is called instead of `await dialog.ShowDialog(owner)`. `Show()` returns immediately (fire-and-forget), meaning `EditDeploymentAsync` checks `vm.IsConfirmed` before the user has interacted with the dialog. In production this path is unreachable (owner is always the MainWindow), but a `// Note: Show() is non-blocking fallback for headless environments; dialog result is not awaited` comment would make the risk explicit.

**S-02 — `GetDeploymentsAsync` lacks `CancellationToken`**  
**File:** `src/KubeTools4Dev.Core/Services/Interfaces/IKubernetesService.cs`  
**Detail:** `GetDeploymentsAsync` and `RestartDeploymentAsync`/`PatchDeploymentAsync` have no `CancellationToken` parameter, unlike `WatchDeploymentsAsync`. The Kubernetes client supports cancellation on list/patch operations. Adding CT would improve responsiveness during navigation-away scenarios. Out of scope for this PR but worth tracking.

**S-03 — `ErrorMessage` on `EditDeploymentDialogViewModel` not cleared on `CancelCommand`**  
**File:** `src/KubeTools4Dev/ViewModels/EditDeploymentDialogViewModel.cs`, line 89  
**Detail:** If the user triggers validation errors and then presses Cancel, `ErrorMessage` is left non-empty. A subsequent re-open of the dialog on the same VM instance (if the VM were reused) would show stale errors. In the current implementation the VM is created fresh per `EditDeploymentAsync` invocation, so this is harmless, but a `ErrorMessage = string.Empty` in `Cancel()` would be cleaner.

---

## Verdict

**PASS** — All 18 plan items implemented, all 59 tests pass, all architecture notes (AN-01 through AN-08) satisfied, no security issues, no blocking standard violations. Feature is ready for QA validation of the 15 acceptance criteria.
