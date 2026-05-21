# Handover: Planner → Architect

**Team:** dev-team  
**Task:** team-profile-composition  
**Date:** 2026-05-21  
**From:** planner  
**To:** architect

---

## What Was Done

- Read all six existing files: `team.md` (global), `team-start.md`, `team-dispatch.md`, `team-developer/AGENT.md`, `team-reviewer/AGENT.md`, `team-qa/AGENT.md`, and `base-developer/AGENT.md`.
- Produced `plan/team-profile-composition.md` with goal, scope, 8 acceptance criteria, 8-step ordered task checklist, and dependency table.
- Updated `plan/backlog.md` — moved "Profile composition per role" from Pending to In Progress.

---

## Decisions Made (Planner-level, scope only)

- `team-qa/AGENT.md` requires **no change** — no informal base-developer prose was found in that file.
- `profiles_append` workspace file for this workspace (`R:\DevDogs\KubeTools4Dev`) is **in scope** as a working example; the Architect should verify `csharp-developer` profile exists at `~/.agent-brains/profiles/csharp-developer/AGENT.md` before the Developer creates it.
- The `team-reviewer/AGENT.md` change (Step 6) is **surgical**: only the reference naming `base-developer` as a rule source is removed. The actual rules listed in Rule 4 (naming, scope discipline, etc.) survive as explicit text.
- All changes are to **global `~/.agent-brains/` files** and the workspace `team.md`. No repository source code is touched.

---

## Open Questions / Blockers

- **Does `~/.agent-brains/profiles/csharp-developer/AGENT.md` exist?** The Architect must verify this. If it does not exist, Step 7 (workspace team.md) must reference a profile that does exist, or the Architect must flag it and add a note to the plan.
- **v2.0 schema format**: The plan specifies the required fields (`profiles:`, `profiles_append:`, collision rule, limit) but leaves the exact markdown/YAML formatting decision to the Architect. An ADR is expected.

---

## Expected From Next Role (Architect)

- Verify `csharp-developer` profile exists; record finding in the plan's `## Architecture Notes` section.
- Decide and document the exact v2.0 `team.md` format (pure YAML frontmatter vs. YAML code block in markdown body vs. structured markdown table with `profiles:` as a sub-row).
- Decide the exact concatenation separator format for N profiles in `sk-team-dispatch` role prompts.
- Write at least one ADR if any format decision is non-obvious.
- Write `handover-architect.md`.

---

## Key Files

- `R:\DevDogs\KubeTools4Dev\.agent-brains\plan\team-profile-composition.md` — active plan with full checklist
- `R:\DevDogs\KubeTools4Dev\.agent-brains\plan\backlog.md` — updated
- `C:\Users\SupawatTanmanee\.agent-brains\teams\dev-team\team.md` — global team config to migrate (v1.0 → v2.0)
- `C:\Users\SupawatTanmanee\.agent-brains\skills\team-start\team-start.md` — skill to update (Step 3 single→N profiles)
- `C:\Users\SupawatTanmanee\.agent-brains\skills\team-dispatch\team-dispatch.md` — skill to update (Step 4a single→N profiles)
- `C:\Users\SupawatTanmanee\.agent-brains\profiles\team-developer\AGENT.md` — remove `## Base Profile` section
- `C:\Users\SupawatTanmanee\.agent-brains\profiles\team-reviewer\AGENT.md` — remove base-developer reference in Rule 4
- `R:\DevDogs\KubeTools4Dev\.agent-brains\teams\dev-team\team.md` — create as workspace override example
