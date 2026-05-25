# Plan: Team Profile Composition (v2.0)

**Status:** In Progress  
**Date:** 2026-05-21  
**Planned by:** team-planner  
**Task ID:** team-profile-composition

---

## Goal

Replace the single-profile-per-role assignment in the dev-team framework with a declarative, ordered list of profiles per role. This makes cross-profile inheritance mechanical and explicit rather than prose-based, and allows workspaces to safely augment the global profile list without replacing it.

---

## Scope

### In Scope
- Migrate `~/.agent-brains/teams/dev-team/team.md` to v2.0 schema with `profiles:` list per role.
- Update `~/.agent-brains/skills/team-start/team-start.md` to load and merge N profiles in order.
- Update `~/.agent-brains/skills/team-dispatch/team-dispatch.md` to inline all N profiles per role prompt.
- Remove informal "All base-developer rules apply" prose from `team-developer/AGENT.md`.
- Remove informal base-developer references from `team-reviewer/AGENT.md` (Rule 4 references "all `base-developer` rules" by name as a prose directive rather than a loaded profile).
- Audit `team-qa/AGENT.md` for any similar prose reference and remove if found.
- Create workspace-level `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md` as a working `profiles_append` example.

### Out of Scope
- Modifying any profile AGENT.md content other than removing the prose base-developer references.
- Creating new profiles (e.g., `csharp-developer` already exists; no new profiles are created here).
- Changes to any other skills beyond `team-start` and `team-dispatch`.
- Changes to any source code files in the repository.
- Modifying the `team-planner`, `team-architect` profiles (neither contains informal base-developer prose).

---

## Current State (read before implementing)

| File | Current State |
|------|--------------|
| `~/.agent-brains/teams/dev-team/team.md` | v1.0 — Roles table has single `Profile` string column per role |
| `~/.agent-brains/skills/team-start/team-start.md` | Step 3 loads exactly one profile file: `team-[role]/AGENT.md` |
| `~/.agent-brains/skills/team-dispatch/team-dispatch.md` | Step 4a inlines exactly one profile: `team-[role]/AGENT.md` |
| `~/.agent-brains/profiles/team-developer/AGENT.md` | Contains `## Base Profile` section with prose: "All rules from the `base-developer` profile apply…" |
| `~/.agent-brains/profiles/team-reviewer/AGENT.md` | Review Rule 4 references base-developer rules informally by name within the prose |
| `~/.agent-brains/profiles/team-qa/AGENT.md` | No base-developer prose found — no change required |
| `~/.agent-brains/profiles/base-developer/AGENT.md` | Exists and is the target profile to reference via the profiles list |

---

## Acceptance Criteria

1. **`team.md` (global) uses v2.0 format** — The frontmatter reads `version: 2.0` and each role entry uses a `profiles:` key containing an ordered list (not a single string). The collision rule ("last profile in the resolved list wins on conflict") is documented inline. The practical limit (≤ 4 profiles per role) is documented.

2. **`team-developer/AGENT.md` contains no base-developer prose** — The `## Base Profile` section and the sentence "All rules from the `base-developer` profile apply…" are absent. The `base-developer` profile is instead declared in the `profiles:` list for the developer role in `team.md`.

3. **`team-reviewer/AGENT.md` contains no informal base-developer directive** — Review Rule 4 no longer names `base-developer` as a rules source to "apply." Any enumerated rules that were sourced from base-developer may remain as explicit text but must not be framed as "apply all `base-developer` rules."

4. **`sk-team-start` resolves an effective profile list** — Step 3 of the procedure is updated to:
   - Read the `profiles:` list for the role from the global `team.md`.
   - If a workspace `team.md` exists and specifies `profiles_append:` for the same role, append those profiles to the list.
   - Load and concatenate all profile files in resolved order into one effective rule set for the session.

5. **`sk-team-dispatch` inlines all N profiles per role** — Step 4a is updated so the role prompt includes the full text of every profile in the resolved effective list, in order, not just the single `team-[role]/AGENT.md` file.

6. **A workspace can add profiles without replacing the global list** — The workspace `team.md` at `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md` is created, uses `profiles_append: [csharp-developer]` for the developer role, and contains a comment explaining that `profiles_append` extends rather than replaces the global list.

