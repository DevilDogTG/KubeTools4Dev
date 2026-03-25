# Scripts

This directory contains various utility scripts for building, packaging, and releasing the `KubeTools4Dev` project.

## Release Script (`release.ps1`)

The release script automates the process of versioning, building, and publishing a new release to GitHub.

### What it does:
1. Validates that there are no uncommitted changes in the working directory.
2. Extracts the `Version` from `KubeTools4Dev.csproj`.
3. Auto-increments the version (Patch by default, optionally Minor or Major) and updates the `.csproj` file.
4. Creates a new release branch (`release/vX.Y.Z`).
5. Commits the `.csproj` version bump.
6. Tags the commit with `vX.Y.Z`.
7. Builds the Velopack `.exe` installers and `.zip` portables using `build_installer_win.ps1`.
8. Pushes the tag and release branch to the `origin` remote.
9. Creates an official **GitHub Release** and uploads the built artifacts.
10. Merges the release branch downstream into your main sequence branches (`main`/`master` and `develop`).

### Prerequisites
- **Git** installed and available in your shell.
- **GitHub CLI (`gh`)** installed and authenticated. 
  - To authenticate: run `gh auth login`.
- **.NET 10 SDK** and **Velopack (`vpk`)** (installed automatically by the build script if missing).

### Usage

Run the script from anywhere, although standard practice is to run it from the root of your repository or from within the `scripts` folder.

#### 1. Dry Run (Preview Mode)
Highly recommended to run before making a real release. The `-DryRun` flag calculates the new version and prints exactly what git and build commands *would* be executed without actually modifying any files, branches, or remotes.

```powershell
.\scripts\release.ps1 -DryRun
```

#### 2. Default (Patch Bump)
Bumps the `Build` (patch) number. For example: `1.2.2` -> `1.2.3`.

```powershell
.\scripts\release.ps1
```

#### 3. Minor Bump
Bumps the `Minor` number and resets patch to `0`. For example: `1.2.2` -> `1.3.0`.

```powershell
.\scripts\release.ps1 -Minor
```

#### 4. Major Bump
Bumps the `Major` number and resets minor/patch to `0`. For example: `1.2.2` -> `2.0.0`.

```powershell
.\scripts\release.ps1 -Major
```

---

## Build Scripts

- **`build_installer_win.ps1`**: Compiles the Avalonia UI project and packs the executable and assets into a Velopack installer and portable zip archive targeting `win-x64`. It is automatically executed during the release process but can also be ran independently for local testing.
- **`build_installer_linux.sh`**: (If applicable) A script containing the equivalent logic for Linux environments.
