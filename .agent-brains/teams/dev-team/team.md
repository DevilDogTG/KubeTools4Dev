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
