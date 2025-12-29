#!/bin/bash
set -e

# Configuration
PROJECT_NAME="KubeTools4Dev"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/../src/$PROJECT_NAME"
PUBLISH_DIR="$SCRIPT_DIR/../dist/linux-x64"
RELEASE_DIR="$SCRIPT_DIR/../dist/Releases-Linux"

# Ensure dotnet tools are in PATH
export PATH="$PATH:$HOME/.dotnet/tools"

# Dependencies
if ! command -v vpk &> /dev/null; then
    echo "vpk not found. Installing..."
    dotnet tool update -g vpk || dotnet tool install -g vpk
fi

# Get Version
# parsing .csproj using sed
VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$PROJECT_DIR/$PROJECT_NAME.csproj" | head -n 1)
if [ -z "$VERSION" ]; then
    echo "Error: Could not extract <Version> from $PROJECT_DIR/$PROJECT_NAME.csproj" >&2
    exit 1
fi
echo "Building Version: $VERSION"

# Clean
rm -rf "$PUBLISH_DIR"

# Publish
echo "Publishing Application (Linux x64)..."
dotnet publish "$PROJECT_DIR/$PROJECT_NAME.csproj" -c Release -r linux-x64 --self-contained true -o "$PUBLISH_DIR" /p:DebugType=embedded

# Pack
echo "Packing with Velopack..."
ICON_PATH="$PROJECT_DIR/Assets/app-icon.png"

# Note: vpk pack for linux produces AppImage
vpk pack -u "$PROJECT_NAME" -v "$VERSION" -p "$PUBLISH_DIR" -e "$PROJECT_NAME" -i "$ICON_PATH" -o "$RELEASE_DIR"

# Cleanup
rm -f "$RELEASE_DIR"/*.nupkg

echo "Done! Linux Installer is in $RELEASE_DIR"
