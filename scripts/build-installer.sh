#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/build-installer.sh mac|win [arm64|x64]
PLATFORM="${1:?Usage: build-installer.sh mac|win [arch]}"
ARCH="${2:-}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ARTIFACTS="$ROOT/artifacts/backend"
CONFIGURATION="${CONFIGURATION:-Release}"

cd "$ROOT"

case "$PLATFORM" in
  mac)
    if [[ -z "$ARCH" ]]; then
      ARCH="$(uname -m)"
    fi
    case "$ARCH" in
      arm64) RID="osx-arm64" ;;
      x86_64|x64) RID="osx-x64"; ARCH="x64" ;;
      *) echo "Unsupported Mac arch: $ARCH" >&2; exit 1 ;;
    esac
  ;;
  win)
    RID="win-x64"
    ARCH="x64"
  ;;
  *)
    echo "Unsupported platform: $PLATFORM (use mac or win)" >&2
    exit 1
  ;;
esac

echo "==> Building frontend"
npm run build:frontend

echo "==> Publishing .NET backend for $RID"
mkdir -p "$ARTIFACTS"
dotnet publish "$ROOT/backend/TapeReplay.Api.csproj" \
  -c "$CONFIGURATION" \
  -r "$RID" \
  --self-contained true \
  -o "$ARTIFACTS" \
  /p:PublishSingleFile=false \
  /p:IncludeNativeLibrariesForSelfExtract=true

EXE_NAME="TapeReplay.Api"
if [[ "$PLATFORM" == "win" ]]; then
  EXE_NAME="TapeReplay.Api.exe"
fi

test -f "$ARTIFACTS/$EXE_NAME" || { echo "Backend executable missing: $ARTIFACTS/$EXE_NAME" >&2; exit 1; }

export CSC_IDENTITY_AUTO_DISCOVERY=false

is_windows() {
  case "$(uname -s)" in
    CYGWIN*|MINGW*|MSYS*) return 0 ;;
  esac
  [[ "${RUNNER_OS:-}" == "Windows" ]] && return 0
  [[ "${OS:-}" == "Windows_NT" ]] && return 0
  return 1
}

echo "==> Packaging Electron installer ($PLATFORM $ARCH)"
case "$PLATFORM" in
  mac)
    npx electron-builder --mac dmg zip "--$ARCH"
    ;;
  win)
    if is_windows; then
      npx electron-builder --win nsis zip portable --x64
    else
      echo "Note: building Windows zip/portable on macOS (NSIS Setup.exe requires Windows)."
      npx electron-builder --win zip portable --x64
    fi
    ;;
esac

echo ""
echo "Installers written to $ROOT/release/"
