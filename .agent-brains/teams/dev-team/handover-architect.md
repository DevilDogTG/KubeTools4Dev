# Handover: Architect → Developer

**Team:** dev-team  
**Task:** team-profile-composition  
**Date:** 2026-05-21  
**From:** architect  
**To:** developer

---

## What Was Done

- Verified `csharp-developer` profile exists at `C:\Users\SupawatTanmanee\.agent-brains\profiles\csharp-developer\AGENT.md` — Step 7 is unblocked.
- Decided v2.0 `team.md` schema: **Option A** — YAML frontmatter `roles:` block with per-role `profiles:` lists. Full example in AN-01.
- Decided workspace override format: minimal frontmatter-only file using `profiles_append:` per role. Full example in AN-02.
- Decided profile concatenation separator for `sk-team-dispatch` prompts: `--- BEGIN PROFILE: [id] ---` / `--- END PROFILE: [id] ---` block format. Full example in AN-03.
- Confirmed all plan steps are correct; flagged one implicit requirement in Step 2 (the `## Profile Resolution Rules` section, required by AC-1).
- Recorded all decisions in `## Architecture Notes` of the plan file (ADR-001 + AN-01 through AN-05).

All planner open questions are resolved. No blockers remain.

---

## Architecture Notes Reference

| Ref | What it means for you |
|-----|----------------------|
| **ADR-001** | You are implementing Option A (YAML frontmatter `roles:` block). Do not put profiles data in the markdown body. |
| **AN-01** | Exact frontmatter YAML you must produce for `team.md` (global). Also specifies the `## Profile Resolution Rules` markdown section you must add. |
| **AN-02** | Exact full file content for the workspace `team.md` at `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md`. Copy it verbatim. |
| **AN-03** | Separator format for `sk-team-dispatch` role prompts. Use `--- BEGIN PROFILE: [id] ---` / `--- END PROFILE: [id] ---`. |
| **AN-04** | `csharp-developer` profile is verified to exist. Step 7 may proceed. |
| **AN-05** | Per-file summary including the exact replacement text for `team-reviewer/AGENT.md` Rule 4. Use it. |

---

## Zero-Ambiguity Checklist for Developer

Work through Steps 2–7 in order. Each step refers to the AN note that specifies exactly what to write.

### Step 2 — Migrate `~/.agent-brains/teams/dev-team/team.md` to v2.0

1. Change `version: 1.0` → `version: 2.0` in the frontmatter.
2. Add the `roles:` block to the frontmatter exactly as shown in **AN-01**:
   ```yaml
   roles:
     planner:
       profiles: [team-planner]
     architect:
       profiles: [team-architect]
     developer:
       profiles: [base-developer, team-developer]
     reviewer:
       profiles: [base-developer, team-reviewer]
     qa:
       profiles: [team-qa]
   ```
3. In the markdown body's Roles table, update the `Profile` column header/annotation to indicate "see frontmatter `roles.[role].profiles`".
4. Append the `## Profile Resolution Rules` section to the markdown body exactly as shown in **AN-01**. This section is **required by AC-1** even though the checklist item doesn't name it.
5. Leave all other body sections (Handoff Rules, State Files, Skills Used, Cross-Provider Usage) unchanged.

### Step 3 — Update `~/.agent-brains/skills/team-start/team-start.md`

Rewrite **Step 2** of the procedure to note the skill must parse the v2.0 `profiles:` list from the `roles:` frontmatter block (not a single `Profile` string).

Rewrite **Step 3** of the procedure to the following algorithm:
1. Read `roles.[role].profiles` from the global `team.md` frontmatter. This is the base list.
2. Check for a workspace `team.md` at `./.agent-brains/teams/[team-name]/team.md`. If it exists and declares `roles.[role].profiles_append`, append those profile IDs to the list.
3. For each profile ID in the resolved list (in order): load `~/.agent-brains/profiles/[profile-id]/AGENT.md`. If a file does not exist, report an error and stop.
4. Concatenate all profile contents in order. The combined text is the effective rule set for this session.

Update the `=== Team Session ===` announce block: replace `(team-[role] profile active)` with `Profiles: [resolved profile IDs, comma-separated]`.