7. **Effective set is `[base-developer, team-developer, csharp-developer]` for developer in this workspace** — The global `team.md` declares `profiles: [base-developer, team-developer]` for developer, and the workspace appends `csharp-developer`, producing the documented three-profile effective set.

8. **`sk-team-start` announce step reflects multiple profiles** — The `=== Team Session ===` output block updated (or noted) to reflect the active profile set (e.g., `Profiles: base-developer, team-developer`) rather than a single profile name.

---

## Task Checklist

> **Order matters.** Complete steps in sequence. Each step is independently verifiable.

### Step 1 — Architect: Review and annotate the plan
- [ ] Read all six current files listed in Current State.
- [ ] Confirm the v2.0 YAML schema for `team.md` is unambiguous (profiles list, profiles_append key, collision rule wording, limit wording).
- [ ] Confirm the merge algorithm for `sk-team-start` and `sk-team-dispatch` is unambiguous (L0 list + workspace append = effective list, load in order, concatenate).
- [ ] Record any decisions or ambiguities as ADRs in this plan file under `## Architecture Notes`.
- [ ] Write `handover-architect.md`.

### Step 2 — Developer: Migrate `team.md` (global)
- [x] Bump frontmatter `version` from `1.0` to `2.0`.
- [x] Replace the Roles table's single `Profile` column with a structured block per role using `profiles:` list.
  - `planner`: `[team-planner]`
  - `architect`: `[team-architect]`
  - `developer`: `[base-developer, team-developer]`
  - `reviewer`: `[base-developer, team-reviewer]`
  - `qa`: `[team-qa]`
- [x] Add a `## Profile Resolution Rules` section documenting: (a) last profile wins on conflict, (b) ≤ 4 profiles per role limit, (c) `profiles` replaces, `profiles_append` extends.
- [x] Preserve all Handoff Rules, State Files, Skills Used, and Cross-Provider Usage sections unchanged.

### Step 3 — Developer: Update `team-start.md`
- [x] Rewrite Step 2 (Load team config) — clarify it must parse role entries with the v2.0 `profiles:` list format.
- [x] Rewrite Step 3 (Activate role profile) — expand to:
  1. Read `profiles:` list from the global `team.md` for the target role.
  2. If a workspace `team.md` exists for the same team and role, read its `profiles_append:` and append to the list.
  3. Load each profile file in order: `~/.agent-brains/profiles/[profile-id]/AGENT.md`.
  4. Concatenate all profile contents into one effective rule set active for the session.
- [x] Update the `=== Team Session ===` announce block to output `Profiles: [profile1, profile2, ...]` instead of `(team-[role] profile active)`.
- [x] Update the Validation checklist to reflect N-profile loading.

### Step 4 — Developer: Update `team-dispatch.md`
- [x] Rewrite Step 4a (Build the role prompt) to:
  1. Resolve the effective profile list using the same L0 + `profiles_append` merge logic described in Step 3 above.
  2. Inline the full text of each profile in the effective list, in order, separated by a visible header (e.g., `--- Profile: [profile-id] ---`).
- [x] Update the Copilot CLI Notes section to reference "all profiles in the effective list" instead of "the role profile."
- [x] Update the Validation checklist to reflect N-profile inlining.

### Step 5 — Developer: Clean `team-developer/AGENT.md`
- [x] Remove the `## Base Profile` section entirely (currently lines 29–31).
- [x] Verify no other sentence in the file refers to `base-developer` as a rule source.

### Step 6 — Developer: Clean `team-reviewer/AGENT.md`
- [x] In Review Rule 4, remove the phrase that names `base-developer` as a rules source (currently: "Apply all `base-developer` rules: naming, scope discipline, no TODO without a ticket, no broken references.").
- [x] Replace with a concrete, self-contained rule statement that does not reference a profile by name (the rules themselves may remain as text).
- [x] Verify no other sentence in the file refers to `base-developer` as a rule source.

### Step 7 — Developer: Create workspace `team.md`
- [x] Create `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md`.
- [x] Include frontmatter with `id: dev-team`, version noting it is a workspace override.
- [x] Include a `roles:` section with a single entry: developer role using `profiles_append: [csharp-developer]`.
- [x] Include a comment block explaining this file extends (not replaces) the global list, making the effective developer profile set `[base-developer, team-developer, csharp-developer]`.
- [x] Do NOT duplicate the full role table from the global `team.md` — this file should only declare overrides.

