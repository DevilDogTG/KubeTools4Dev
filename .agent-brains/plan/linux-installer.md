# Plan: Linux Installer (.deb + apt repository)

**Status:** active
**Created:** 2026-05-29
**Branch:** feature/linux-deb-installer

## Goal
Ship a complete Linux install story in a single PR:

1. **Phase A — `.deb` artifact.** Each `v*` tag produces a self-contained `linux-x64` `.deb` attached to the GitHub Release (runnable on Ubuntu/Debian and WSL2+WSLg).
2. **Phase B — Self-hosted apt repository.** That same `.deb` lands in a GitHub-Pages-hosted apt repo signed by a project-owned GPG key, so users can `apt install kubetools4dev` once and get every future release via `apt upgrade`.

Phase A is fully implementable by automation. Phase B has manual maintainer steps (GPG key gen, gh-pages bootstrap, Pages enable, secret upload) that this branch **does not** perform automatically — they are documented in `docs/maintainer/apt-repo-setup.md` for the maintainer to execute, after which Phase B's CI job becomes functional.

Full Phase A design: `~/.claude/plans/peppy-splashing-lovelace.md` (approved 2026-05-29).
Full Phase B playbook: `docs/maintainer/apt-repo-setup.md` (delivered in this branch).

## Decisions confirmed
**Phase A (.deb):**
- Format `.deb` only; runtime self-contained; arch `linux-x64`.
- Hand-rolled `dpkg-deb` instead of Velopack's Linux path.
- No code changes in `Program.cs` or `KubeTools4Dev.csproj` (cross-platform settings path + `WinExe` publishes correctly to linux-x64).

**Phase B (apt repo):**
- Hosted on GitHub Pages (`gh-pages` branch) — free, no third-party signing service.
- Signed with a 4096-bit RSA GPG key, 2-year expiry, documented rotation procedure.
- Public key shipped in the gh-pages root as `KubeTools4Dev.gpg` (no keyserver dependency).
- End-user install via the modern `signed-by=/etc/apt/keyrings/...` form (not deprecated `apt-key`).
- `pool/` retains every released version; cleanup deferred until repo bloat justifies it.

## Phase A — `.deb` artifact
- [x] **A1 Packaging assets** — `packaging/linux/control.template`, `packaging/linux/kubetools4dev.desktop`, `packaging/linux/build-deb.sh`. `.gitattributes` added to pin LF endings on `*.sh`/`*.desktop`/`control.template`; build-deb.sh staged with mode 100755.
- [x] **A2 CI: publish-linux job** — added in `publish.yml`. `runs-on: ubuntu-latest`; reads `version.json` via `jq`; `dotnet publish -c Release -r linux-x64 --self-contained true /p:DebugType=embedded`; calls `build-deb.sh`; uploads `*.deb` via second `softprops/action-gh-release@v2` step on same `tag_name`.
- [x] **A3 README** — "Download & Install" section added above "Getting Started"; covers Windows / Ubuntu+Debian / WSL2; notes WSLg on Win11 vs X server on Win10; documents Linux-side kubeconfig symlink.
- [x] **A4 Local smoke test (WSL Debian 13)** — publish produced 131 MB self-contained ELF; `.deb` produced at 40 MB; `apt install ./KubeTools4Dev_1.3.3_amd64.deb` clean; binary executes, creates `~/.config/KubeTools4Dev/settings.json`, auto-discovers WSL-side kubeconfig (k3s); Velopack startup silent; `apt remove` removes all paths with no residue.

