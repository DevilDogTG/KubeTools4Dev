# Handover: Reviewer → QA

**Team:** dev-team
**Task:** team-profile-composition
**Date:** 2025-07-14
**From:** reviewer
**To:** qa

---

## Review Result

**PASS** — All 8 acceptance criteria verified against actual on-disk file content. No blocking issues. One non-blocking finding (missing 4-profile limit guard in `sk-team-start`). One suggestion (placeholder alias inconsistency in `team-dispatch.md`). Framework changes are internally consistent and safe to validate end-to-end.

---

## What QA Must Validate

QA must perform end-to-end validation of the profile-composition framework. Verify the following:

1. **AC-1** — Open `~/.agent-brains/teams/dev-team/team.md`. Confirm `version: 2.0`, a `roles:` block with `profiles:` lists for all five roles, the collision rule (`last profile wins`), and the ≤4-profile limit, all present.

2. **AC-2** — Open `~/.agent-brains/profiles/team-developer/AGENT.md`. Confirm no `## Base Profile` section and no "All rules from the `base-developer` profile apply" text.

3. **AC-3** — Open `~/.agent-brains/profiles/team-reviewer/AGENT.md`. Confirm Review Rule 4 makes no reference to `base-developer`.

4. **AC-4** — Open `~/.agent-brains/skills/team-start/team-start.md`. Confirm Step 3 reads global `profiles:`, appends workspace `profiles_append:`, loads files in order, and concatenates (last wins). Confirm the announce block in Step 5 uses `Profiles: [...]`.

5. **AC-5** — Open `~/.agent-brains/skills/team-dispatch/team-dispatch.md`. Confirm Step 4a wraps each inlined profile with `--- BEGIN PROFILE: [profile-id] ---` / `--- END PROFILE: [profile-id] ---` separators. Confirm Copilot CLI Notes section references the same format.

6. **AC-6** — Confirm `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md` exists and contains `profiles_append: [csharp-developer]` for the `developer` role, with no `profiles:` key.

7. **AC-7** — Confirm the workspace `team.md` comment block documents the effective set as `[base-developer, team-developer, csharp-developer]`.

8. **AC-8** — Re-confirm the `sk-team-start` announce block (Step 5) shows `Profiles: [...]` and not the old `(team-[role] profile active)` text.

### End-to-End Scenario (recommended)

Simulate a `sk-team-start as developer` invocation mentally or by dry-run trace:
- Global `team.md` gives `profiles: [base-developer, team-developer]`.
- Workspace `team.md` gives `profiles_append: [csharp-developer]`.
- Resolved list: `[base-developer, team-developer, csharp-developer]` — 3 entries, within the ≤4 limit.
- Each profile file path resolves to `~/.agent-brains/profiles/[id]/AGENT.md`.
- Announce block should show: `Profiles: base-developer, team-developer, csharp-developer`.

---

## Files Changed

| File | Location | Change Summary |
|------|----------|----------------|
| `team.md` (global) | `~/.agent-brains/teams/dev-team/team.md` | Migrated to v2.0: `roles:` block, Profile Resolution Rules section, updated table and Cross-Provider line |
| `team-start.md` | `~/.agent-brains/skills/team-start/team-start.md` | Bumped to v2.0: N-profile resolution algorithm in Step 3, updated announce block to `Profiles: [...]` |
| `team-dispatch.md` | `~/.agent-brains/skills/team-dispatch/team-dispatch.md` | Bumped to v1.2: Step 4a inlines all N profiles with BEGIN/END separators, updated Copilot CLI Notes |
| `team-developer/AGENT.md` | `~/.agent-brains/profiles/team-developer/AGENT.md` | Removed `## Base Profile` section and "All base-developer rules apply" prose |
| `team-reviewer/AGENT.md` | `~/.agent-brains/profiles/team-reviewer/AGENT.md` | Rule 4 rewritten to remove `base-developer` reference; rule text preserved as self-contained statement |
| `team.md` (workspace) | `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md` | New file: workspace override using `profiles_append: [csharp-developer]` for developer role |

---

## Non-blocking Findings for QA Awareness

**NB-1 — `sk-team-start` has no 4-profile limit guard.**
The `## Profile Resolution Rules` in `team.md` documents a maximum of 4 profiles per role, but `sk-team-start` Step 3 does not validate or reject lists exceeding this limit. This does not affect correctness of the current configuration (which has 3 profiles for the developer role) but could silently allow future violations. Flagged for a follow-up fix by the Developer.
