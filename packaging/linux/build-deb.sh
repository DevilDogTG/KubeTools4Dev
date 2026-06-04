#!/usr/bin/env bash
# Build a Debian package for KubeTools4Dev from a dotnet publish output dir.
#
# Usage:
#   build-deb.sh --publish-dir <path> --version <X.Y.Z> --out-dir <path>
#
# Produces: <out-dir>/KubeTools4Dev_<version>_amd64.deb
set -euo pipefail

PUBLISH_DIR=""
VERSION=""
OUT_DIR=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish-dir) PUBLISH_DIR="$2"; shift 2 ;;
    --version)     VERSION="$2";     shift 2 ;;
    --out-dir)     OUT_DIR="$2";     shift 2 ;;
    *) echo "Unknown arg: $1" >&2; exit 64 ;;
  esac
done

[[ -n "$PUBLISH_DIR" ]] || { echo "--publish-dir is required" >&2; exit 64; }
[[ -n "$VERSION"     ]] || { echo "--version is required"     >&2; exit 64; }
[[ -n "$OUT_DIR"     ]] || { echo "--out-dir is required"     >&2; exit 64; }
[[ -d "$PUBLISH_DIR" ]] || { echo "publish dir not found: $PUBLISH_DIR" >&2; exit 66; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

PKG_NAME="kubetools4dev"
DEB_NAME="KubeTools4Dev_${VERSION}_amd64.deb"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

# 1. Layout
install -d "$STAGE/DEBIAN"
install -d "$STAGE/opt/$PKG_NAME"
install -d "$STAGE/usr/bin"
install -d "$STAGE/usr/share/applications"
install -d "$STAGE/usr/share/icons/hicolor/256x256/apps"

# 2. Copy publish output into /opt/kubetools4dev (preserve modes)
cp -a "$PUBLISH_DIR"/. "$STAGE/opt/$PKG_NAME/"

# The published ELF binary's name is locked to the .csproj AssemblyName
# (defaults to the project name "KubeTools4Dev"). Fail loudly with a
# pointer instead of letting chmod surface a cryptic stat error if that
# ever drifts.
APP_BIN="$STAGE/opt/$PKG_NAME/KubeTools4Dev"
if [[ ! -f "$APP_BIN" ]]; then
  echo "ERROR: expected published binary not found at $APP_BIN" >&2
  echo "       Has AssemblyName changed in src/KubeTools4Dev/KubeTools4Dev.csproj?" >&2
  exit 70
fi
chmod 0755 "$APP_BIN"

# 3. /usr/bin launcher (not a symlink: keeps argv[0] sane and lets us set cwd if needed)
cat > "$STAGE/usr/bin/$PKG_NAME" <<'LAUNCHER'
#!/bin/sh
exec /opt/kubetools4dev/KubeTools4Dev "$@"
LAUNCHER
chmod 0755 "$STAGE/usr/bin/$PKG_NAME"

# 4. Desktop entry + icon
install -m 0644 "$SCRIPT_DIR/kubetools4dev.desktop" \
  "$STAGE/usr/share/applications/$PKG_NAME.desktop"
install -m 0644 "$REPO_ROOT/src/KubeTools4Dev/Assets/app-icon.png" \
  "$STAGE/usr/share/icons/hicolor/256x256/apps/$PKG_NAME.png"

# 5. DEBIAN/control with version stamped in
sed "s/\${VERSION}/$VERSION/g" "$SCRIPT_DIR/control.template" > "$STAGE/DEBIAN/control"

# 6. Build
mkdir -p "$OUT_DIR"
dpkg-deb --root-owner-group --build "$STAGE" "$OUT_DIR/$DEB_NAME"

echo "Built: $OUT_DIR/$DEB_NAME"
dpkg-deb --info "$OUT_DIR/$DEB_NAME"
