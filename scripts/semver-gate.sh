#!/usr/bin/env bash
# Exit 0 when the tag warrants a full installer build (major or minor bump).
# Exit 1 for patch-only bumps (e.g. v0.1.0 -> v0.1.1).
set -euo pipefail

TAG="${1:-${GITHUB_REF_NAME:-}}"
TAG="${TAG#v}"

if [[ -z "$TAG" ]]; then
  echo "Usage: semver-gate.sh v0.2.0" >&2
  exit 1
fi

IFS='.' read -r CUR_MAJ CUR_MIN CUR_PAT <<< "$TAG"
CUR_MAJ="${CUR_MAJ:-0}"
CUR_MIN="${CUR_MIN:-0}"
CUR_PAT="${CUR_PAT:-0}"

PREV_TAG="$(git tag -l 'v*' --sort=-v:refname | while read -r candidate; do
  candidate="${candidate#v}"
  [[ "$candidate" == "$TAG" ]] && continue
  echo "$candidate"
  break
done)"

if [[ -z "$PREV_TAG" ]]; then
  echo "No previous tag. Building installers for first release v$TAG."
  exit 0
fi

IFS='.' read -r PREV_MAJ PREV_MIN PREV_PAT <<< "$PREV_TAG"
PREV_MAJ="${PREV_MAJ:-0}"
PREV_MIN="${PREV_MIN:-0}"
PREV_PAT="${PREV_PAT:-0}"

if (( CUR_MAJ > PREV_MAJ )) || (( CUR_MAJ == PREV_MAJ && CUR_MIN > PREV_MIN )); then
  echo "Installer build required: v$PREV_TAG -> v$TAG (major or minor bump)."
  exit 0
fi

echo "Skipping installer build: v$PREV_TAG -> v$TAG is a patch-only bump."
echo "Publish a JS patch zip to the CDN instead (make build-patch)."
exit 1
