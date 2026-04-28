---
version: 1.0
profiles:
  - base-developer
  - csharp-developer
  - kubernetes-devops
strict_override: false
---

# Workspace Instructions

## Overview
KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# for managing Kubernetes resources.

## Workspace Rules
- **System Constraint: Filesystem-First State**: You are FORBIDDEN from executing code changes until a corresponding plan exists in \`./.agent-brains/plan/\`.
- **Distributed Planning**: Roadmap in \`backlog.md\`, detailed checklists in dedicated atomic files.
- **Automatic Handover**: Execute handover and provide \"Handover Memo\" at session end.
- Ensure all skills added to `./.agent-brains/skills/` are provider-agnostic.
- Use `./.agent-brains/memory/` to store shared context about the project.
- Use `./.agent-brains/plan/` to track task execution.