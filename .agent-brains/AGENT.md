---
version: 1.0
profiles:
  - base-developer
  - csharp-developer
strict_override: false
---

# Workspace Instructions

## Overview
KubeTools4Dev is a cross-platform desktop application built with Avalonia UI and C# for managing Kubernetes resources.

## Workspace Rules
- Ensure all skills added to `./.agent-brains/skills/` are provider-agnostic.
- Use `./.agent-brains/memory/` to store shared context about the project.
- Use `./.agent-brains/plan/` to track task execution.