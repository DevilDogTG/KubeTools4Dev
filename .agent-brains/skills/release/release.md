---
id: release
name: Release (version bump + generated notes)
version: 1.0
compatibility: [claude, gemini, copilot, codex]
---

# Skill: Release (version bump + generated notes)

## Context
Invoke this skill to cut a new release of KubeTools4Dev. The agent analyzes everything merged
since the last release tag, **recommends** a bump type (major / minor / patch), **generates
user-facing release notes**, then drives the existing automation end-to-end:
`release.yml` (version bump + release PR) → rebase-merge → `tag.yml` (vX.Y.Z tag) →
`publish.yml` (Windows installer, portable zip, Linux `.deb`, apt repository).

Per global §6.7, never trigger a release automatically — the user must confirm the bump type
before dispatch and the release-PR merge before it lands.

## Procedure

### Phase 1 — Preflight

1. Confirm `gh` CLI is available and authenticated:
   ```powershell
   gh auth status
   ```

2. Sync and verify a releasable state:
   ```powershell
   git checkout main
   git pull
   git status --porcelain        # must be empty
   gh pr list --state open       # warn if feature PRs are open (they will miss this release)
   ```
   If an open PR with head `release/*` already exists — stop; a release is mid-flight.

3. Read the current version from `version.json` and find the last release tag:
   ```powershell
   git describe --tags --abbrev=0          # e.g. v1.3.6
   ```
   Sanity check: tag must match `version.json`. If they disagree, stop and investigate
   (a release may have half-completed).

### Phase 2 — Gather Changes Since Last Release

4. Collect the change set:
   ```powershell
   git log <last-tag>..HEAD --oneline
   git log <last-tag>..HEAD --pretty=format:"%h %s%n%b"
   ```
   If empty — stop: nothing to release.

5. Map commits to merged PRs for richer notes (titles, bodies, review context):
   ```powershell
   gh pr list --state merged --base main --limit 20 --json number,title,mergedAt,body
   ```
   Keep only PRs merged after the last tag's date. Ignore pure bookkeeping PRs
   (`chore(agent)`, handover memos) — they don't belong in user-facing notes.

### Phase 3 — Recommend the Bump

6. Classify every non-bookkeeping commit by Conventional Commit type and derive the
   recommendation:

   | Evidence | Recommendation |
   |----------|----------------|
   | Any `BREAKING CHANGE:` footer or `!` after type (e.g. `feat!:`) | **major** |
   | Any `feat:` (user-visible new capability) | **minor** |
   | Only `fix:` / `refactor:` / `perf:` / `docs:` / `test:` / `ci:` / `chore:` | **patch** |

   Judgement beats mechanics: a `fix` that changes user-visible behaviour contracts, or a
   `feat` that is trivial polish, may justify moving one level — say so explicitly when it
   applies.

7. Present to the user: the change list (grouped by type), the recommended bump with a
   one-sentence justification, and the resulting version (`current` → `next`). Ask the user
   to confirm **major / minor / patch / cancel**. Do not proceed without an explicit choice.

### Phase 4 — Generate Release Notes

8. Write user-facing notes from the PR titles/bodies — describe outcomes, not implementation.
   Use the template below. Rules:
   - Group as `### Fixes`, `### New`, `### Improvements` (omit empty groups).
   - One bullet per user-visible change; lead with the affected area in bold
     (e.g. `**Port forwarding**:`).
   - Plain language a user of the app understands; no internal class names unless the change
     is developer-facing (CI, packaging).
   - Backticks and quotes ARE safe in notes — `release.yml` passes the input via `env:`
     (fixed 2026-06-05; do not regress this).

9. Show the generated notes to the user for approval/edits before dispatch.

### Phase 5 — Dispatch the Release Workflow

10. Dispatch with the confirmed bump and approved notes:
    ```powershell
    $notes = @'
    <approved release notes here>
    '@
    gh workflow run release.yml -f version_type=<patch|minor|major> -f release_notes="$notes"
    ```

11. Watch the run:
    ```powershell
    gh run list --workflow=release.yml --limit 1 --json databaseId,status,conclusion
    gh run watch <id> --exit-status
    ```

12. **Failure recovery** (known mode from v1.3.6): if the run fails at "Open PR → main" but
    the `release/vX.Y.Z` branch was already pushed (check `git fetch && git branch -r`), do
    NOT re-dispatch — the bump commit exists. Open the release PR manually with the same body
    shape the workflow would have produced (`## Release vX.Y.Z`, bump line, notes section).
    `tag.yml` only requires a merged PR whose head branch starts with `release/`.
    If the run failed **before** the branch push, fix the cause and re-dispatch.

### Phase 6 — Merge, Tag, Publish

13. Confirm with the user, then rebase-merge the release PR (repo disallows squash;
    `main` requires PRs):
    ```powershell
    gh pr merge <prNum> --rebase --delete-branch
    ```

14. Verify the chain:
    ```powershell
    gh run list --workflow=tag.yml --limit 1 --json status,conclusion        # tag created
    gh run list --workflow=publish.yml --limit 1 --json databaseId           # then watch it
    gh run watch <publish-id> --exit-status
    ```
    `publish.yml` has three jobs: Windows installer, Linux `.deb`, and `publish-apt-repo`
    (gated on `vars.APT_REPO_ENABLED`). All must be green.

### Phase 7 — Post-release Verification

15. Verify artifacts:
    ```powershell
    gh release view vX.Y.Z --json tagName,assets --jq "{tag: .tagName, assets: [.assets[].name]}"
    ```
    Expect: `KubeTools4Dev-Setup-X.Y.Z.exe`, `KubeTools4Dev-Portable-X.Y.Z.zip`,
    `KubeTools4Dev_X.Y.Z_amd64.deb`.

16. Spot-check the apt repo (note: GitHub Pages CDN caches ~10 min — don't treat a stale
    index as failure if the publish job was green):
    ```powershell
    (Invoke-WebRequest -Uri "https://devildogtg.github.io/KubeTools4Dev/dists/stable/main/binary-amd64/Packages" -UseBasicParsing).Content | Select-String "Version"
    ```

17. Sync local main (`git pull` — picks up the bump commit + tag) and update
    `memory/overview.md` (Current Version + Recently Merged). Memory changes must route
    through a chore PR (`main` is protected); batch them with session-end if one is coming.

## Validation
- [ ] Working tree clean, `version.json` matched the last tag before starting.
- [ ] User explicitly confirmed the bump type after seeing the recommendation.
- [ ] User approved the generated release notes before dispatch.
- [ ] `release.yml`, `tag.yml`, and all `publish.yml` jobs green (or recovery path documented in the session log).
- [ ] Release assets present on the GitHub Release (3 artifacts).
- [ ] Memory overview updated (or explicitly deferred to session-end).

## Template: Release Notes

```markdown
### Fixes
- **<Area>**: <what was broken, now fixed — from the user's point of view>.

### New
- **<Area>**: <new capability>.

### Improvements
- **<Area>**: <what got better>.
```

## Template: Manual Release PR Body (recovery, step 12)

```markdown
## Release vX.Y.Z

**Version bump**: `X.Y.(Z-1)` → `X.Y.Z`
**Type**: patch|minor|major

### Release Notes
<approved notes>

---
Merge this PR into `main` (rebase merge) to trigger tag creation.
```
