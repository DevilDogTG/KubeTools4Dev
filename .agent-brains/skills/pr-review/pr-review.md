---
id: pr-review
name: PR Code Review
version: 1.0
compatibility: [claude, gemini, copilot, codex]
---

# Skill: PR Code Review

## Context
Invoke this skill to run an AI code review on the current branch's open or draft PR and post
findings as a structured GitHub comment. The agent performs the review directly — no external
AI CLI tools are needed. Applies the global `code-review` skill rules plus KubeTools4Dev-specific
coding standards.

Per global §6.7, do not invoke automatically. Wait for explicit user confirmation or invocation.

## Procedure

### Phase 1 — Preflight

1. Confirm `gh` CLI is available:
   ```powershell
   gh --version
   ```
   If not found — stop and inform the user.

2. Confirm current branch is not protected:
   ```powershell
   git branch --show-current
   ```
   If `main`, `develop`, or `release/*` — stop.

3. Find an open or draft PR for this branch:
   ```powershell
   gh pr list --head <branch> --state open --json number,title,isDraft,url
   ```
   If none found — stop. Tell user to run `finish-feature` first.

4. Get current HEAD SHA:
   ```powershell
   git rev-parse HEAD
   ```

---

### Phase 2 — Detect Review State

5. Read all PR comments:
   ```powershell
   gh pr view <prNum> --json comments --jq ".comments[].body"
   ```

6. Extract **base SHA** from the latest `finish-feature-update` comment:
   - Search for `<!-- head-sha: SHA -->` in comments that contain `finish-feature-update`.
   - Use the last matching SHA as the diff base.
   - Fall back to `main` if no marker found.

7. Extract **last review state** from the latest `pr-review-findings` comment:
   - `<!-- review-sha: SHA -->` — SHA when last review ran.
   - `<!-- review-status: approved|needs-work -->` — last status.

8. Apply skip guards (unless `-Force` is set):
   - **Already approved at HEAD**: `review-status = approved` AND `review-sha = HEAD` → exit cleanly.
   - **Needs-work at HEAD, no dev reply**: `review-status = needs-work` AND `review-sha = HEAD`
     AND no non-bot comments after the last review comment → inform user, exit cleanly.
     ("Reply to the review comment, push new commits, or re-invoke with `-Force`.")

---

### Phase 3 — Gather Diff Context

9. Collect review material:
   ```powershell
   git log <baseSHA>..HEAD --oneline              # short log
   git log <baseSHA>..HEAD --pretty=format:"%h %s%n%b"  # full log
   git diff --stat <baseSHA>..HEAD               # stat
   git diff <baseSHA>..HEAD                      # full diff
   ```
   If short log is empty — nothing to review between base and HEAD; exit cleanly.

---

### Phase 4 — Agent Code Review

10. Apply the global `code-review` skill (all 7 check areas), **plus** enforce these
    KubeTools4Dev-specific standards:

    | Standard | Rule |
    |----------|------|
    | Primary constructors | All classes must use primary constructor syntax |
    | Structured logging | All logging via `[LoggerMessage]` source-generated methods (no `_logger.LogXxx(...)` string interpolation) |
    | Nullable refs | NRTs enabled — no unchecked null dereferences; all nullable paths handled |
    | XML documentation | Required on all `public` types and members (CS1591 enforced) |
    | Build cleanliness | 0 warnings, 0 errors (built with `-warnaserror`) |
    | Test coverage | All new logic must have xUnit + NSubstitute tests |
    | DI lifetimes | `Singleton` for services/VMs registered in App.axaml.cs; `Transient` for `PodListViewModel`, `ServiceListViewModel` |
    | Async patterns | Kubernetes watch streams use `IAsyncEnumerable<(WatchEventType, T)>` |

11. Categorise every finding:
    - **🔴 Critical** — bugs, null-ref risks, broken async, incorrect DI lifetimes, security flaws. Blocks merge.
    - **🟡 Warning** — missing XML docs, missing tests, standards deviation, performance concerns. Should fix.
    - **🔵 Info** — naming suggestions, optional refactors. Non-blocking.

---

### Phase 5 — Post Review Comment

12. Determine status:
    - `approved` → only 🔵 findings or none.
    - `needs-work` → any 🔴 or 🟡 findings.

13. **Agent writes the review comment** using this template:

    ```markdown
    ## ✅ Code Review — Approved   <!-- OR: ## 🔍 Code Review — Needs Work -->

    _Reviewed commits: **<baseSHA-short> → <HEAD-short>** at <timestamp>_

    ---

    ### 🔴 Critical Issues
    [numbered list, or: _None found._]

    ### 🟡 Warnings
    [numbered list, or: _None found._]

    ### 🔵 Info / Suggestions
    [numbered list, or: _None found._]

    ### ✅ Summary
    [2–4 sentences: overall quality, safe to merge or needs work]

    ---

    **Status**: ✅ Approved — ready to merge.
    <!-- OR: **Status**: 🔄 Needs work — address findings above and re-invoke `pr-review`. -->

    <!-- pr-review-findings -->
    <!-- review-status: approved|needs-work -->
    <!-- review-sha: <HEAD-SHA> -->
    ```

    If `needs-work`, append:
    ```markdown
    ### What to do next
    1. Address all 🔴 Critical issues (required before merge).
    2. Address or acknowledge 🟡 Warnings.
    3. Re-invoke the `pr-review` skill after pushing fixes, or reply to this comment explaining decisions.
    ```

14. Post comment:
    ```powershell
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, $reviewComment, [System.Text.UTF8Encoding]::new($false))
    gh pr comment <prNum> --body-file $tmp
    Remove-Item $tmp
    ```

---

## Validation
- [ ] `gh` CLI present and authenticated.
- [ ] Branch is not protected.
- [ ] Open/draft PR found.
- [ ] Diff gathered (base SHA detected or fallback to `main`).
- [ ] All 7 `code-review` check areas completed + KubeTools4Dev standards applied.
- [ ] Review comment posted with correct hidden markers.
- [ ] Status is `approved` or `needs-work` — never ambiguous.
