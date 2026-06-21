# Spec: Anti-Day-Trading Educational Engine

## Scope & Purpose

TapeReplay is "pessimistic by design." While retail day-traders operate under the illusion of easy, repeatable intraday gains, this subsystem provides data-driven evidence proving why day-trading is a mathematically losing strategy for 97%+ of retail participants.

This document defines the data points, backend indicators, and pre-packaged simulation tests required to expose market friction, overnight growth dependency, and intraday volatility.

---

## 1. Target Tickers (Free-Tier Polygon Ingest)

Because our background trickle-scraper works within Polygon's free-tier limitations (5 calls/min, 1-minute bars), we optimize our data collection by splitting targets into two distinct buckets:

### A. Mega-Cap Volume Monsters (The Liquid Traps)

Highly liquid giants that retail day-traders love to scalp because of high daily headline visibility:

- `TSLA` (Tesla)
- `NVDA` (Nvidia)
- `AMD` (Advanced Micro Devices)
- `AAPL` (Apple)
- `QQQ` (Invesco QQQ Trust ETF)

### B. Micro-Cap Catalyst Chasers (The Pump-and-Dumps)

Low-float, high-volatility micro-caps (under $10) frequently targeted by financial influencers or social media hype:

- `RGTI` (Rigetti Computing)
- `SOUN` (SoundHound AI)
- `BBAI` (BigBear.ai)
- `HOLO` (MicroCloud Hologram)

---

## 2. Extended Market Data Schema

To expose the specific structural failure points of day trading, our database schema requires fields that calculate intraday behavior rather than just standard closing values.

### Historical Extensions Table

```sql
CREATE TABLE IF NOT EXISTS daily_anti_trading_metrics (
    ticker TEXT NOT NULL,
    trade_date TEXT NOT NULL,
    open_price REAL,
    high_price REAL,
    low_price REAL,
    close_price REAL,
    volume INTEGER,

    -- Educational Metrics
    opening_range_high_5min REAL,    -- High of the first 5 minutes (9:30–9:35 AM)
    max_intraday_drawdown REAL,     -- ((Low - Open) / Open) * 100
    overnight_gap_percent REAL,     -- ((Today Open - Yesterday Close) / Yesterday Close) * 100
    intraday_return_percent REAL,   -- ((Close - Open) / Open) * 100
    atr_14 REAL,                    -- Average True Range (14-day volatility measure)

    PRIMARY KEY (ticker, trade_date)
);
```

---

## 3. Core Backtest Proofs (The "De-Hyping" Scenarios)

### Scenario 1: The "9:30 AM Breakout" Trap

- **The Logic:** Buys a hype stock if it breaks the 5-minute opening range high. Sets a 1.5% trailing stop-loss, exiting at 4:00 PM.
- **The Reality:** Retail traders act as exit liquidity for institutional algos. The morning spike is frequently the absolute high of the day before a steady afternoon bleed.
- **The Output:** Massive win-rate decay over out-of-sample data.

### Scenario 2: Intraday Scalping vs. Passive Holding

- **The Logic:** Compares a strategy that buys at 9:30 AM and sells at 4:00 PM daily vs. a simple Buy and Hold strategy.
- **The Reality:** The overwhelming majority of net multi-month market returns occur _overnight_ (gapping up or down due to global news and earnings).
- **The Output:** A dual-line chart showing a flat/negative intraday equity curve contrasted against an upward-trending buy-and-hold curve.

### Scenario 3: The Friction Engine (The Invisible Tax)

- **The Logic:** Evaluates any strategy with a toggle for **Realistic Market Friction** (0.05% per trade accounting for bid-ask spread slippage and SEC/broker fees).
- **The Reality:** Frequent trading multiplies friction losses.
- **The Output:** An equity curve that looks mildly profitable or flat without friction, but aggressively bankrupts the account over a 100-day out-of-sample timeline once friction is checked.
