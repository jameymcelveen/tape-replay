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

### Polygon API key

Set your key in `backend/appsettings.Development.json`:

```json
{
  "Polygon": {
    "ApiKey": "YOUR_KEY_HERE"
  },
  "MarketData": {
    "UseMockProvider": false
  }
}
```

With `UseMockProvider: true` (default in `appsettings.json`), the API serves synthetic minute bars so you can test without Polygon.

## MVP flow

1. Select ticker and date in the Strategy Builder.
2. Configure position size, stop loss, take profit targets, auto-exit time, and risk limits.
3. Preview the generated strategy DSL.
4. Run backtest: backend loads cached bars or fetches from Polygon, replays minute bars, returns P&L and trade log.
5. Review results in the dashboard.

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/health` | Health check |
| POST | `/api/strategy/parse` | Parse DSL to `StrategyConfig` |
| POST | `/api/strategy/generate` | Generate DSL from config |
| POST | `/api/backtest/run` | Run single-day backtest |
| GET | `/api/marketdata/{ticker}/{date}` | Load or fetch minute bars |

## Project layout

```text
electron/     Electron entry, launches .NET backend
frontend/     React UI
backend/      ASP.NET Core API, backtest engine, data layer
```

## Build for production

Stage frontend and self-contained backend, then package a single Electron app:

```bash
make bundle
```

Installers land in `release/` (DMG + ZIP on macOS, NSIS on Windows, AppImage on Linux).

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
