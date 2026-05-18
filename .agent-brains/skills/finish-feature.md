---
id: finish-feature
name: Finish Feature (PR Creation / Update)
version: 1.0
compatibility: [claude, gemini, copilot, codex]
---

# Skill: Finish Feature (PR Creation / Update)

## Context
Invoke this skill when the user is ready to open a new PR or update an existing one for the
current feature branch. It wraps `scripts/finish-feature.ps1`, which runs the full preflight
pipeline and handles PR creation/update automatically.

Do not invoke on protected branches (`main`, `develop`, `release/*`). Per global §6.7, always
confirm with the user before running — never trigger a PR action automatically.

## Procedure

### Phase 1 — Pre-flight Confirmation

1. **Verify branch safety**:
   - Confirm current branch is NOT `main`, `develop`, or `release/*`.
   - If on a protected branch, stop and inform the user.

2. **Confirm intent with the user**:
   > "Ready to run `finish-feature` on branch `[branch-name]`? This will rebase from main,
   > run build + tests, then create or update the PR. Proceed? (yes / cancel)"
   - Do not proceed without explicit confirmation.

3. **Identify any skip flags needed** — ask if relevant:
   - `-SkipRebase` — use only if user explicitly asks to skip the rebase step.
   - `-SkipTests` — use only if user explicitly asks to skip the test gate.
   - `-Draft` — use if the PR should be opened as a draft.
   - `-Provider` — defaults to `Copilot`; offer `Gemini` as alternative if Copilot is unavailable.

### Phase 2 — Execute

4. **Run the script**:
   ```powershell
   .\scripts\finish-feature.ps1
   ```
   With optional flags as determined in Phase 1. Examples:
   ```powershell
   .\scripts\finish-feature.ps1 -Draft
   .\scripts\finish-feature.ps1 -Provider Gemini
   .\scripts\finish-feature.ps1 -SkipRebase -SkipTests   # emergency only
   ```

5. **Monitor output** for the preflight stages:
   | Stage | Expected |
   |-------|----------|
   | Clean tree | No uncommitted changes |
   | Commits ahead | At least 1 commit not in main |
   | Rebase | Branch up-to-date with origin/main |
   | Build | 0 warnings, 0 errors (`-warnaserror`) |
   | Tests | All xUnit tests pass |

6. **On failure**: read the error, diagnose the root cause, and fix it before re-running.
   Do not use skip flags to bypass failures unless explicitly asked.

### Phase 3 — Post-run

7. **Report the outcome**:
   - First run: PR URL created.
   - Re-run: update comment posted (new commits + AI summary + hidden SHA marker).

8. **Update the active plan file** with a dated progress log entry noting the PR was opened or updated.

## Validation
- [ ] Branch is not protected.
- [ ] User confirmed before execution.
- [ ] All preflight stages passed.
- [ ] PR URL returned (first run) or update comment posted (re-run).
- [ ] Active plan file updated.
