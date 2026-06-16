# TapeReplay: Scope and Purpose

![TapeReplay layered architecture](tapereplay-hero.png)

## Purpose

TapeReplay is a desktop day trading strategy backtester. It lets a trader configure a strategy, replay one trading day minute by minute against historical OHLCV data, and review trade-level results without risking capital.

The goal is not live trading. It is fast, local iteration on rule sets: entry triggers, position sizing, stop loss, take profit ladders, auto-exit time, and daily risk limits. Results include entry and exit prices, P&L per trade, win rate, max drawdown, and a full trade log.

## What TapeReplay Is

- A **local-first** Electron application wrapping a React UI and an ASP.NET Core 9 API
- A **cache-first** market data pipeline: SQLite stores bars locally; Polygon.io is called only when data is missing
- A **strategy DSL** layer: UI knobs generate readable rules; the backend parses and executes them
- A **pluggable engine**: data providers, repositories, and strategies are interface-driven so implementations can be swapped without rewriting core logic

## MVP Scope (Current)

The MVP proves one end-to-end workflow:

1. Select a stock ticker and a single trading date
2. Configure one strategy (daily high breakout) with these controls:
   - Entry: price breaks above the running daily high
   - Position size (USD)
   - Stop loss (%)
   - Take profit targets (percent and weight)
   - Auto-exit time (e.g. 14:00)
   - Max daily loss and max concurrent trades
3. Backend fetches minute bars from Polygon when not cached, stores them in SQLite
4. Backtest engine replays bars, fires entry and exit signals, tracks open positions, calculates P&L
5. Dashboard displays aggregate metrics and a trade log

Proof-of-concept strategy: Ross Cameron style daily high breakout.

## Architecture (Three Layers)

The hero image reflects the stack:

| Layer | Role |
|-------|------|
| **Electron shell** | Desktop host, launches the .NET backend, loads the React UI |
| **React frontend** | Strategy builder, DSL preview, backtest results dashboard |
| **ASP.NET Core API** | REST endpoints, DSL parser, backtest engine, market data service |

Data flows through a repository abstraction (SQLite today, PostgreSQL later). Market data flows through `IMarketDataProvider` (Polygon today, swappable).

## Explicitly Out of Scope (MVP)

These are planned directions, not current deliverables:

- PostgreSQL cloud backing
- SQLite to cloud sync
- Strategy library with multiple saved strategies
- Multi-day backtests
- Web client hitting cloud data directly
- Advanced charting (candlesticks plus signal overlays)

The codebase is structured so these can be added without a rewrite.

## Key Design Constraints

1. No hardcoded Polygon calls in business logic
2. Repository pattern for all data access (no raw SQLite scattered through services)
3. JSON API boundary between frontend and backend
4. Composition over inheritance for strategies and providers
5. Strategy rules remain human-readable via the DSL

## Success Criteria

A successful MVP run means:

- User configures a strategy in the UI
- DSL is generated and parsed correctly on the backend
- One day of minute bars is loaded (cache or Polygon)
- Backtest completes with trades, P&L, win rate, max drawdown, and trade log
- Application bundles into a single installable Electron app (no separate .NET SDK required for end users)

## Related Docs

- [README](../README.md): quickstart, API endpoints, build commands
- [Makefile](../Makefile): `make dev`, `make bundle`, and other tasks
