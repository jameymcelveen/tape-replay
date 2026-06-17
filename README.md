# TapeReplay

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18+-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![Electron](https://img.shields.io/badge/Electron-42-47848F?logo=electron&logoColor=white)](https://www.electronjs.org/)
[![Node.js](https://img.shields.io/badge/Node.js-20+-339933?logo=node.js&logoColor=white)](https://nodejs.org/)
[![SQLite](https://img.shields.io/badge/SQLite-cache-003B57?logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![Platform](https://img.shields.io/badge/platform-macOS%20%7C%20Windows%20%7C%20Linux-lightgrey)](Makefile)
[![GitHub last commit](https://img.shields.io/github/last-commit/jameymcelveen/tape-replay)](https://github.com/jameymcelveen/tape-replay/commits/main)

Day trading strategy backtester built with Electron, React, and ASP.NET Core 9.

## Related Docs

- [Scope and Purpose](docs/scope-and-purpose.md)
- [Strategy Designer](strategy-designer.md) (target UI mockup)
- [Honesty by design](docs/honesty.md)
- [JS patch updates (CDN)](docs/updates.md)

## Stack

- **Frontend:** React 18+, Tailwind CSS, Electron shell
- **Backend:** ASP.NET Core 9 REST API on `http://localhost:5180`
- **Data:** SQLite cache (EF Core repository pattern, PostgreSQL-ready)
- **Market data:** Polygon.io behind `IMarketDataProvider` (mock provider for local dev)

## Quickstart

### Prerequisites

- .NET 9 SDK
- Node.js 20+
- Polygon.io API key (optional for mock mode)

### Install

```bash
npm install
npm install --prefix frontend
dotnet restore backend/TapeReplay.Api.csproj
```

### Run (development)

Starts the .NET API, Vite dev server, and Electron window:

```bash
npm run dev
```

Or run pieces separately:

```bash
npm run dev:backend
npm run dev:frontend
npm run dev:electron
```

### Polygon API key (local only — never commit)

Copy the example local config (gitignored):

```bash
cp backend/appsettings.Development.local.json.example backend/appsettings.Development.local.json
```

Edit `backend/appsettings.Development.local.json` with your Polygon key and any recording date ranges.  
**Do not** put keys or job dates in `appsettings.Development.json` — that file is tracked in git.

Alternatively set an environment variable:

```bash
export Polygon__ApiKey=your_key_here
```

With `UseMockProvider: true` (default in `appsettings.json`), the API serves synthetic minute bars so you can test without Polygon.

## Collecting market data (publisher)

Prerequisites: Polygon key in local config, `DataDistribution:Role` = `Both` or `Publisher`, `ScraperEnabled: true`.

### 1. Start the stack

```bash
make install   # first time
make dev       # API + UI + Electron
```

### 2. Queue tickers and dates, then record

Dates go in your **local** config or on the command line — not in the repo.

```bash
chmod +x scripts/record.sh
./scripts/record.sh "AAPL,MSFT" 2024-06-03 2024-06-07
```

Or step by step:

```bash
curl -X POST http://localhost:5180/api/data/queue-minute \
  -H 'Content-Type: application/json' \
  -d '{"tickers":["AAPL"],"dateFrom":"2024-06-03","dateTo":"2024-06-07"}'

curl -X POST http://localhost:5180/api/data/record?batchSize=20
```

Jobs in `appsettings.Development.local.json` run automatically on startup when `runOnStartup` is true (restart `make dev` after editing).

Check coverage (pretty-print with jq):

```bash
curl -s 'http://localhost:5180/api/data/coverage/minute?ticker=EDHL&startDate=2026-06-11&endDate=2026-06-15' | jq .
```

Status values: `Done` (recorded), `Pending` (queued), `Skipped` (weekend).

### 3. Publish to CDN (optional)

```bash
make publish-data
cd publish/data && surge . tapereplay.surge.sh/data
```

SQLite (`tapereplay.db`) and `publish/data/` are gitignored — collected data never gets committed.

See [docs/data-distribution.md](docs/data-distribution.md) for subscriber sync and partition details.

## Why this tool is pessimistic by design

TapeReplay measures whether a strategy survives **after costs** on **data you did not tune against**. A single green day is not evidence. See [docs/honesty.md](docs/honesty.md) for train/test split, cost defaults, metrics, and the look-ahead contract.

## MVP flow

1. Configure strategy knobs (entry, sizing, exits, risk).
2. **Tune** on an in-sample date range, then **Commit** (freezes config).
3. **Evaluate** on an out-of-sample range (headline results, overfitting warning if in-sample looked too good).
4. Optional: **Exploratory Day** for quick dev (labeled "not evidence").
5. Review honest metrics: max drawdown headlines, net-after-costs P&L, plain-English verdict.

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/health` | Health check |
| POST | `/api/strategy/parse` | Parse DSL to `StrategyConfig` |
| POST | `/api/strategy/generate` | Generate DSL from config |
| POST | `/api/backtest/run` | Exploratory single-day backtest |
| POST | `/api/backtest/commit` | Freeze strategy + in-sample window |
| POST | `/api/backtest/evaluate` | Score frozen strategy out-of-sample |
| GET | `/api/marketdata/{ticker}/{date}` | Load or fetch minute bars |
| POST | `/api/data/queue-minute` | Queue tickers + date range for recording |
| POST | `/api/data/record` | Scrape until pending queue empty |
| POST | `/api/data/scrape` | Scrape one batch of pending cells |
| POST | `/api/data/publish` | Export partitions to `publish/data/` |
| POST | `/api/data/sync` | Import partitions from CDN |

## Project layout

```text
electron/     Electron entry, launches .NET backend
frontend/     React UI
backend/      ASP.NET Core API, backtest engine, data layer
```

## Installers

### macOS (local)

```bash
make installer-mac          # DMG + ZIP for this Mac's CPU
make installer-mac-arm64    # Apple Silicon
make installer-mac-x64      # Intel Mac
```

Output: `release/TapeReplay-0.1.0-mac-arm64.dmg` (and `.zip`).

Open the DMG, drag TapeReplay to Applications.

### Windows

On a Windows machine (or GitHub Actions):

```bash
make installer-win
```

Output: `release/TapeReplay-0.1.0-Setup.exe` (NSIS installer), plus `.zip` and portable `.exe`.

Building on macOS produces a Windows **zip/portable** only (NSIS requires Windows). GitHub Actions builds both platform installers **only on major or minor version tags**:

```bash
git tag v0.2.0 && git push origin v0.2.0   # builds installers
git tag v0.1.1 && git push origin v0.1.1   # skipped (patch tag, use CDN patch instead)
```

### JS patch auto-update (CDN / surge.sh)

Between installer releases, ship frontend-only zips. The app fetches `manifest.json` on startup and unpacks `patch_ver_X.Y.Z.zip` when newer.

```bash
make cdn-dist PATCH=0.1.1 SURGE_DOMAIN=tapereplay.surge.sh
cd dist && surge . tapereplay.surge.sh
```

`dist/` contains deploy artifacts: `manifest.json`, the patch zip, and optionally matching installers from `release/` (`INCLUDE_INSTALLERS=1`).

Configure `electron/update-config.json` with your surge URLs before `make installer-mac`. See [docs/updates.md](docs/updates.md).

### Quick bundle (current platform)

```bash
make bundle
```

Installers land in `release/`.

Other useful targets:

```bash
make help              # list all tasks
make install           # npm + dotnet restore
make dev               # hot-reload development stack
make build             # stage artifacts without packaging
make run               # run production shell locally (unbundled)
make test              # build + backend health smoke test
make clean             # remove artifacts and release output
```

Unbundled production run (without electron-builder):

```bash
make run
```
