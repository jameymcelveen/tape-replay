#!/usr/bin/env bash
# Preflight for Playwright demo scripts.
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
  exit 0
fi

echo "Demo preflight failed."
[[ "$backend_ok" -eq 0 ]] && echo "  Backend not running at $API"
[[ "$frontend_ok" -eq 0 ]] && echo "  Frontend not running at $UI"
echo ""
echo "Start both in another terminal, then rerun the demo:"
echo "  npm run dev:ui"
echo ""
exit 1
