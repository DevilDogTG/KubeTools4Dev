# Plan: Port-Forward Profiles

**Status:** active
**Created:** 2026-05-26
**Branch:** feature/port-forward-profiles

## Goal
Replace the "Forward All / Stop All" buttons with a profile-based port-forward system: users create named profiles (per cluster) that specify which services to forward, then select a profile and click Start to activate all its forwards at once.

## Design Decisions
- **Profile scope**: Per-cluster (stored under `ClusterEntry` in `settings.json`)
- **Adding services**: From the service list row — "Add to Profile" button appears when a profile is selected
- **UI placement**: Replace "Forward All / Stop All" header area with Profile selector + Start/Stop/Manage controls

## Data Model

### New: `PortForwardProfileEntry` (Core/Models)
| Field | Type | Notes |
|---|---|---|
| Namespace | string | Kubernetes namespace |
| ServiceName | string | Service name |
| TargetPort | string | Port (int-or-string as string) |
| LocalPort | int | Local port to bind |

### New: `PortForwardProfile` (Core/Models)
| Field | Type | Notes |
|---|---|---|
| Id | Guid | Identity |
| Name | string | User-visible name |
| Entries | List\<PortForwardProfileEntry\> | Services in this profile |

### Modified: `ClusterEntry` (Core/Models)
Add: `List<PortForwardProfile> PortForwardProfiles { get; set; } = []`

## Architecture

```
ServiceListView.axaml
└── Profile Header (replaces Forward All / Stop All)
    ├─ ComboBox: SelectedProfile (shows all profiles for cluster)
    ├─ [+ New] [✏ Rename] [🗑 Delete] profile management buttons
    ├─ [▶ Start Profile] — starts all entries via IPortForwardService
    └─ [■ Stop Profile]  — stops all active entries

DataGrid rows (ServiceViewModel)
├─ [+ Add to Profile] button — visible when a profile is selected & service not in it
└─ [✓ In Profile] indicator — visible when service is already in selected profile
```

## Checklist

### Phase 1 — Core Models
- [x] Add `PortForwardProfileEntry.cs` to `KubeTools4Dev.Core/Models/`
- [x] Add `PortForwardProfile.cs` to `KubeTools4Dev.Core/Models/`
- [x] Add `PortForwardProfiles` list to `ClusterEntry`

### Phase 2 — Core ViewModels
- [x] Add `PortForwardProfileEntryViewModel.cs` to `KubeTools4Dev.Core/ViewModels/`
- [x] Add `PortForwardProfileViewModel.cs` to `KubeTools4Dev.Core/ViewModels/` (Name, Entries, Contains check)

### Phase 3 — ServiceListViewModel Refactor
- [x] Add `clusterId` parameter to `UpdateScopeAsync`
- [x] Replace `ForwardAllCommand` / `StopAllCommand` with profile commands
- [x] Save profiles back to `ClusterEntry` via `ISettingsService.Save()` on any mutation

### Phase 4 — ServiceViewModel Additions
- [x] Add `AddToProfileCommand` / `RemoveFromProfileCommand`
- [x] Add `IsInSelectedProfile` observable property
- [x] Wire callbacks from `ServiceListViewModel` → `ServiceViewModel`

### Phase 5 — UI Updates
- [x] `ServiceListView.axaml` — replaced Forward All / Stop All with full profile bar
- [x] DataGrid — new "Profile" column with ＋/✓ buttons

### Phase 6 — Update Callers
- [x] `MainViewModel.cs` — passes `ctx.ClusterId` to `UpdateScopeAsync`

### Phase 7 — Tests
- [x] `PortForwardProfileViewModelTests.cs` — 15 Core VM + JSON round-trip tests
- [x] `ServiceListViewModelProfileTests.cs` — 20 profile CRUD/load/canExecute tests
- [x] Build: 0 warnings, 0 errors (`-warnaserror`)
- [x] Tests: 72 Core + 57 UI = 129 total, all green

## Progress Log
_Updated as steps complete._