## Phase B — apt repository
- [x] **B1 Maintainer guide** — `docs/maintainer/apt-repo-setup.md` covering one-time GPG setup, gh-pages bootstrap, CI workflow YAML, end-user install snippet, key-rotation procedure, disaster recovery. Delivered + revised post-review.
- [x] **B2 [MAINTAINER ACTION] GPG key + GitHub secrets** — Done 2026-06-04. Key `rsa4096/88D08B8B1B07D512` (uid `KubeTools4Dev Release Signing <noreply@dmnsn.com>`, exp 2028-06-03). All three secrets present (`APT_REPO_GPG_KEY_ID`, `APT_REPO_GPG_PASSPHRASE`, `APT_REPO_GPG_PRIVATE_KEY`).
- [x] **B3 [MAINTAINER ACTION] Bootstrap gh-pages branch** — Done 2026-06-04. `gh-pages` HEAD `6a8a971` with `KubeTools4Dev.gpg`, `.nojekyll`, `index.html`. (Empty `pool/` + `dists/` are created on demand by the publish job — see Risks below.)
- [x] **B4 [MAINTAINER ACTION] Enable GitHub Pages** — Done 2026-06-04. Pages serves from `gh-pages` root, status `built`. `https://devildogtg.github.io/KubeTools4Dev/KubeTools4Dev.gpg` returns the key.
- [x] **B5 publish-apt-repo CI job** — added to `.github/workflows/publish.yml`, `needs: publish-linux`, gated `if: ${{ vars.APT_REPO_ENABLED == 'true' }}`. Workflow YAML validates (`yaml.safe_load` green; three jobs parsed: `publish` / `publish-linux` / `publish-apt-repo`). Job is a hard no-op until the maintainer sets the repo variable in step 1.6 of the guide. `secrets.*` is not allowed in `if:` expressions per GitHub Actions docs, so the gate is a `vars.*` repo variable instead — also acts as a kill-switch during key rotation.
- [x] **B6 README — apt-source install block** — Done 2026-06-05 on `docs/readme-apt-source-install`. README "Linux (Ubuntu / Debian)" section now leads with the Part-3 `signed-by` apt-source snippet (keyring install → sources.list.d entry → `apt install kubetools4dev`), plus repo-removal instructions; manual `.deb` download kept as an air-gapped fallback subsection. WSL2 note updated to point at the apt repo.
- [x] **B7 End-to-end test** — Cut via `release.yml` patch-bump dispatches. `v1.3.4` (first attempt): Linux `.deb` + Windows installer published cleanly, but `publish-apt-repo` failed at "Import signing key" — `gpg --import-ownertrust` rejects the 16-char long key ID stored in `APT_REPO_GPG_KEY_ID`; needs the full 40-char fingerprint. Fixed in PR #52 (rebase-merged as `70ea034`) by deriving the fingerprint inside the workflow via `gpg --with-colons --fingerprint`. `v1.3.5` (re-attempt): all three publish jobs green — Linux `.deb` 59s, Windows installer 1m51s, **`publish-apt-repo` 26s**. `https://devildogtg.github.io/KubeTools4Dev/dists/stable/InRelease` returns a clearsigned manifest (SHA512 + MD5 + SHA1); `pool/main/k/kubetools4dev/KubeTools4Dev_1.3.5_amd64.deb` ≈ 41 MB. Live `apt install` on a fresh box pending user-side verification with the Part-3 snippet.

## Phase C — wrap-up
- [x] **C1 PR via sk-finish-feature** — Draft PR #49 opened 2026-05-29, `sk-pr-review` approved at `53d93dd`, fix commit `170ab40` for the dists-mkdir gap landed 2026-06-04, PR merged (rebase) at 2026-06-04T07:57Z. Merge commit on main: `41f8fb4`.

