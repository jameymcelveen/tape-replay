#!/usr/bin/env bash
# Verify matt-five tickers have minute bars in SQLite.
set -euo pipefail

DB="${1:-backend/tapereplay.db}"
DATE_FROM="${2:-2026-06-11}"
DATE_TO="${3:-2026-06-16}"

if [ ! -f "$DB" ]; then
  echo "Database not found: $DB" >&2
  exit 1
fi

echo "MarketData in $DB ($DATE_FROM to $DATE_TO):"
sqlite3 -header -column "$DB" "
SELECT Ticker, date(DateTime) AS Day, COUNT(*) AS Bars
FROM MarketData
WHERE Ticker IN ('EDHL','CCHH','CAST','VSME','JRSH')
  AND date(DateTime) BETWEEN '$DATE_FROM' AND '$DATE_TO'
GROUP BY Ticker, date(DateTime)
ORDER BY Ticker, Day;
"

echo ""
echo "Coverage:"
sqlite3 -header -column "$DB" "
SELECT Ticker, Date, Status, Provenance
FROM ticker_minute_coverage
WHERE Ticker IN ('EDHL','CCHH','CAST','VSME','JRSH')
  AND Date BETWEEN '$DATE_FROM' AND '$DATE_TO'
ORDER BY Ticker, Date;
"
