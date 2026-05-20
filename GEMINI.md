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
Project-specific context and local overrides for KubeTools4Dev.

# Mandate: Centralized Brains (Gemini)
1. Read the global framework rules from `~/.agent-brains/GLOBAL_AGENT.md`.
2. Read the local workspace directives from `./.agent-brains/AGENT.md`.
3. Use `./.agent-brains/memory/` for project context.
4. Always write plans to `./.agent-brains/plan/` BEFORE writing code.

## Workspace Rules
- At the start of every new session, I must scan and briefly summarize the active rules and profiles from all loaded context levels (Global, Profile, and Workspace).
- I must explicitly confirm that I am aligned with the "Filesystem-First Planning" mandate if it is present in the workspace.
- Follow the framework mechanics defined in `~\.agent-brains\GLOBAL_AGENT.md`.
- Maintain operational state strictly in .\.agent-brains\.
