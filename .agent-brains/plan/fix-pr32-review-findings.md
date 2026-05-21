# Plan: Fix PR #32 Review Findings

**Branch:** feature/deployments-page  
**PR:** #32  
**Date:** 2026-05-21  

## Scope

Address all actionable findings from the pr-review comment posted at `review-sha: 44b3f97`.

---

## Findings to Address

| # | Severity | File | Fix |
|---|----------|------|-----|
| 1 | 🔴 Critical | `KubernetesService.cs:308` | Replace `Containers[0]` with guarded `FirstOrDefault()` |
| 2 | 🟡 Warning  | `DeploymentListViewModel.cs:143` | Add `_disposed` guard for idempotent `Dispose` |
| 3 | 🟡 Warning  | Three ViewModels | Primary constructors — consistent with `PodListViewModel`/`ServiceListViewModel` siblings; document as codebase-wide deviation |
| 4 | 🟡 Warning  | `DeploymentListViewModel.cs` | `[LoggerMessage]` — pre-existing pattern; document as deviation |
| 5 | 🔵 Info     | `DeploymentListViewModel.cs:223` | Add comment on non-blocking `dialog.Show()` fallback |

---

## Checklist

- [x] Fix 1: Guard `Containers[0]` in `KubernetesService.PatchDeploymentAsync`
- [x] Fix 2: Add `_disposed` guard to `DeploymentListViewModel.Dispose(bool)`
- [x] Fix 3: Document primary-constructor deviation (consistent with siblings — no code change)
- [x] Fix 4: Document `[LoggerMessage]` deviation (consistent with siblings — no code change)
- [x] Fix 5: Add comment to `ShowEditDialogAsync` non-blocking fallback
- [x] Build: 0W / 0E verified
- [x] Tests: 66/66 passed
- [x] Commit and push (a2d4437)
- [x] Re-invoked pr-review → Approved
