---
id: finish-feature
name: Finish Feature (PR Creation / Update)
version: 2.0
compatibility: [claude, gemini, copilot, codex]
---

# Skill: Finish Feature (PR Creation / Update)

## Context
Invoke this skill when the user is ready to open a new PR or update an existing one for the
current feature branch. The agent executes all steps directly — no external script dependency.
The agent generates PR text itself; no AI CLI tools (gh copilot, gemini) are needed.

Do not invoke on protected branches (`main`, `develop`, `release/*`). Per global §6.7, always
confirm with the user before running — never trigger a PR action automatically.

## Procedure

### Phase 1 — Pre-flight Confirmation

1. **Verify branch safety**:
   ```powershell
   git branch --show-current
   ```
   If result is `main`, `develop`, or matches `release/*` — stop and inform the user.

2. **Confirm intent with the user**:
   > "Ready to run `finish-feature` on branch `[branch-name]`? This will check clean tree,
   > rebase from main, run build + tests, then create or update the PR. Proceed? (yes / cancel)"
   Do not proceed without explicit confirmation.

3. **Identify any skip flags needed** — ask if relevant:
   - `-SkipRebase` — skip the rebase step (e.g., rebase already done manually)
   - `-SkipTests` — skip the test gate (emergency only; must be justified)
   - `--draft` — open PR as draft (default per github-scm profile)

---

### Phase 2 — Clean Tree Check

4. Run:
   ```powershell
   git status --porcelain
   ```
   If output is non-empty — stop. Tell the user to commit or stash changes before continuing.

---

### Phase 3 — Commits-Ahead Check

5. Run:
   ```powershell
   git log main..HEAD --oneline
   ```
   If output is empty — stop. Nothing to PR; branch has no commits ahead of `main`.

---

### Phase 4 — Rebase from Main (skippable with `-SkipRebase`)

6. Fetch latest:
   ```powershell
   git fetch origin main
   ```

7. Check if behind:
   ```powershell
   git rev-list HEAD..origin/main --count
   ```
   If count > 0:
   ```powershell
   git rebase origin/main
   ```
   On conflict — stop, surface the conflict, ask user to resolve then re-run.

8. Push rebased branch:
   ```powershell
   git push --force-with-lease origin <branch>
   ```
   (Only needed when rebase was performed.)

---

### Phase 5 — Build Gate

9. Find the solution file:
   ```powershell
   Get-ChildItem -Recurse -Filter "*.sln" | Select-Object -First 1
   ```

10. Run build:
    ```powershell
    dotnet build <sln-path> -warnaserror
    ```
    Must exit 0 with 0 warnings, 0 errors. On failure — surface errors, stop.

---

### Phase 6 — Test Gate

11. Invoke the `test-gate` skill, or run directly:
    ```powershell
    dotnet test <sln-path> --no-build --logger "console;verbosity=minimal"
    ```
    All tests must pass. On failure — surface failing test names, stop (unless `-SkipTests`).

---

### Phase 7 — PR Create or Update

12. Check for an existing open PR:
    ```powershell
    gh pr list --head <branch> --json number,state --jq ".[0]"
    ```

#### If no PR exists → Create

13. Gather context:
    ```powershell
    git log main..HEAD --oneline          # short log
    git log main..HEAD --pretty=format:"%h %s%n%b"  # full log
    git diff --stat main..HEAD            # stat
    git diff main..HEAD                   # full diff (cap display at ~200 lines)
    ```

14. **Agent writes the PR body** using this template:

    ```markdown
    ## Overview
    [2–3 sentences: purpose and impact of this PR]

    ## What's Changed
    [Grouped by area — UI, Core, Config, etc. Bullet points. Specific file/class/method names.]

    ## Files Changed
    | File | Change | Description |
    |------|--------|-------------|
    | ...  | New/Modified/Deleted | ... |

    ## Testing
    [How to verify: commands to run, what to check]

    > No related issue. / Closes #N

    <!-- finish-feature-update -->
    <!-- head-sha: <HEAD-SHA> -->
    ```

15. Save body to a temp file and create the PR:
    ```powershell
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, $prBody, [System.Text.UTF8Encoding]::new($false))
    gh pr create --draft --title "<type>(<scope>): <summary>" --body-file $tmp
    Remove-Item $tmp
    ```

16. Post initial SHA marker comment (enables accurate diffs on re-run):
    ```powershell
    $prNum = gh pr list --head <branch> --json number --jq ".[0].number"
    $marker = "<!-- finish-feature-update -->`n<!-- head-sha: <HEAD-SHA> -->"
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, $marker, [System.Text.UTF8Encoding]::new($false))
    gh pr comment $prNum --body-file $tmp
    Remove-Item $tmp
    ```

#### If PR already exists → Update comment

17. Read comments to find last `<!-- head-sha: SHA -->` marker:
    ```powershell
    gh pr view <prNum> --json comments --jq ".comments[].body"
    ```
    Parse the last `finish-feature-update` comment for `<!-- head-sha: SHA -->`.
    Use that SHA as the base for the diff. Fall back to `main` if not found.

18. Get new commits since that SHA:
    ```powershell
    git log <baseSHA>..HEAD --oneline
    ```
    If empty — no new commits since last update, exit cleanly without posting duplicate.

19. **Agent writes an update comment**:

    ```markdown
    ## Update — [timestamp]

    ### Preflight Results
    - Build: ✅ 0 warnings, 0 errors
    - Tests: ✅ N passed

    ### New Commits
    - `<sha> <message>`
    - ...

    ### Summary
    [Agent summary of what changed in the new commits]

    <!-- finish-feature-update -->
    <!-- head-sha: <HEAD-SHA> -->
    ```

20. Post the comment:
    ```powershell
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, $commentBody, [System.Text.UTF8Encoding]::new($false))
    gh pr comment <prNum> --body-file $tmp
    Remove-Item $tmp
    ```

---

### Phase 8 — Post-run

21. Report outcome to the user:
    - First run: PR URL and draft status.
    - Re-run: update comment URL posted.

22. Update the active plan file with a dated progress log entry noting the PR was opened or updated.

---

## Validation
- [ ] Branch is not protected.
- [ ] User confirmed before execution.
- [ ] Clean tree confirmed.
- [ ] At least one commit ahead of `main`.
- [ ] Rebase completed (or skipped with justification).
- [ ] Build: 0 warnings, 0 errors.
- [ ] Tests: all passed (or risk accepted and logged).
- [ ] PR URL returned (first run) or update comment posted (re-run).
- [ ] Active plan file updated with progress log entry.