## Risks / open items
- ~~**libicu Depends string**~~ — RESOLVED A4. Widened to `libicu76 | libicu74 | libicu72 | libicu71 | libicu70 | libicu67 | libicu66` after Debian 13's `libicu76` failed against the original list. Covers Debian 11/12/13 and Ubuntu 20.04/22.04/24.04.
- ~~**Velopack startup on Linux**~~ — RESOLVED A4. `VelopackApp.Build().Run()` is silent on Linux outside a Velopack-managed install; no code change needed.
- **Pre-existing `LogPath` relative-path bug** (out of scope): default `LogPath` is `"../logs/kubetools4dev-.log"`; resolves relative to CWD. When launched from `.desktop` on Linux, CWD is typically `$HOME`, so logs land under `~/logs/`. File a follow-up to change the default to `Environment.SpecialFolder.LocalApplicationData`-rooted on Linux.
- **WSLg + Avalonia 12**: no GUI rendering test possible from `wsl.exe -e bash`. Manual verification by maintainer before merge.
- **Phase B CI job pre-secrets**: the `if: secrets.APT_REPO_GPG_KEY_ID != ''` gate means publishing this PR before B2-B4 are done is safe (job skips, no error). But the **README apt-source instructions** (B6) MUST land **after** B2-B4 or users will follow a broken install flow. Keep B6 in a follow-on commit, not the initial merge.
- **Pages cache TTL** (Phase B): GitHub Pages CDN caches ~10 min — users won't see a new release the instant it's tagged. Acceptable.
- **Pool bloat** (Phase B): ~40 MB per release. After 50 releases ≈ 2 GB. Cleanup job deferred until release count justifies it.

