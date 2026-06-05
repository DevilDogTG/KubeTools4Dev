# Plan: Pod Diagnostics (Logs Power-Ups + Events Viewer)

**Status:** active
**Created:** 2026-06-05
**Branch:** feature/pod-diagnostics

## Goal
Turn the pod detail window from a raw log pipe into a troubleshooting tool: filterable/highlighted/saveable logs plus a first-class Events view — shippable as the v1.4.0 story.

## Scope notes (from code scan)
- Log lines already live in `_logLines: List<string>` (ring buffer, `MaxLogLines = 1000`) in `PodDetailViewModel.cs`; UI shows one joined string `PodLogsText` via `SelectableTextBlock` (`PodDetailWindow.axaml:27-35`). Filtering = rebuild from `_logLines` applying a predicate; the Channels batch loop + stream-generation stamp stay untouched.
- Severity highlighting requires moving from a single bound string to `SelectableTextBlock.Inlines` (colored `Run`s per line) — keep the batched rebuild, just emit Inlines instead of `string.Join`. Watch perf at 1000 lines.
- Logs/Describe use a bool `IsLogsView` visibility toggle → generalize to a 3-state view selector (Logs / Describe / Events).
- Event fetch code already exists inside `KubernetesService.GetPodDescribeAsync()` (`ListNamespacedEventAsync` with `involvedObject.name` fieldSelector, lines 176-202) — extract into a dedicated `GetPodEventsAsync`.
- Save-file: no `SaveFilePickerAsync` usage in app yet; mirror the `OpenFilePickerAsync` pattern in `AddClusterDialog.axaml.cs:23-42`.
- Test seam: `protected virtual DispatchToUIAsync` override pattern in `PodDetailViewModelTests.cs` (`TestableViewModel`, `WaitUntilAsync` lock-protected polling — no wall-clock waits).

## Checklist

### A — Log viewer power-ups
- [x] A1: Filter box above log pane — case-insensitive substring match against `_logLines`; live re-filter on text change; empty = show all. Filter applies to display only (ring buffer keeps everything).
- [x] A2: Severity highlighting — parse common level tokens (`Error|Warning|Information|Debug` + bracketed `[ERR]/[WRN]`-style) per line; render via `Inlines` colored `Run`s (theme-aware: red/amber/default/dim). Fallback: unparsed lines render default.
- [x] A3: Follow toggle — auto-scroll to end on new batch when ON (default); flipping OFF freezes viewport. Turn OFF automatically when user scrolls up.
- [x] A4: Save logs to file — `StorageProvider.SaveFilePickerAsync` → write full `_logLines` (unfiltered) as `.txt`; default filename `<pod>-<container>-<yyyyMMdd-HHmmss>.log`.
- [x] A5: UI tests for filter (match/no-match/clear), severity classification, follow-toggle state; reuse `TestableViewModel` seam.

### B — Events viewer
- [x] B1: `IKubernetesService.GetPodEventsAsync(ns, pod)` returning a typed event model (type, reason, message, count, lastTimestamp, age); extract/refactor the fetch out of `GetPodDescribeAsync` (describe output unchanged). Core tests.
- [x] B2: 3-state view selector in `PodDetailWindow` (Logs / Describe / Events); Events pane = sorted-desc list with type-colored rows + manual Refresh button + loading/error states.
- [x] B3: ViewModel tests: load, sort order, error surface, empty-state ("No events").

### C — CI chore (deadline 2026-06-16)
- [x] C1: Bump Node-20 actions in `publish.yml` (and check `release.yml`/`tag.yml`) to Node-24 majors: `actions/checkout`, `actions/setup-dotnet`, `softprops/action-gh-release`. Verify with a workflow lint/dry run.

### D — Ship
- [ ] D1: `sk-finish-feature` preflight + PR; `sk-pr-review` pass.
- [ ] D2: Update memory/overview + backlog; release v1.4.0 via `sk-release` when approved.

## Progress Log
_Updated as steps complete._
- 2026-06-05 — Plan created; scope grounded in code scan (log pipeline, describe toggle, existing event fetch, test seams).
- 2026-06-05 — C1 done (commit f60c045): checkout v4→v6, setup-dotnet v4→v5, action-gh-release v2→v3 across all 3 workflows (9 refs). Verified all latest majors declare node24 via each action.yml; checkout v6 cred-storage change confirmed harmless (plain `git push` flows only). Real-run verification (no deprecation annotations) happens on this branch's CI run.
- 2026-06-05 — B1 done (commit 0b4b022): `PodEventInfo` record (Core/Models) with timestamp/count fallback chains + `FormatAge(now)`; `GetPodEventsAsync` on IKubernetesService; shared `ListPodEventsAsync` private helper (describe path unchanged). NSubstitute fakes auto-cover the new member — no fake edits needed. 102 Core + 77 UI tests green, 0W/0E. Next: B2 (3-state view selector + Events pane).
- 2026-06-05 — B2+B3 done (commit d044f7e): `PodDetailWindow` now a TabControl (Logs/Describe/Events) with lazy one-shot per-tab loads; logs keep streaming across tab switches. `SelectedViewIndex` replaces `IsLogsView` setter (factory `tab` int passes straight through; `LogsViewIndex/DescribeViewIndex/EventsViewIndex` consts). Events pane: `PodEventRow` rows, Refresh command, loading/error/empty states, `Classes.eventWarning` + `SemiColorWarning` for Warning rows. Events button added to pod list Actions (width 140→200). `UtcNow` virtual seam for age tests. 102 Core + 84 UI green, 0W/0E. NOT yet visually smoke-tested in a live cluster. Next: A1 (log filter box).
- 2026-06-05 — A1 done (commit 1a45f93): live case-insensitive `LogFilter` + `LogFilterStatus` ("n / m lines") above the log pane; display-only (ring buffer unfiltered). 4 tests.
- 2026-06-05 — A3+A4 done (commit 9ee348c): `IsFollowingLogs` VM property replaces code-behind `_autoScroll` (gesture heuristic + Follow ToggleButton stay in sync; checking jumps to end). Save... button → `SaveFilePickerAsync`, exports FULL buffer, suggested name `pod[-container]-yyyyMMdd-HHmmss.log` via `UtcNow` seam, failure appends notice line (async void guarded). 5 tests.
- 2026-06-05 — A2+A5 done (commit 7028d0f): `LogLineClassifier` (pure static, 80-char scan window, Error>Warning>Debug precedence) covering bracketed/MEL-console/Serilog/logfmt/full-word/panic formats; code-behind renders `PodLogsText` → severity-colored `Run` inlines on `SelectableTextBlock` (cross-line selection preserved; rebuild at Channels-batch cadence; `SemiColorDanger`/`SemiColorWarning` with fixed fallbacks). 28 tests. **All A+B+C items complete: 223 tests (102 Core + 121 UI), 0W/0E. Remaining: user smoke-test, then D1 (finish-feature → PR) and D2.**
