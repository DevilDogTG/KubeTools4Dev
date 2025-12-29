$ErrorActionPreference = "Stop"

# Configuration
$ProjectName = "KubeTools4Dev"
$ProjectDir = "$PSScriptRoot/../src/$ProjectName"
$PublishDir = "$PSScriptRoot/../dist/win-x64"
$ReleaseDir = "$PSScriptRoot/../dist/Releases"

# Ensure vpk is installed
Write-Host "Checking for vpk (Velopack CLI)..."
dotnet tool update -g vpk
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to update vpk globally. Trying to install..."
    dotnet tool install -g vpk
}

# Get Version
$CsprojContent = [xml](Get-Content "$ProjectDir/$ProjectName.csproj")
$Version = $CsprojContent.Project.PropertyGroup.Version
if ([string]::IsNullOrEmpty($Version)) {
    $Version = "1.0.0" # Fallback
}
Write-Host "Building Version: $Version"

# Clean
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }

# Publish
Write-Host "Publishing Application..."
dotnet publish "$ProjectDir/$ProjectName.csproj" -c Release -r win-x64 --self-contained true -o $PublishDir /p:DebugType=embedded

# Pack
$IconPath = "$ProjectDir/Assets/app-icon.ico"
Write-Host "Packing with Velopack..."
if (Test-Path $IconPath) {
    vpk pack -u $ProjectName -v $Version -p $PublishDir -e "$ProjectName.exe" -i $IconPath -o $ReleaseDir
}
else {
    Write-Warning "Icon not found at $IconPath. Packing without icon."
    vpk pack -u $ProjectName -v $Version -p $PublishDir -e "$ProjectName.exe" -o $ReleaseDir
}

# Rename/Copy artifacts for distribution
$SetupFile = "$ReleaseDir/${ProjectName}-win-Setup.exe"
$PortableFile = "$ReleaseDir/${ProjectName}-win-Portable.zip"

if (Test-Path $SetupFile) {
    Copy-Item $SetupFile "$ReleaseDir/${ProjectName}-Setup-${Version}.exe" -Force
    Write-Host "Created: ${ProjectName}-Setup-${Version}.exe"
}

if (Test-Path $PortableFile) {
    Copy-Item $PortableFile "$ReleaseDir/${ProjectName}-Portable-${Version}.zip" -Force
    Write-Host "Created: ${ProjectName}-Portable-${Version}.zip"
}

Write-Host "Done! Installer is in $ReleaseDir"
