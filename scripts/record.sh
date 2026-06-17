#!/usr/bin/env bash
# Queue and record minute bars using local config (API key in appsettings.Development.local.json).
# Usage: ./scripts/record.sh [tickers_csv] [date_from] [date_to]
# Example: ./scripts/record.sh "EDHL,CCHH,CAST,VSME,JRSH" 2026-06-11 2026-06-15
set -euo pipefail

API="${TAPEREPLAY_API:-http://localhost:5180}"
TICKERS="${1:-AAPL}"
DATE_FROM="${2:-2024-06-03}"
DATE_TO="${3:-2024-06-03}"

IFS=',' read -ra TICKER_ARR <<< "$TICKERS"
TICKER_JSON="$(printf '"%s",' "${TICKER_ARR[@]}")"
TICKER_JSON="[${TICKER_JSON%,}]"

echo "Queueing $TICKERS from $DATE_FROM to $DATE_TO..."
curl -sf -X POST "$API/api/data/queue-minute" \
  -H 'Content-Type: application/json' \
  -d "{\"tickers\":$TICKER_JSON,\"dateFrom\":\"$DATE_FROM\",\"dateTo\":\"$DATE_TO\"}"

echo ""
echo "Recording (scrape until pending queue is empty)..."
curl -sf -X POST "$API/api/data/record?batchSize=20"
echo ""
