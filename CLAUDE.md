<!-- begin:framework -->
# Mandate: Centralized Brains (Claude Code)
1. Read the global framework rules from `~\.agent-brains\GLOBAL_AGENT.md`.
2. Read the local workspace directives from `.\.agent-brains\AGENT.md`.
3. Use `.\.agent-brains\memory\` for project context.
4. Always write plans to `.\.agent-brains\plan\` BEFORE writing code.

## Agent-Brains Skill Invocation

When a user invokes a skill by name, resolve it using the [SK] entries in the session
context banner — do NOT use the built-in Skill tool. Resolution paths:
- [SK] global:<id>          -> `~\.agent-brains\skills\<id>\<id>.md`
- [SK] profile(<name>):<id> -> `~\.agent-brains\profiles\<name>\skills\<id>\<id>.md`
- [SK] workspace:<id>       -> `.\.agent-brains\skills\<id>\<id>.md`

Read the file and execute its Procedure section. Innermost level wins on ID collision
(workspace > profile > global).

## Automatic Session Start

At the beginning of every session — on the first user message — automatically execute
the session-start skill without waiting to be asked.
<!-- end:framework -->
