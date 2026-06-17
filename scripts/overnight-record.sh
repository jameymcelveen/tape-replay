#!/usr/bin/env bash
# Queue and record all configured tickers until pending queue is empty.
# Usage: ./scripts/overnight-record.sh
set -euo pipefail

API="${TAPEREPLAY_API:-http://localhost:5180}"
TICKERS=("EDHL" "CCHH" "CAST" "VSME" "JRSH")
DATE_FROM="${RECORD_DATE_FROM:-2026-06-11}"
DATE_TO="${RECORD_DATE_TO:-2026-06-17}"
BATCH_SIZE="${RECORD_BATCH_SIZE:-20}"
MAX_ROUNDS="${RECORD_MAX_ROUNDS:-500}"

TICKER_JSON="$(printf '"%s",' "${TICKERS[@]}")"
TICKER_JSON="[${TICKER_JSON%,}]"

echo "Waiting for API at $API..."
for _ in $(seq 1 60); do
  if curl -sf "$API/api/health" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

if ! curl -sf "$API/api/health" >/dev/null 2>&1; then
  echo "API not reachable at $API" >&2
  exit 1
fi

echo "Queueing ${#TICKERS[@]} tickers from $DATE_FROM to $DATE_TO..."
curl -sf -X POST "$API/api/data/queue-minute" \
  -H 'Content-Type: application/json' \
  -d "{\"tickers\":$TICKER_JSON,\"dateFrom\":\"$DATE_FROM\",\"dateTo\":\"$DATE_TO\"}"
echo ""

round=0
total=0
while [ "$round" -lt "$MAX_ROUNDS" ]; do
  result="$(curl -sf -X POST "$API/api/data/record?batchSize=$BATCH_SIZE&maxRounds=1")"
  recorded="$(echo "$result" | sed -n 's/.*"totalRecorded":\([0-9]*\).*/\1/p')"
  recorded="${recorded:-0}"
  round=$((round + 1))
  total=$((total + recorded))
  echo "Round $round: recorded $recorded (total $total)"
  if [ "$recorded" -eq 0 ]; then
    break
  fi
  sleep 1
done

echo ""
echo "Coverage summary:"
for ticker in "${TICKERS[@]}"; do
  done_count="$(curl -sf "$API/api/data/coverage/minute?ticker=$ticker&startDate=$DATE_FROM&endDate=$DATE_TO" \
    | grep -o '"status":"Done"' | wc -l | tr -d ' ')"
  skipped_count="$(curl -sf "$API/api/data/coverage/minute?ticker=$ticker&startDate=$DATE_FROM&endDate=$DATE_TO" \
    | grep -o '"status":"Skipped"' | wc -l | tr -d ' ')"
  pending_count="$(curl -sf "$API/api/data/coverage/minute?ticker=$ticker&startDate=$DATE_FROM&endDate=$DATE_TO" \
    | grep -o '"status":"Pending"' | wc -l | tr -d ' ')"
  echo "  $ticker: Done=$done_count Skipped=$skipped_count Pending=$pending_count"
done

echo ""
echo "Finished. Total cells recorded this run: $total"
