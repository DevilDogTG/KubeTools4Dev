[CmdletBinding()]
param (
    [switch]$Major,
    [switch]$Minor,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$ProjectName = "KubeTools4Dev"
$ProjectDir = "$PSScriptRoot/../src/$ProjectName"
$ReleaseDir = "$PSScriptRoot/../dist/Releases"

Write-Host "Checking for uncommitted changes..."
$gitStatus = git status --porcelain
if (![string]::IsNullOrWhiteSpace($gitStatus)) {
    throw "Working directory is not clean. Please commit or stash your changes before creating a release."
}

# 1. Extract Current Version
$CsprojPath = "$ProjectDir/$ProjectName.csproj"
$CsprojContent = [xml](Get-Content $CsprojPath)
$CurrentVersionStr = $CsprojContent.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($CurrentVersionStr)) {
    throw "Failed to extract application version from '$ProjectName.csproj'. Ensure the <Version> element exists and contains a valid version string."
}

# Calculate New Version
$versionObj = [version]$CurrentVersionStr

if ($Major) {
    $newVersion = "$($versionObj.Major + 1).0.0"
} elseif ($Minor) {
    if ($versionObj.Minor -ge 0) {
        $newVersion = "$($versionObj.Major).$($versionObj.Minor + 1).0"
    } else {
        $newVersion = "$($versionObj.Major).1.0"
    }
} else {
    $patch = if ($versionObj.Build -ge 0) { $versionObj.Build + 1 } else { 1 }
    $newVersion = "$($versionObj.Major).$($versionObj.Minor).$patch"
}

Write-Host "Current Version: $CurrentVersionStr"
Write-Host "New Version:     $newVersion"

$Version = $newVersion
$ReleaseBranch = "release/v$Version"
$TagName = "v$Version"

if ($DryRun) {
    Write-Host "`n[DRY RUN] Would update $ProjectName.csproj to version $Version"
    Write-Host "[DRY RUN] Would branch: $ReleaseBranch"
    Write-Host "[DRY RUN] Would commit version bump"
    Write-Host "[DRY RUN] Would tag: $TagName"
    Write-Host "[DRY RUN] Would build installer"
    Write-Host "[DRY RUN] Would push branch and tag to origin"
    Write-Host "[DRY RUN] Would create GitHub release"
    Write-Host "[DRY RUN] Would merge into main/master and develop"
    Write-Host "`nDry run complete. Exiting."
    exit 0
}

Write-Host "Started release process for version: $Version"

# 2. Create release branch
Write-Host "Creating release branch '$ReleaseBranch'..."
git checkout -b $ReleaseBranch
if ($LASTEXITCODE -ne 0) { throw "Failed to create release branch." }

# 3. Update csproj and commit bump
Write-Host "Updating version in $ProjectName.csproj..."
$CsprojContent.Project.PropertyGroup.Version = $newVersion
$CsprojContent.Save($CsprojPath)

Write-Host "Committing version bump..."
git add $CsprojPath
git commit -m "chore: bump version to $newVersion"
if ($LASTEXITCODE -ne 0) { throw "Failed to commit version bump." }

# 4. Tag version
Write-Host "Tagging version '$TagName'..."
git tag $TagName
if ($LASTEXITCODE -ne 0) { throw "Failed to tag version." }

# 5. Build installer wrapper
Write-Host "Building installer..."
& "$PSScriptRoot/build_installer_win.ps1"
if ($LASTEXITCODE -ne 0) { throw "Build installer script failed." }

# 6. Create release version on GitHub
Write-Host "Pushing tag '$TagName' to origin..."
git push origin $TagName
if ($LASTEXITCODE -ne 0) { throw "Failed to push tag to origin." }

Write-Host "Pushing release branch '$ReleaseBranch' to origin..."
git push origin $ReleaseBranch
if ($LASTEXITCODE -ne 0) { throw "Failed to push release branch to origin." }

Write-Host "Creating GitHub Release '$TagName'..."
$SetupFile = "$ReleaseDir/${ProjectName}-Setup-${Version}.exe"
$PortableFile = "$ReleaseDir/${ProjectName}-Portable-${Version}.zip"

$ghArgs = @("release", "create", $TagName, "--title", "Release $TagName", "--notes", "Automated release for version $Version.")
if (Test-Path $SetupFile) {
    $ghArgs += $SetupFile
} else {
    Write-Warning "Setup file not found: $SetupFile"
}

if (Test-Path $PortableFile) {
    $ghArgs += $PortableFile
} else {
    Write-Warning "Portable file not found: $PortableFile"
}

# Run GitHub CLI
& gh $ghArgs
if ($LASTEXITCODE -ne 0) { throw "Failed to create GitHub Release." }
Write-Host "GitHub release created successfully."

# 7. Merge into master/main and develop too
function Merge-Branch {
    param([string]$targetBranch, [string]$sourceBranch)
    Write-Host "Merging '$sourceBranch' into '$targetBranch'..."
    git checkout $targetBranch
    if ($LASTEXITCODE -ne 0) { throw "Failed to checkout '$targetBranch'." }

    git pull origin $targetBranch
    
    # Attempt merge (No Fast-Forward)
    git merge $sourceBranch --no-ff -m "Merge branch '$sourceBranch' into $targetBranch"
    if ($LASTEXITCODE -ne 0) { throw "Merge conflict or error while merging to '$targetBranch'. Please resolve manually." }
    
    git push origin $targetBranch
    if ($LASTEXITCODE -ne 0) { throw "Failed to push target branch '$targetBranch'." }
}

$branches = git branch --list "main" "master"
if ($branches -match "main") {
    $MainBranch = "main"
} elseif ($branches -match "master") {
    $MainBranch = "master"
} else {
    Write-Warning "Neither 'main' nor 'master' branch found."
    $MainBranch = $null
}

if ($MainBranch) {
    Merge-Branch -targetBranch $MainBranch -sourceBranch $ReleaseBranch
}

$devBranches = git branch --list "develop"
if ($devBranches -match "develop") {
    Merge-Branch -targetBranch "develop" -sourceBranch $ReleaseBranch
    
    # Check out develop at the end of the script to continue work
    git checkout develop
} else {
    Write-Warning "'develop' branch not found. Skipping merge to develop."
}

Write-Host "Release process for version $Version completed successfully!"