Update the Validation checklist: change "Role profile loaded (team-[role]/AGENT.md exists)" to "All role profiles in effective list loaded successfully."

### Step 4 — Update `~/.agent-brains/skills/team-dispatch/team-dispatch.md`

Rewrite **Step 4a** (Build the role prompt) to:
1. Resolve the effective profile list using the same L0 + `profiles_append` merge logic as Step 3 in `team-start` above.
2. Inline the full content of each profile file in resolved order. Wrap each with the **AN-03** separator:
   ```
   --- BEGIN PROFILE: [profile-id] ---
   [full verbatim content of ~/.agent-brains/profiles/[profile-id]/AGENT.md]
   --- END PROFILE: [profile-id] ---
   ```
   Leave a blank line between each END/BEGIN pair.
3. Update the existing instruction line to read: _"You are acting as the [role] on team [team-name]. Read your role profiles above (listed in order; last profile wins on any conflict). Complete all deliverables listed for your role…"_

Update the **Copilot CLI Notes** section: replace "the full contents of `~/.agent-brains/profiles/team-[role]/AGENT.md` (role mandate, responsibilities, rules)" with "the full text of all profiles in the effective list, inlined in order using `--- BEGIN PROFILE: [id] ---` / `--- END PROFILE: [id] ---` separators."

Update the **Validation checklist**: change "Each role agent given full inline context (profile + team config + handover memo)" to "Each role agent given full inline context (all profiles in effective list + team config + handover memo)."

### Step 5 — Clean `~/.agent-brains/profiles/team-developer/AGENT.md`

Remove the entire `## Base Profile` section. This is currently:
```
## Base Profile
All rules from the `base-developer` profile apply: security-first, SOLID, cross-platform scripting, safe-delete/rename, PR description format, and scope discipline.
```
Delete this section and the blank lines surrounding it. Do not alter any other content.

Verify no other sentence in the file references `base-developer` as a named rule source.

### Step 6 — Clean `~/.agent-brains/profiles/team-reviewer/AGENT.md`

In **Review Rule 4**, replace:
```
Apply all `base-developer` rules: naming, scope discipline, no TODO without a ticket, no broken references.
```
With:
```
Apply standard rules: naming conventions, scope discipline, no TODO comment without a tracking ticket, no broken file or symbol references.
```
The rules are preserved as explicit text; only the reference to `base-developer` as a named rules source is removed.

Verify no other sentence in the file references `base-developer` as a named rule source.

### Step 7 — Create workspace `team.md`

Create the file at `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md` with the **exact content from AN-02**:

```yaml
---
# Workspace override for dev-team — KubeTools4Dev
#
# This file EXTENDS the global ~/.agent-brains/teams/dev-team/team.md.
# Use profiles_append: to append profiles to the global list for a role.
# DO NOT use profiles: here — that replaces the global list entirely.
#
# Effective developer profile set in this workspace:
#   [base-developer, team-developer, csharp-developer]
#   (global: [base-developer, team-developer]  +  append: [csharp-developer])

id: dev-team
version: workspace-1.0
roles:
  developer:
    profiles_append: [csharp-developer]
---
```

No markdown body. No other content.

### Step 8 — Self-verify

Check each of the 8 Acceptance Criteria in the plan against the files you have modified. Then write `handover-developer.md`.

---

## No Open Questions

All planner-flagged open questions are resolved:

| Question | Resolution |
|----------|-----------|
| Does `csharp-developer` profile exist? | **Yes** — verified at `C:\Users\SupawatTanmanee\.agent-brains\profiles\csharp-developer\AGENT.md` (AN-04). Step 7 is unblocked. |
| What is the exact v2.0 `team.md` schema format? | **Decided** — Option A (YAML frontmatter `roles:` block). Full example in AN-01. |
| What is the profile concatenation separator for `sk-team-dispatch`? | **Decided** — `--- BEGIN PROFILE: [id] ---` / `--- END PROFILE: [id] ---`. Full example in AN-03. |
| What does the workspace `profiles_append` file look like? | **Decided** — Minimal YAML frontmatter only. Full example in AN-02. |
