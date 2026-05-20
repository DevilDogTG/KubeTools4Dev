# Plan: PoC Agent Team Dispatch — Deployments Page

## Problem Statement
Validate the `sk-team-dispatch` automated pipeline by running the full
planner → architect → developer → reviewer → qa sequence on a real KubeTools4Dev feature.

The feature to build: **Deployments page** — a new screen listing Kubernetes Deployments
with rollout actions (restart, replica count edit, image tag edit).

Two gaps block this PoC today:
1. `sk-team-dispatch` is declared **Claude Code only** — Copilot CLI uses a `task` tool for
   sub-agents. The skill must be updated to support Copilot CLI.
2. The workspace has no `.agent-brains/teams/dev-team/` directory for state files.

## Approach
1. Infrastructure setup — Create workspace team state directory.
2. Improve `sk-team-dispatch` — Extend it to support Copilot CLI via the `task` tool.
3. Run the dispatch — Execute the full pipeline on the Deployments page feature.

## Scope

### In Scope
- Update `~/.agent-brains/skills/team-dispatch/team-dispatch.md` to add Copilot CLI support.
- Create `.agent-brains/teams/dev-team/` in this workspace (✓ done).
- Run dispatch end-to-end.

### Out of Scope
- Changes to role profiles — they are sufficient.
- Changes to `sk-team-start` or `sk-team-handover` — adequate for the PoC.
- Infrastructure beyond replicas + image tag editing.

## Feature Acceptance Criteria (Deployments Page)
The system must:
1. Display a list of all Deployments in the selected cluster/namespace in the sidebar.
2. Show per-deployment: name, namespace, desired/ready/available replicas, image tag(s).
3. Provide a "Rollout Restart" action per deployment.
4. Provide an "Edit" action: change replica count (int ≥ 0) and image tag (free text).
5. Edit action must apply changes via the Kubernetes API (patch deployment spec).
6. All actions must surface errors via a notification/message.
7. Build: 0 warnings, 0 errors (`-warnaserror`).
8. Unit tests for new ViewModel and Service logic.

## Task Checklist

### Phase 1 — Infrastructure
- [x] Create `.agent-brains/teams/dev-team/` directory in workspace.

### Phase 2 — Improve sk-team-dispatch
- [x] Add Copilot CLI procedure to `team-dispatch.md` (use `task` tool, general-purpose background agents).
- [x] Update `compatibility` front-matter to include `copilot`.
- [x] Validate dispatch flow still gates on handover memo existence.

### Phase 3 — Dispatch the pipeline
- [ ] Confirm updated skill.
- [ ] Run dispatch for the Deployments page feature.
- [ ] Verify all 5 handover memos are produced.
- [ ] Confirm QA PASS before merge.

## Progress Log
- 2026-05-20: Plan created by Planner role (PoC session). Infrastructure directory created.
