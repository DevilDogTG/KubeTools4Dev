# Setting Up the KubeTools4Dev apt Repository

This guide walks the maintainer through standing up a self-hosted Debian apt
repository on GitHub Pages so end users can install KubeTools4Dev with
`apt install kubetools4dev` and pick up updates via `apt upgrade`.

It is split into three parts:

1. **One-time maintainer setup** — generate a GPG signing key, store secrets,
   bootstrap the `gh-pages` branch.
2. **CI integration** — the workflow that signs and publishes each release.
3. **End-user instructions** — the two commands users run to add the repo
   (paste these into the README once the repo is live).

Cost: **free**. GitHub Pages, GitHub Actions on a public repo, and GnuPG are
all zero-cost. The only "free tier" caveat is GitHub Actions minutes on
public repos — currently unlimited for open source.

> **Prerequisites on your workstation**
>
> ```bash
> sudo apt install gnupg        # Linux / WSL
> # macOS: brew install gnupg
> # Windows native: install Gpg4win  https://gpg4win.org/
> ```
>
> Verify: `gpg --version` should print 2.2+ (we will use modern features).

---

## Part 1 — One-time maintainer setup

### 1.1 Generate the signing key

Pick a long passphrase and store it in your password manager **before** you
start — `gpg` will not let you change your mind partway through.

```bash
gpg --batch --gen-key <<'EOF'
%echo Generating KubeTools4Dev apt-repo signing key
Key-Type: RSA
Key-Length: 4096
Key-Usage: sign
Name-Real: KubeTools4Dev Release Signing
Name-Email: noreply@dmnsn.com
Expire-Date: 2y
Passphrase: REPLACE_WITH_LONG_PASSPHRASE
%commit
%echo Done
EOF
```

Find the key fingerprint:

```bash
gpg --list-secret-keys --keyid-format LONG noreply@dmnsn.com
# sec   rsa4096/ABCD1234EF567890 2026-05-29 [S] [expires: 2028-05-29]
#       FULL-40-CHAR-FINGERPRINT-GOES-HERE
# uid                 [ultimate] KubeTools4Dev Release Signing <noreply@dmnsn.com>
```

Copy the 40-character fingerprint — you will need it in step 1.3.

### 1.2 Export the keys

```bash
# Private key — for the GitHub secret. ASCII-armoured so it survives the
# GitHub secrets text box.
gpg --armor --export-secret-keys ABCD1234EF567890 > apt-repo-signing.private.asc

# Public key — for users to verify packages. Ship this in the gh-pages branch.
gpg --armor --export ABCD1234EF567890 > KubeTools4Dev.gpg
```

After CI is wired up and tested, **destroy the local copy** of the private
key file:

```bash
shred -u apt-repo-signing.private.asc
```

Keep the in-keyring secret key on your workstation as the backup — if you
lose access to GitHub, you can re-publish from your machine. Back up
`~/.gnupg/` to a password manager / encrypted USB.

### 1.3 Add GitHub repository secrets

In the repo: **Settings → Secrets and variables → Actions → New repository
secret**. Add three:

| Name                          | Value                                    |
| ----------------------------- | ---------------------------------------- |
| `APT_REPO_GPG_PRIVATE_KEY`    | Full contents of `apt-repo-signing.private.asc` |
| `APT_REPO_GPG_PASSPHRASE`     | The passphrase from step 1.1             |
| `APT_REPO_GPG_KEY_ID`         | Long key id, e.g. `ABCD1234EF567890`     |

Mask all three (default for `secrets.*`). Never echo them in workflow logs.

**Do not** set the `APT_REPO_ENABLED` variable yet — that is the master
switch that turns on the publish job, and we want it off until everything
else (gh-pages branch, Pages enabled, key verified) is ready. We will flip
it in step 1.6.

### 1.4 Bootstrap the `gh-pages` branch

