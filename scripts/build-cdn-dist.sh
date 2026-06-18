#!/usr/bin/env bash
# Build dist/ for CDN deploy (e.g. surge.sh).
# Contains manifest.json, patch zip, and optional installer artifacts from release/.
#
# Usage:
#   ./scripts/build-cdn-dist.sh [patch-version]
#   PATCH=0.1.1 SURGE_DOMAIN=tapereplay.surge.sh make cdn-dist
#
# Env:
#   PATCH / arg          Patch version (default: package.json tapereplay.patchVersion)
#   SURGE_DOMAIN         e.g. tapereplay.surge.sh (default: tapereplay.surge.sh)
#   CDN_BASE_URL         Full base URL (overrides SURGE_DOMAIN)
#   RELEASE_NOTES        Optional string for manifest.json
#   SKIP_INSTALLERS=1    Do not copy files from release/ (default)
#   INCLUDE_INSTALLERS=1 Copy matching installers from release/
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST_DIR="$ROOT/dist"
PATCH_VERSION="${1:-${PATCH:-}}"
INSTALLER_VERSION="$(node -p "require('$ROOT/package.json').version")"

if [[ -z "$PATCH_VERSION" ]]; then
  PATCH_VERSION="$(node -p "require('$ROOT/package.json').tapereplay.patchVersion")"
fi

if [[ -z "$PATCH_VERSION" ]]; then
  echo "Patch version required. Set PATCH=0.1.1 or bump package.json tapereplay.patchVersion." >&2
  exit 1
fi

if [[ -n "${CDN_BASE_URL:-}" ]]; then
  CDN_BASE="${CDN_BASE_URL%/}"
elif [[ -n "${SURGE_DOMAIN:-}" ]]; then
  CDN_BASE="https://${SURGE_DOMAIN%/}"
else
  CDN_BASE="https://tapereplay.surge.sh"
fi

ZIP_NAME="patch_ver_${PATCH_VERSION}.zip"
RELEASED_AT="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
RELEASE_NOTES="${RELEASE_NOTES:-}"

cd "$ROOT"
echo "Building frontend..."
npm run build:frontend --silent

mkdir -p "$DIST_DIR"
rm -f "$DIST_DIR"/*.zip "$DIST_DIR/manifest.json"

ZIP_PATH="$DIST_DIR/$ZIP_NAME"
rm -f "$ZIP_PATH"
(cd "$ROOT/frontend/dist" && zip -r -q "$ZIP_PATH" .)

echo "Copying help site to dist/help/..."
rm -rf "$DIST_DIR/help"
mkdir -p "$DIST_DIR/help"
cp -R "$ROOT/docs/help/." "$DIST_DIR/help/"
# Remove dev-only partials if present
rm -rf "$DIST_DIR/help/partials"

if command -v shasum >/dev/null 2>&1; then
  SHA256="$(shasum -a 256 "$ZIP_PATH" | awk '{print $1}')"
elif command -v sha256sum >/dev/null 2>&1; then
  SHA256="$(sha256sum "$ZIP_PATH" | awk '{print $1}')"
else
  echo "shasum or sha256sum required." >&2
  exit 1
fi

MANIFEST_PATH="$DIST_DIR/manifest.json"
node --input-type=module -e "
import fs from 'node:fs';
const manifest = {
  installerVersion: '$INSTALLER_VERSION',
  latestPatchVersion: '$PATCH_VERSION',
  patchFile: '$ZIP_NAME',
  sha256: '$SHA256',
  cdnBaseUrl: '$CDN_BASE',
  releasedAt: '$RELEASED_AT',
};
const notes = process.env.RELEASE_NOTES ?? '';
if (notes) manifest.releaseNotes = notes;
fs.writeFileSync('$MANIFEST_PATH', JSON.stringify(manifest, null, 2) + '\n');
"

if [[ "${INCLUDE_INSTALLERS:-}" == "1" ]] && [[ -d "$ROOT/release" ]]; then
  shopt -s nullglob
  for artifact in "$ROOT/release"/*; do
    base="$(basename "$artifact")"
    case "$base" in
      *.dmg|*.exe|*.zip|*.AppImage)
        if [[ "$base" == *"${INSTALLER_VERSION}"* ]]; then
          cp -f "$artifact" "$DIST_DIR/"
          echo "Included installer: $base"
        fi
        ;;
    esac
  done
  shopt -u nullglob
fi

echo "Generating CDN landing page..."
node "$ROOT/scripts/generate-cdn-index.mjs" "$DIST_DIR" "$ROOT"

cat <<EOF

CDN dist ready: $DIST_DIR/
  index.html        (landing page + download links)
  manifest.json
  $ZIP_NAME
  help/             (static documentation)
  SHA256: $SHA256

cdnBaseUrl: $CDN_BASE

Deploy:
  cd dist && surge . \${SURGE_DOMAIN:-tapereplay.surge.sh}

Set electron/update-config.json before building installers:
  manifestUrl: $CDN_BASE/manifest.json
  cdnBaseUrl:  $CDN_BASE

EOF
