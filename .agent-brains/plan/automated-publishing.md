# Plan: Automated Build and Release Publishing

## Objective
Implement a `publish.yml` GitHub Actions workflow that automatically builds, packages (using Velopack), and creates a GitHub Release whenever a new version tag (e.g., `v1.2.4`) is pushed. This will complete the release pipeline by mirroring the functionality in `JoystickGremlinSharp`.

## Background
The current pipeline handles version bumping and tagging, but it stops short of building artifacts and creating a formal GitHub Release. The repository already contains an obsoleted script (`scripts/obsoleted/build_installer_win.ps1`) that demonstrates how to use Velopack (`vpk`) to package the application.

## Proposed Solution

### 1. Create `publish.yml` Workflow
Add `.github/workflows/publish.yml` with the following stages:
- **Trigger**: `on.push.tags: ['v*']`
- **Environment**: `windows-2022` (required for Velopack and potential signing).
- **Steps**:
    1. **Checkout**: Fetch the code.
    2. **Setup .NET**: Ensure .NET 10 is available.
    3. **Read Version**: Extract the version from `version.json`.
    4. **Restore & Build**: Run `dotnet restore` and `dotnet build -c Release`.
    5. **Publish**: Run `dotnet publish` for `win-x64` as self-contained.
    6. **Install Velopack**: Install `vpk` global tool.
    7. **Pack**: Use `vpk pack` to create the installer and portable zip.
    8. **Create Release**: Use `softprops/action-gh-release` to create a GitHub Release and upload the artifacts (`*-Setup.exe` and `*-Portable.zip`).

### 2. Signing (Optional/Future Proofing)
- While not strictly requested now, the workflow will include placeholders/logic for signing (similar to `JoystickGremlinSharp`) so it can be enabled easily with repository secrets (`SIGNING_CERT_BASE64`).

## Implementation Steps
1. Create `.github/workflows/publish.yml`.
2. Update `.agent-brains/memory/overview.md` to document the full end-to-end release pipeline.

## Verification
- Since we cannot trigger tag-based workflows easily in this environment without a real push, we will verify the workflow syntax and logic against the existing successful `JoystickGremlinSharp` implementation.
- A final manual check of the YAML structure will be performed.
