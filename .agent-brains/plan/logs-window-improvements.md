# Plan: Logs Window Improvements (selectable text + open-error fixes)

**Status:** active
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
- [ ] L1 Selectable logs: replace per-line `ItemsControl`/`TextBlock` with a single `SelectableTextBlock` bound to a joined `PodLogsText` string (keeps the existing `ScrollViewer` auto-scroll code-behind working; allows cross-line selection + Ctrl+C). ViewModel keeps the 1000-line ring internally; rebuild text on append (batch if perf demands).
- [ ] L2 Container support: extend `StreamPodLogsAsync` with optional `container` param. `PodDetailViewModel` loads pod's container list; if >1, show a ComboBox in the logs window header to pick the container (default: first); switching restarts the stream.
- [ ] L3 Error diagnostics: in `StartLogStreamAsync` catch, surface `ex.GetType().Name` + walk inner exceptions into the UI message; log full exception via `LogStreamError`. (Captures the real "XML conversion" error if it recurs.)
- [ ] L4 Tests: update existing `PodDetailViewModel` tests for `PodLogsText`; add multi-container default-selection + restart-on-switch tests; error-message format test.
- [ ] L5 Preflight + PR via sk-finish-feature.

## Progress Log
- 2026-06-05: Plan created from session-start triage.