### Step 8 — Developer: Self-verify acceptance criteria
- [x] Re-read all modified files and check each AC item (1–8) is satisfied.
- [x] Run `sk-test-gate` (documentation-only changes may not have automated tests; verify manually by tracing the procedure steps against the AC).
- [x] Write `handover-developer.md`.

---

## Dependencies

| Dependency | Direction | Notes |
|------------|-----------|-------|
| `~/.agent-brains/profiles/csharp-developer/AGENT.md` | Must exist before workspace `team.md` is valid | Verify it exists before Step 7; do not create it as part of this task |
| Global `team.md` (Step 2) | Must complete before Steps 3 & 4 | `team-start` and `team-dispatch` procedures reference the v2.0 schema; they must align |
| `team-developer/AGENT.md` cleanup (Step 5) | Independent of Steps 3 & 4 | Can be done in parallel with skill updates |
| `team-reviewer/AGENT.md` cleanup (Step 6) | Independent of Steps 3 & 4 | Can be done in parallel with skill updates |

---

## Architecture Notes

_Filled in by the Architect — 2026-05-21_

---

### ADR-001: v2.0 `team.md` Schema Format

**Status:** Accepted  
**Decision:** Option A — Extend YAML frontmatter with a `roles:` block containing per-role `profiles:` lists.

**Alternatives considered:**
- **Option B** (markdown body YAML code blocks per role): requires agents to parse markdown _and_ YAML code blocks instead of a single frontmatter document; more fragile and less consistent with the existing pattern.
- **Option C** (structured markdown sections per role): least machine-parseable; prose-friendly only; no standard parsing path.

**Rationale:** The current `team.md` frontmatter is already the authoritative machine-readable section (`id`, `name`, `version`). Extending it with `roles:` keeps all parseable config in one place. YAML natively supports nested lists. Agents loading a team config parse frontmatter first; the markdown body remains human-readable documentation. This is the minimal-change path: the body retains all existing sections; only the Roles table annotation and a new `## Profile Resolution Rules` section are added.

**Risk:** YAML comments in frontmatter may be stripped by strict parsers. Mitigation: collision rule and profile limit are documented in a `## Profile Resolution Rules` markdown section in the body, not as YAML comments.

---

### AN-01: v2.0 Global `team.md` Schema

The `roles:` block in YAML frontmatter replaces the single `Profile` string column. Each role entry declares a `profiles:` key with an ordered YAML list. Profiles are applied left-to-right; the last entry wins on any conflicting rule.

**Concrete example — full migrated frontmatter:**

```yaml
---
id: dev-team
name: Software Development Team
version: 2.0
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
---
```

The markdown body retains all existing sections unchanged (Handoff Rules, State Files, Skills Used, Cross-Provider Usage). The Roles table `Profile` column annotation is updated to `see frontmatter roles.[role].profiles`. A new section is appended to the body:

```markdown
## Profile Resolution Rules

- **Load order:** Profiles are applied left to right. The **last** profile in the resolved list wins on any conflicting rule.
- **Limit:** No role may declare more than **4 profiles** in the resolved list (global `profiles:` + workspace `profiles_append:` combined).
- **`profiles:` is the full global list.** Declaring `profiles:` for a role in the global `team.md` fully specifies that role's base profile set.
- **`profiles_append:` extends, never replaces.** A workspace override using `profiles_append:` appends to the global `profiles:` list. It cannot remove or reorder global entries.
```

> **Plan annotation (Step 2):** The checklist does not explicitly name the `## Profile Resolution Rules` section. Acceptance Criterion 1 requires it ("collision rule documented inline", "practical limit documented"). The Developer must add this section as part of Step 2. It is not a new scope item — it is the implementation detail of AC-1.

---

### AN-02: Workspace Override `team.md` Format

Workspace override files must be **minimal** — only overrides, no duplication of global configuration. Use `profiles_append:` per role. Do **not** use `profiles:` in a workspace file; that key is reserved for the global file and would replace (not extend) the global list.

**Concrete example — full content of `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md`:**

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

No markdown body is required. The file is purely declarative. All human-readable documentation lives in the global `team.md`.

---

### AN-03: Profile Concatenation Separator for `sk-team-dispatch`

