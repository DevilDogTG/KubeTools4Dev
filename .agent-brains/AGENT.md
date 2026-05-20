---
version: 1.1
profiles:
  - base-developer
  - csharp-developer
  - github-scm
  - kubernetes-devops
strict_override: false
---

# Workspace Instructions

## Overview
KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# (.NET 10) for managing Kubernetes resources.

## System Constraints
- **Filesystem-First Planning**: You are FORBIDDEN from executing code changes until a corresponding plan exists in `./.agent-brains/plan/`.
- **Distributed Planning**: High-level roadmap in `backlog.md`; detailed checklists in dedicated atomic plan files.
- **Automatic Handover**: Execute `project-handover` skill and provide Handover Memo at session end.

# Mandate: Filesystem-First Planning
You MUST NOT keep your plans in internal context only.
1. Read \`./.agent-brains/AGENT.md\`.
2. Write plan to \`./.agent-brains/plan/\` BEFORE writing code.
3. Update memory/overview.md at session end.

## Workspace Rules
- At the start of every new session, I must scan and briefly summarize the active rules and profiles from all loaded context levels (Global, Profile, and Workspace).
- I must explicitly confirm that I am aligned with the "Filesystem-First Planning" mandate if it is present in the workspace.
- Ensure all skills added to `./.agent-brains/skills/` are provider-agnostic.
- Use `./.agent-brains/memory/` to store shared context about the project.
- Use `./.agent-brains/plan/` to track task execution.

## Git Workflow
- Always create a new branch (`feature/` or `bugfix/` prefix) before making any code changes.
- Use the `finish-feature` skill to run preflights and create/update PRs.
- Use the `pr-review` skill to run an AI code review and post findings on a PR.
- Never commit directly to `main`.

## Coding Standards
- Build must have **0 warnings, 0 errors** (`-warnaserror`).
- Use `[LoggerMessage]` source-generated methods for structured logging.

## Testing
- Test project: `src/KubeTools4Dev.Core.Tests` (xUnit + NSubstitute, net10.0).
- Run: `dotnet test src/KubeTools4Dev.Core.Tests/KubeTools4Dev.Core.Tests.csproj`
- All tests must pass before finishing a feature.

## PR Comment Contracts
- `finish-feature` skill posts `<!-- finish-feature-update -->` + `<!-- head-sha: SHA -->` on PRs.
- `pr-review` skill detects these markers → reviews diff → posts `<!-- pr-review-findings -->` + `<!-- review-status: approved|needs-work -->` + `<!-- review-sha: SHA -->`.
