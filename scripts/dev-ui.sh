#!/usr/bin/env bash
# Start Vite and/or backend for browser demos. Reuses healthy services already on :5173 / :5180.
set -euo pipefail

API="${TAPEREPLAY_API:-http://localhost:5180}"
UI="${DEMO_URL:-http://localhost:5173}"

backend_ok=0
frontend_ok=0

if curl -sf "$API/api/health" >/dev/null 2>&1; then
  backend_ok=1
fi

if curl -sf "$UI" >/dev/null 2>&1; then
  frontend_ok=1
fi

if [[ "$backend_ok" -eq 1 && "$frontend_ok" -eq 1 ]]; then
  echo "Backend ($API) and frontend ($UI) are already running."
  echo "Leave this terminal open or Ctrl+C when finished demoing."
  tail -f /dev/null
fi

if [[ "$backend_ok" -eq 1 ]]; then
  echo "Backend already running at $API. Starting Vite at $UI"
  exec npm run dev --prefix frontend
fi

if [[ "$frontend_ok" -eq 1 ]]; then
  echo "Frontend already running at $UI. Starting backend at $API"
  exec npm run dev:backend
fi

echo "Starting backend + Vite..."
exec npx concurrently -k "npm run dev:backend" "npm run dev:frontend"