When `sk-team-dispatch` builds a role prompt that inlines N profiles, each profile's content is wrapped with explicit `BEGIN`/`END` markers:

```
--- BEGIN PROFILE: [profile-id] ---
[full verbatim content of ~/.agent-brains/profiles/[profile-id]/AGENT.md]
--- END PROFILE: [profile-id] ---
```

**Rationale:**
- Triple-dash prefix is visually distinct from `##` headings inside profile content and is not confused with YAML frontmatter delimiters (which appear at document boundaries, not mid-text).
- Paired `BEGIN`/`END` labels make boundaries unambiguous even in non-markdown-rendering providers.
- The `profile-id` in the header allows the agent (and any human reader) to attribute rules to a specific source profile.

**Example — developer role prompt structure in KubeTools4Dev:**

```
--- BEGIN PROFILE: base-developer ---
[full content of base-developer/AGENT.md]
--- END PROFILE: base-developer ---

--- BEGIN PROFILE: team-developer ---
[full content of team-developer/AGENT.md]
--- END PROFILE: team-developer ---

--- BEGIN PROFILE: csharp-developer ---
[full content of csharp-developer/AGENT.md]
--- END PROFILE: csharp-developer ---
```

The profiles are emitted in resolved list order (global `profiles:` list first, then `profiles_append:` items in declaration order).

---

### AN-04: `csharp-developer` Profile Existence Verification

**Result: VERIFIED ✅**

`C:\Users\SupawatTanmanee\.agent-brains\profiles\csharp-developer\AGENT.md` exists. The file is substantive (≈185 lines) covering .NET 10 / C# coding standards, naming conventions, XML documentation requirements, nullable reference types, async patterns, memory discipline, bulk-insert rules, security, and xUnit testing conventions.

Step 7 (create workspace `team.md` with `profiles_append: [csharp-developer]`) is **unblocked**. No new profiles need to be created as part of this task.

---

### AN-05: Per-File Change Summary

The plan steps are correct. The table below confirms each file, the required change, and any architectural clarification the Developer needs.

| Step | File | Required Change | Notes |
|------|------|----------------|-------|
| 2 | `~/.agent-brains/teams/dev-team/team.md` | Bump `version: 1.0` → `2.0`; add `roles:` block to frontmatter per AN-01; update Roles table annotation; add `## Profile Resolution Rules` section | `## Profile Resolution Rules` is required by AC-1 even though the Step 2 checklist doesn't name it explicitly |
| 3 | `~/.agent-brains/skills/team-start/team-start.md` | Rewrite Step 2 (parse v2.0 `profiles:` list) and Step 3 (N-profile resolution: global list + `profiles_append` append, load in order, concatenate); update `=== Team Session ===` announce block to `Profiles: [p1, p2, ...]`; update validation checklist | Resolution algorithm: (1) read global `profiles:` for role; (2) if workspace `team.md` exists and has `profiles_append:` for role, append; (3) load each file; (4) concatenate with AN-03 separators for display |
| 4 | `~/.agent-brains/skills/team-dispatch/team-dispatch.md` | Rewrite Step 4a to resolve effective profile list and inline all N profiles using AN-03 `--- BEGIN PROFILE: [id] ---` / `--- END PROFILE: [id] ---` separators; update Copilot CLI Notes to reference "all profiles in the effective list"; update validation checklist | The same L0 + `profiles_append` merge logic from Step 3 applies here |
| 5 | `~/.agent-brains/profiles/team-developer/AGENT.md` | Remove the entire `## Base Profile` section ("All rules from the `base-developer` profile apply…") | No other changes to this file; the rules themselves now come from the `base-developer` profile being in the `profiles:` list |
| 6 | `~/.agent-brains/profiles/team-reviewer/AGENT.md` | In Review Rule 4: replace `"Apply all \`base-developer\` rules: naming, scope discipline, no TODO without a ticket, no broken references."` with the self-contained text: `"Apply standard rules: naming conventions, scope discipline, no TODO comment without a tracking ticket, no broken file or symbol references."` | The rules are preserved as explicit text; only the reference to `base-developer` as a named rules source is removed |
| 7 | `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md` | Create new file per AN-02 exact format | File content is fully specified in AN-02; no markdown body needed |

---

## Deviations

_(To be filled in by the Developer if any plan item is adjusted during implementation.)_
