# Plan: Align Release Flow with JoystickGremlinSharp (Hybrid Approach)

## Objective
Update the release flow and version bumping process in `KubeTools4Dev` to use an automated GitHub Actions model ("main-first") similar to `JoystickGremlinSharp`, but maintain both `version.json` and `KubeTools4Dev.csproj` as explicit files that get bumped during the release process.

## Background & Motivation
Currently, `KubeTools4Dev` uses `KubeTools4Dev.csproj` as the source of truth for its version (`<Version>1.2.3</Version>`). To automate the release pipeline (branching, bumping, and PR generation) based on the `JoystickGremlinSharp` model, we will introduce `version.json`. However, instead of removing the version from `.csproj` and injecting it dynamically at build time, the release workflow will be responsible for simultaneously updating both `version.json` and the `.csproj` file. This satisfies the user's request to have the version bump handled explicitly in both files.

## Scope & Impact
This change will introduce a new file (`version.json`) initialized with the current version and establish GitHub Actions workflows for managing releases. The `KubeTools4Dev.csproj` file will retain its `<Version>` tag, but its value will be managed automatically by the release workflow.

## Proposed Solution / Implementation Steps

### Phase 1: Establish Initial Version State
1. **Create `version.json`**: Add a `version.json` file to the root of `KubeTools4Dev` initialized with the current version from the `.csproj`:
   ```json
   {
     "version": "1.2.3"
   }
   ```
   *Note: We DO NOT remove the `<Version>` tag from `src/KubeTools4Dev/KubeTools4Dev.csproj`.*

### Phase 2: Implement GitHub Actions Workflows (Hybrid Version Bump)
1. **Add `release.yml` Workflow**: Create `.github/workflows/release.yml` based on the logic from `JoystickGremlinSharp`. This workflow will:
   - Provide a manual trigger (`workflow_dispatch`) to select patch, minor, or major bumps.
   - Read and increment the version in `version.json`.
   - Update the `version.json` file on disk.
   - **(NEW)** Use a script (e.g., `sed` or a custom python snippet) to update the `<Version>X.Y.Z</Version>` tag in `src/KubeTools4Dev/KubeTools4Dev.csproj` with the new version.
   - Create a `release/vX.Y.Z` branch.
   - Commit both the updated `version.json` and `KubeTools4Dev.csproj`.
   - Open a PR against `main`.
2. **Add `tag.yml` Workflow**: Create `.github/workflows/tag.yml` based on the `JoystickGremlinSharp` workflow. This workflow will:
   - Trigger on PR merge to `main` when the branch name starts with `release/`.
   - Read the version from `version.json`.
   - Create and push a `vX.Y.Z` git tag.

## Verification
- Test the GitHub Actions workflow on a test branch (or run locally using `act`) to ensure it successfully bumps the version in *both* `version.json` and `KubeTools4Dev.csproj`, creates a branch, and opens a PR.