From a clean checkout of `main` (`git switch main && git status` shows nothing
to commit — the orphan branch inherits the current tree before we wipe it,
so starting from anywhere else means you'll inherit junk):

```bash
git checkout --orphan gh-pages
git rm -rf .                                 # wipe working tree
mkdir -p dists/stable/main/binary-amd64
mkdir -p pool/main/k/kubetools4dev

cp /path/to/KubeTools4Dev.gpg ./KubeTools4Dev.gpg
touch .nojekyll                              # tells Pages not to run Jekyll

cat > index.html <<'HTML'
<!doctype html>
<title>KubeTools4Dev apt repository</title>
<h1>KubeTools4Dev apt repository</h1>
<p>See <a href="https://github.com/DevilDogTG/KubeTools4Dev#linux-ubuntu--debian">install instructions</a>.</p>
<p>Signing key: <a href="KubeTools4Dev.gpg">KubeTools4Dev.gpg</a></p>
HTML

git add .
git commit -m "chore(apt): bootstrap gh-pages with signing key + empty pool"
git push origin gh-pages
```

### 1.5 Enable GitHub Pages

Repo **Settings → Pages → Source: Deploy from a branch → Branch: `gh-pages` / `(root)`**.
Save. Wait ~1 minute for the first deploy. Verify
`https://devildogtg.github.io/KubeTools4Dev/KubeTools4Dev.gpg` returns the
ASCII-armoured public key.

### 1.6 Flip the master switch

In the repo: **Settings → Secrets and variables → Actions → Variables tab →
New repository variable**. Add:

| Name                | Value  |
| ------------------- | ------ |
| `APT_REPO_ENABLED`  | `true` |

This variable gates the `publish-apt-repo` job in `.github/workflows/publish.yml`.
Until it is set to `true` the job is **skipped entirely** on every release —
so the workflow YAML can safely ship before any of the GPG / gh-pages setup
is done. The moment you set it, the **next** tag push (or a manual
`workflow_dispatch`-equivalent re-run) will publish to the apt repo.

To turn the apt repository off temporarily (e.g. during key rotation), just
delete the variable or change its value to anything other than `true`. The
`.deb` upload to the GitHub Release continues regardless.

---

## Part 2 — CI integration

The `publish-apt-repo` job is **already wired into `.github/workflows/publish.yml`**.
It runs after `publish-linux` finishes, gated on the `APT_REPO_ENABLED`
repo variable (Part 1.6) — until you flip that variable, the job is
skipped on every release and is a true no-op. No need to edit the
workflow file when you do the one-time setup.

What the job does, in order:
1. Checks out the `gh-pages` branch into `site/`.
2. Reads `version.json` from the main branch via the GitHub API.
3. Downloads the `.deb` that `publish-linux` just attached to the Release.
4. Copies it into `site/pool/main/k/kubetools4dev/`.
5. Imports the GPG private key from `APT_REPO_GPG_PRIVATE_KEY` and trusts
   it ultimately so signing won't prompt.
6. Regenerates `Packages{,gz,xz}` via `apt-ftparchive packages` and the
   `Release` file via `apt-ftparchive release` with origin/suite/component
   metadata.
7. Signs `Release` two ways: `Release.gpg` (detached, ASCII-armoured) and
   `InRelease` (clearsigned). Modern apt clients prefer `InRelease`.
8. Commits + pushes additively to `gh-pages`. Skips the push if nothing
   changed (e.g. a manual re-run of an already-published tag).

> **Operational note:** the gpg signing step reads the passphrase from
> file descriptor 0 via a bash here-string (`<<<"$GPG_PASSPHRASE"`),
> keeping it out of `/proc/<pid>/cmdline` and `ps aux` entirely. The
> passphrase env var is still in-process — **do not** add `set -x` or
> `-v` to those steps, which would print expanded variables to the
> workflow log.

### Verifying CI

Push a throwaway tag once secrets and gh-pages are in place:

```bash
git tag v0.0.0-apt-test
git push origin v0.0.0-apt-test
```

Watch the Actions run. Once green, confirm:

```bash
curl -sI https://devildogtg.github.io/KubeTools4Dev/dists/stable/InRelease
# HTTP/2 200
curl -s  https://devildogtg.github.io/KubeTools4Dev/dists/stable/main/binary-amd64/Packages | head
# Package: kubetools4dev
# Version: 0.0.0-apt-test
# ...
```

Then delete the throwaway tag (`git push --delete origin v0.0.0-apt-test`)
and, if you want, manually edit the `pool/` to remove the test `.deb`.

---

## Part 3 — End-user instructions

Once Part 2 is shipped, paste this into the README's **Linux (Ubuntu / Debian)**
section, **above** the manual-download fallback:

> ### Linux (Ubuntu / Debian) — apt repository
>
> ```bash
> # 1. Trust the signing key (one-time)
> sudo install -d /etc/apt/keyrings
> curl -fsSL https://devildogtg.github.io/KubeTools4Dev/KubeTools4Dev.gpg \
>   | sudo gpg --dearmor -o /etc/apt/keyrings/kubetools4dev.gpg
>
> # 2. Register the repo (one-time)
> echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/kubetools4dev.gpg] \
> https://devildogtg.github.io/KubeTools4Dev/ stable main" \
>   | sudo tee /etc/apt/sources.list.d/kubetools4dev.list
>
> # 3. Install — and `apt upgrade` will pick up every future release
> sudo apt update
> sudo apt install kubetools4dev
> ```
>
> To remove the repository later:
>
> ```bash
> sudo rm /etc/apt/sources.list.d/kubetools4dev.list \
>        /etc/apt/keyrings/kubetools4dev.gpg
> sudo apt update
> ```

**Notes for users:**

- The `signed-by=` form is the modern replacement for the deprecated
  `apt-key add` flow. It scopes the trust of the key to *this one repo*,
  not the system-wide trust store — safer.
- `arch=amd64` keeps `apt update` from looking for `arm64` / `i386` indexes
  that we don't publish (silences a warning on multi-arch hosts).
- WSL Ubuntu and WSL Debian work identically.

---

## Part 4 — Maintenance & rotation

### Key rotation (every 2 years, or after suspected compromise)

1. Generate a new key (repeat 1.1).
2. Update the three repo secrets (1.3).
3. Publish the new public key into `gh-pages` root, **alongside** the old
   one — e.g. `KubeTools4Dev-2028.gpg`.
4. Re-sign the existing `dists/stable/Release` with the new key (one-off CI
   run via `workflow_dispatch`).
5. Update README to point users at the new key file.
6. After a transition period (~3 months), remove the old key file.

### Pruning old releases

Until `pool/` exceeds ~2 GB it is fine to retain every release. After that,
add a `cleanup-pool` job that keeps the last 10 versions and rebuilds the
indexes. Defer until justified.

### Disaster recovery

The signing key is recoverable from your local `~/.gnupg/`. The published
`.deb`s are recoverable from GitHub Releases. The gh-pages branch can be
rebuilt from scratch: re-bootstrap (1.4), then re-run the publish job for
each version tag you want carried over.

If you lose **both** the local keyring and the GitHub secret, you must
generate a new key and follow the rotation steps — users will see a
signature-verification warning on their next `apt update` until they fetch
the new public key.
