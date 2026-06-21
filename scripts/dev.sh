#!/usr/bin/env bash
# Full dev stack: backend + Vite + Electron. Reuses healthy services on :5180 / :5173.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

API="${TAPEREPLAY_API:-http://localhost:5180}"
UI="${DEMO_URL:-http://localhost:5173}"
ELECTRON='wait-on '"$API"'/api/health '"$UI"' && npm run dev:electron'

backend_ok=0
frontend_ok=0

if curl -sf "$API/api/health" >/dev/null 2>&1; then
  backend_ok=1
  echo "Reusing backend at $API"
fi

if curl -sf "$UI" >/dev/null 2>&1; then
  frontend_ok=1
  echo "Reusing frontend at $UI"
fi

if [[ "$backend_ok" -eq 1 && "$frontend_ok" -eq 1 ]]; then
  echo "Starting Electron only."
  exec bash -c "$ELECTRON"
fi

if [[ "$backend_ok" -eq 1 ]]; then
  echo "Starting frontend + Electron."
  exec npx concurrently -k "npm run dev:frontend" "bash -c '$ELECTRON'"
fi

if [[ "$frontend_ok" -eq 1 ]]; then
  echo "Starting backend + Electron."
  exec npx concurrently -k "npm run dev:backend" "bash -c '$ELECTRON'"
fi

echo "Starting backend + frontend + Electron."
exec npx concurrently -k \
  "npm run dev:backend" \
  "npm run dev:frontend" \
  "bash -c '$ELECTRON'"
