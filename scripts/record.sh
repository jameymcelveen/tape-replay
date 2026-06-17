#!/usr/bin/env bash
# Queue and record minute bars from Polygon (API key in appsettings.Development.local.json).
# Usage: ./scripts/record.sh [tickers_csv] [date_from] [date_to]
# Example: ./scripts/record.sh "EDHL,CCHH,CAST,VSME,JRSH" 2026-06-11 2026-06-15
# Or:    make record TICKERS="AAPL,MSFT" DATE_FROM=2024-06-03 DATE_TO=2024-06-07
set -euo pipefail

API="${TAPEREPLAY_API:-http://localhost:5180}"
TICKERS="${1:-AAPL}"
DATE_FROM="${2:-2024-06-03}"
DATE_TO="${3:-2024-06-03}"
BATCH_SIZE="${RECORD_BATCH_SIZE:-20}"

echo "Waiting for API at $API..."
for _ in $(seq 1 60); do
  if curl -sf "$API/api/health" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

if ! curl -sf "$API/api/health" >/dev/null 2>&1; then
  echo "API not reachable at $API (start with: make dev)" >&2
  exit 1
fi

IFS=',' read -ra TICKER_ARR <<< "$TICKERS"
TICKER_JSON="$(printf '"%s",' "${TICKER_ARR[@]}")"
TICKER_JSON="[${TICKER_JSON%,}]"

echo "Queueing $TICKERS from $DATE_FROM to $DATE_TO..."
curl -sf -X POST "$API/api/data/queue-minute" \
  -H 'Content-Type: application/json' \
  -d "{\"tickers\":$TICKER_JSON,\"dateFrom\":\"$DATE_FROM\",\"dateTo\":\"$DATE_TO\"}"
echo ""

echo "Recording from Polygon (runs until pending queue is empty; respects rate limits)..."
curl -sf -X POST "$API/api/data/record?batchSize=$BATCH_SIZE" | (command -v jq >/dev/null && jq . || cat)
echo ""