## Progress Log
- 2026-05-29: Plan approved, branch `feature/linux-deb-installer` created off `main` (clean tree at `68c861d`).
- 2026-05-29: Phase A (A1–A4) landed in working tree. Smoke-tested end-to-end on WSL Debian 13. Required widening libicu alternatives. Velopack-on-Linux risk dismissed.
- 2026-05-29: Phase B scope folded into this branch per maintainer's request. Maintainer guide (`docs/maintainer/apt-repo-setup.md`) and Phase-B checklist (B1–B7) added. B1 delivered; B2–B4 are maintainer-side manual steps blocking B5–B7.
- 2026-05-29: B5 landed — `publish-apt-repo` job appended to `publish.yml`, gated on `vars.APT_REPO_ENABLED == 'true'`. Guide updated with step 1.6 (master switch) and Part 2 trimmed to a description of what the existing job does (no more inline YAML duplication).
- 2026-05-29: Work committed in 4 atomic commits (`ec628aa` packaging, `5622b28` CI, `6ba082a` docs, `e2d67f5` plan). Preflights green: build 0W/0E, tests 154/154 (85 Core + 69 UI). Draft PR #49 opened: https://github.com/DevilDogTG/KubeTools4Dev/pull/49 — awaiting `sk-pr-review`.
- 2026-05-29: `sk-pr-review` posted findings at `d3ce5f2` — **approved**. No 🔴 critical. 2 🟡 warnings (GPG passphrase on CLI; guide step 1.4 missing "start from main" preamble) — both operational hardening for the future apt-repo activation, not blocking. 5 🔵 info items (StartupWMClass verification deferred to WSLg manual test, `.gpg` vs `.asc` naming, explicit `bash` call, dual `Read version` patterns, hard-coded binary name). https://github.com/DevilDogTG/KubeTools4Dev/pull/49#issuecomment-4573569573
- 2026-06-04: Folded both 🟡 warnings and 🔵 #4 + #5 into one atomic fix commit (`53d93dd`). gpg now reads passphrase via `--passphrase-fd 0` with bash here-string; guide step 1.4 has "from a clean checkout of `main`" preamble; `publish-apt-repo` Read-version step explains the API roundtrip inline; `build-deb.sh` has a friendly existence preflight (exit 70 + AssemblyName hint). Build 0W/0E, tests 154/154, WSL Debian 13 re-smoke green (happy path + new failure path). `sk-pr-review` re-run posted **approved** at `53d93dd`: https://github.com/DevilDogTG/KubeTools4Dev/pull/49#issuecomment-4618310258
- 2026-06-04: Maintainer completed Part 1 of `apt-repo-setup.md` off-agent. Verified end-to-end: 3 secrets present + dated, `APT_REPO_ENABLED=true`, `gh-pages` HEAD `c7bf76f` with public key reachable + parseable (rsa4096/88D08B8B1B07D512). Found two issues: (a) `dists/stable/main/binary-amd64/` cannot exist on `gh-pages` because git drops empty dirs — would cause first apt-repo CI run to fail at the `> dists/.../Packages` redirect; (b) stray 7.9 KB `.gitignore` from `main` survived the orphan bootstrap. Fix: `publish.yml` now `mkdir -p`s the dir before `apt-ftparchive`; guide step 1.4 drops the misleading `mkdir -p` lines; `.gitignore` removed from `gh-pages` via worktree commit `6a8a971`. CI fix shipped on feature branch in `170ab40`. PR #49 is now `OPEN` (no longer draft).
- 2026-06-04: B7 strategy switched away from `v0.0.0-apt-test` because `build-deb.sh` derives the deb filename from `version.json` (currently `1.3.3`), so a throwaway tag would still produce `KubeTools4Dev_1.3.3_amd64.deb` and collide with the eventual real 1.3.3 release. New plan: wait for PR #49 merge, bump version to `1.3.4-rc1`, tag and let CI publish — produces a sensibly-named prerelease artifact.
- 2026-06-04: PR #49 rebase-merged at 07:57Z, last commit of feature branch (`170ab40` dists-mkdir fix) lands on main as `41f8fb4`. Origin feature branch deleted; local copy still present at `170ab40`. C1 done. B7 unblocked — next session can immediately work the `v1.3.4-rc1` flow.
- 2026-06-04: Session resumed to work B7. Pre-B7 state: local `main` ahead 1 (unpushed handover memo `332075f`); 5 stale local branches present; remote `main` at `41f8fb4`.
- 2026-06-04: Branch protection on `main` blocked direct push of the handover memo. Re-routed via PR #50 (`chore/handover-2026-06-04-pm`), merged onto main as `7f073ac`. Dispatched `release.yml` (patch) → PR #51 `Release v1.3.4` rebase-merged as `1c01825` → `tag.yml` stamped `v1.3.4` → `publish.yml` ran. Linux `.deb` ✓ (1m1s), Windows installer ✓ (~3m), `publish-apt-repo` ✗ at "Import signing key": `gpg: error in '[stdin]': invalid fingerprint`. Run `26941642652` job `79484525225` (~18s before fail). No `gh-pages` state mutated. Root cause: line 250 of `publish.yml` piped `APT_REPO_GPG_KEY_ID` (16-char long key ID per guide §1.4) into `gpg --import-ownertrust`, which requires the full 40-char fingerprint.
- 2026-06-04: PR #52 `fix(ci): derive fingerprint for gpg --import-ownertrust` opened on `fix/apt-repo-ownertrust-fingerprint`. Workflow now runs `gpg --with-colons --fingerprint "$GPG_KEY_ID" | awk -F: '/^fpr:/ {print $10; exit}'` after secret-key import; guards against empty result with a clear error. No maintainer-side change needed (secret stays as the long ID). Squash-merge attempted but repo disallows squash; rebase-merged as `70ea034`.
- 2026-06-04: Re-dispatched `release.yml` (patch) → PR #53 `Release v1.3.5` rebase-merged → `v1.3.5` tagged → `publish.yml` run `26944046408`: Linux `.deb` ✓ 59s, Windows installer ✓ 1m51s, **`publish-apt-repo` ✓ 26s** (every step including the gh-pages commit/push). First successful end-to-end run of the apt-repo flow. `dists/stable/InRelease`, `Release.gpg`, and `pool/main/k/kubetools4dev/KubeTools4Dev_1.3.5_amd64.deb` (~41 MB) all serve `200 OK` from Pages. B6 now unblocked.
- 2026-06-04: B7 closed. User ran the Part-3 snippet on WSL: `apt update` accepted the signed `InRelease`, `apt install kubetools4dev` succeeded, binary launches. End-to-end Phase B flow verified.
