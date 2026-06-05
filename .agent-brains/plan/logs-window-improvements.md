# Plan: Logs Window Improvements (selectable text + open-error fixes)

**Status:** complete
**Created:** 2026-06-05
**Branch:** feature/logs-window-improvements

## Goal
1. Log text in the pod logs window can be highlighted and copied.
2. Opening logs no longer errors on multi-container pods; any remaining stream errors surface with full diagnostic detail (to finally capture the reported "XML conversion" message).

## Findings
- `PodDetailWindow.axaml:21-33`: logs render as `ItemsControl` + `TextBlock` (not selectable). Describe panel (lines 35-48) already uses selectable read-only `TextBox`; `SettingsView.axaml:66` uses `SelectableTextBlock`. Avalonia 12.0.1 supports both.
- `KubernetesService.StreamPodLogsAsync` (`KubernetesService.cs:144-165`) passes **no container name** to `ReadNamespacedPodLogAsync` → 400 on multi-container pods ("a container name must be specified"). Likely the "sometimes error on opening logs".
- `PodDetailViewModel.StartLogStreamAsync` (lines 62-95) catches all and shows only `ex.Message` — type/inner chain lost.

## Checklist
- [x] L1 Selectable logs: `SelectableTextBlock` bound to `PodLogsText`; 1000-line ring buffer; appends batched via `System.Threading.Channels` (one text rebuild per drained batch — review fix); stream-generation stamp guards stale UI lambdas on container switch (review fix).
- [x] L2 Container support: `StreamPodLogsAsync(…, string? container = null, …)`; header ComboBox visible when >1 container; first selected by default; switch restarts the stream. `PodViewModel.ContainerNames` added.
- [x] L3 Error diagnostics: `DescribeException` walks the inner-exception chain (type + message per level) and appends the K8s API response body from `HttpOperationException` (carries the real reason, e.g. "a container name must be specified"); trailing cancel-race catch logs at debug level (review fix).
- [x] L4 Tests: PodDetailViewModel suite reworked for `PodLogsText` + 5 new tests (picker default/visibility, container pass-through, restart-on-switch, error detail); deterministic lock-protected polling instead of `Task.Delay` waits (review fix); flake-checked 5×.
- [x] L5 PR #57 — `sk-pr-review` first pass ⚠️ needs-work at `68da20e` (3🟡/2🔵); all findings fixed in `f70e1a6`; re-review ✅ approved; rebase-merged 2026-06-05.

## Progress Log
- 2026-06-05: Plan created from session-start triage.
- 2026-06-05: L1–L4 done (`05901fa` core, `68da20e` ui). Review findings addressed in `f70e1a6` (generation stamp, channel batching, deterministic test waits). PR #57 approved + rebase-merged. Shipped in v1.3.6.
