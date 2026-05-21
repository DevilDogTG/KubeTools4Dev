# Review: team-profile-composition
**Date:** 2025-07-14
**Reviewer:** team-reviewer
**Status:** PASS

---

## AC Verification

- [x] **AC-1 — `team.md` (global) uses v2.0 format.**
  `version: 2.0` present (line 4). `roles:` block with `profiles:` lists for all five roles (lines 5–15). Collision rule (`last profile wins`) documented in `## Profile Resolution Rules`. ≤4-profile limit documented. ✅

- [x] **AC-2 — `team-developer/AGENT.md` contains no base-developer prose.**
  `## Base Profile` section is absent. The phrase "All rules from the `base-developer` profile apply…" is absent. No base-developer references anywhere in the file. ✅

- [x] **AC-3 — `team-reviewer/AGENT.md` Rule 4 contains no informal base-developer directive.**
  Rule 4 reads: "**Standard compliance.** Apply standard rules: naming conventions, scope discipline, no TODO comment without a tracking ticket, no broken file or symbol references." No mention of `base-developer`. ✅

- [x] **AC-4 — `sk-team-start` resolves N profiles.**
  Step 3 correctly: (1) reads `roles.[role].profiles` from global `team.md`; (2) appends `profiles_append` from workspace override if present (does not replace); (3) loads each profile file in order; (4) concatenates with last-wins semantics. Algorithm matches `## Profile Resolution Rules` in `team.md`. ✅

- [x] **AC-5 — `sk-team-dispatch` inlines all N profiles with BEGIN/END separators.**
  Step 4a wraps each profile with:
  ```
  --- BEGIN PROFILE: [profile-id] ---
  [full verbatim content]
  --- END PROFILE: [profile-id] ---
  ```
  Same format confirmed in `## Copilot CLI Notes`. ✅

- [x] **AC-6 — Workspace `team.md` uses `profiles_append` (not `profiles:`).**
  File exists at `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md`. Contains only `profiles_append: [csharp-developer]` for the developer role. No `profiles:` key present. ✅

- [x] **AC-7 — Effective set `[base-developer, team-developer, csharp-developer]` documented in workspace `team.md`.**
  Comment block in frontmatter reads: `[base-developer, team-developer, csharp-developer]` with sub-note `(global: [base-developer, team-developer]  +  append: [csharp-developer])`. ✅

- [x] **AC-8 — `sk-team-start` announce block shows `Profiles: [...]`.**
  Step 5 announce template contains `Profiles: [resolved profile IDs, comma-separated]`. The old `(team-[role] profile active)` text is absent. ✅

---

## Internal Consistency Checks

| Check | Result |
|-------|--------|
| `team-start.md` Step 3 algorithm matches `team.md ## Profile Resolution Rules` | ✅ Consistent — load order, append semantics, last-wins rule all agree |
| `team-dispatch.md` Step 4a BEGIN/END separators consistent with Copilot CLI Notes section | ✅ Consistent — same format in both locations (minor placeholder alias `[profile-id]` vs `[id]` noted under Suggestions) |
| Workspace `team.md` uses only `profiles_append`, never `profiles:` | ✅ Confirmed |
| `team-developer/AGENT.md` — no residual `base-developer` prose | ✅ Confirmed |
| `team-reviewer/AGENT.md` Rule 4 — no residual `base-developer` reference | ✅ Confirmed |

---

## Security Scan

All changed files are documentation/configuration (Markdown + YAML). No secrets, credentials, injection vectors, deserialization, or input boundaries present. **Clean.**

---

## Findings

### Blocking
*(none)*

### Non-blocking

**NB-1 — `sk-team-start` validation checklist does not enforce the 4-profile limit.**
The `## Profile Resolution Rules` in `team.md` states: "No role may declare more than **4 profiles** in the resolved list." However, Step 3 of `team-start.md` does not include a guard that errors when the resolved list exceeds 4 entries, and the Validation checklist at the end of the skill has no corresponding item. A user could accidentally exceed the limit without any warning from the skill.
*Recommendation:* Add a check after Step 3-step-2: "If the resolved list length exceeds 4, report an error and stop." Add a matching Validation checklist item.

### Suggestions

**S-1 — Standardize the profile-ID placeholder in `team-dispatch.md`.**
Step 4a uses `[profile-id]` while the Copilot CLI Notes section uses `[id]` in the separator format. Both refer to the same thing. Standardizing to `[profile-id]` throughout would marginally improve readability.

---

## Test Gate

Documentation-only task — no source code changed, no automated test suite applicable.
Manual verification completed: all 8 ACs traced against actual on-disk file content. All pass.

---

## Sign-off

**PASS** — All 8 acceptance criteria verified against actual file content. No blocking issues found. One non-blocking gap (NB-1: missing 4-profile limit guard in `sk-team-start`) and one suggestion (S-1: placeholder naming). Ready for QA.
