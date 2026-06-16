#!/usr/bin/env bash
# Build frontend patch zip into dist/ (same output as cdn-dist).
# Usage: ./scripts/build-patch.sh 0.1.1
set -euo pipefail

PATCH_VERSION="${1:?Usage: build-patch.sh <patch-version e.g. 0.1.1>}"
exec "$(dirname "$0")/build-cdn-dist.sh" "$PATCH_VERSION"
