# Market data CDN distribution

TapeReplay can publish recorded market data to a **static CDN** (separate from app installer / JS patch updates). One machine records (publisher); another imports (subscriber) without running the scraper.

## Separation from app updates

| Concern | CDN path | Manifest |
|---------|----------|----------|
| App installer + JS patches | `https://tapereplay.surge.sh/` | `dist/manifest.json` |
| Market data partitions | `https://tapereplay.surge.sh/data/` | `publish/data/manifest.json` |

Data sync is runtime-only. App updates do not redownload the dataset; data sync does not touch app binaries.

## Roles (`DataDistribution:Role`)

| Role | Scraper default | Publish | Subscribe |
|------|-----------------|---------|-----------|
| `Publisher` | On | Yes | No |
| `Subscriber` | Off | No | Yes |
| `Both` | On | Yes | Yes |

Configure in `backend/appsettings.json` or `appsettings.Development.json`:

```json
"DataDistribution": {
  "Role": "Both",
  "ManifestUrl": "https://tapereplay.surge.sh/data/manifest.json",
  "CdnBaseUrl": "https://tapereplay.surge.sh/data",
  "PublishDirectory": "publish/data",
  "SyncOnLaunch": true,
  "ScraperEnabled": null,
  "IncludeBootstrapArchive": true
}
```

## Partition layout

- **Minute bars:** Parquet + zstd, key `{TICKER}_{yyyy}_{MM}` (e.g. `AAPL_2024_06`)
- **Daily bars:** Parquet + zstd, key `{yyyy}_{MM}` (all tickers in one file per month)
- **Filenames:** content-addressed `{sha256}.parquet` (immutable on CDN)

Partial months get a new hash on each publish until the month is complete.

## Publisher workflow

1. Record data (scraper or backtests fill SQLite + coverage grid).
2. Export changed partitions:

```bash
curl -X POST http://localhost:5180/api/data/publish
```

Response includes `publishDirectory` and `suggestedSyncCommand`, e.g.:

```bash
cd publish/data && surge . tapereplay.surge.sh/data
```

No CDN credentials are stored in the app. You sync `publish/data/` with surge, rsync, or S3 yourself.

## Subscriber workflow

On launch (when `SyncOnLaunch: true`) or on demand:

```bash
curl -X POST http://localhost:5180/api/data/sync
```

1. Fetch `manifest.json`
2. Diff partition hashes vs local `data_partition_imports`
3. Optionally fetch bootstrap tar for first-time seeding
4. Download only new/changed `{sha256}.parquet` files
5. Verify SHA256, import into SQLite, mark coverage `Done` with provenance `Published`

Re-run with no upstream changes → zero downloads.

## Coverage grid

Tables: `ticker_minute_coverage`, `market_daily_coverage`

| Field | Meaning |
|-------|---------|
| `Status` | `Pending` or `Done` |
| `Provenance` | `Api` (scraper) or `Published` (CDN import) |

Scraper skips any cell already `Done` regardless of provenance.

Query grid:

```bash
curl 'http://localhost:5180/api/data/coverage/minute?ticker=AAPL&startDate=2024-06-01&endDate=2024-06-30'
```

## API summary

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/data/config` | Role and URLs |
| POST | `/api/data/publish` | Export to `publish/data/` |
| POST | `/api/data/sync` | Pull from CDN |
| POST | `/api/data/scrape` | Run scraper batch |
| GET | `/api/data/coverage/minute` | Minute coverage grid |
| GET | `/api/data/coverage/daily` | Daily coverage grid |

## manifest.json schema

```json
{
  "datasetVersion": "1",
  "schemaVersion": "1",
  "generatedAt": "2026-06-16T20:00:00Z",
  "partitions": [
    {
      "kind": "minute",
      "key": "AAPL_2024_06",
      "filename": "abc123....parquet",
      "sha256": "abc123...",
      "rowCount": 3900,
      "byteSize": 120000,
      "covers": {
        "tickers": ["AAPL"],
        "startDate": "2024-06-03",
        "endDate": "2024-06-28"
      }
    }
  ],
  "bootstrap": {
    "filename": "bootstrap/deadbeef....tar",
    "sha256": "deadbeef...",
    "byteSize": 5000000
  },
  "signature": null
}
```

`signature` is reserved for future manifest signing.

## Tests

```bash
dotnet test backend/tests/TapeReplay.Api.Tests -c Release
```

Covers Parquet round-trip, incremental publish, and scraper skip for published coverage.
