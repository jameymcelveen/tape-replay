# Why TapeReplay Is Pessimistic by Design

TapeReplay exists to tell the truth about whether a day-trading strategy still works **after costs** and **on data you did not tune against**. A single green day is not evidence. This document explains how the engine enforces skepticism.

## The real question

> Does this strategy have a durable edge after costs, or did I curve-fit to one lucky day?

Everything in the product flows from that question.

## Train / test split (the whole point)

| Phase | What you do | Can you trust it? |
|-------|-------------|-------------------|
| **Exploratory** | Single-day quick run | No. Dev convenience only. |
| **In-sample** | Tune freely on a date range, then **Commit** | No. Suspect by design. |
| **Out-of-sample** | **Evaluate** the frozen commit on new dates | **Yes. This is the headline.** |

When you **Commit**, TapeReplay:

1. Freezes your strategy config (no more tuning on that commit).
2. Stores the commit in SQLite.
3. Runs the in-sample window and labels results `InSample`.

When you **Evaluate**, TapeReplay:

1. Loads the frozen config (you cannot change knobs).
2. Runs only the out-of-sample window you specify.
3. Labels results `OutOfSample` and surfaces the verdict.

If in-sample return is dramatically better than out-of-sample, you get an **overfitting warning**. That gap is a classic curve-fit signal.

## Cost assumptions (pessimistic defaults)

Every fill pays:

- **Commission:** $0.005/share + $1.00/trade (per fill leg).
- **Spread:** 5 basis points (buy at ask, sell at bid).
- **Slippage:** 2 basis points adverse per fill.

Costs flow through `ITradeCostModel`. They cannot be silently bypassed. Defaults are realistic-to-pessimistic, never zero.

The UI shows **gross P&L** and **net P&L** side by side. **Net is the headline.**

## Honest metrics (drawdown over return)

| Metric | Why it matters |
|--------|----------------|
| **Max drawdown (% and $)** | Ruin risk. Headline number. |
| **Longest losing streak** | Psychological and capital pressure. |
| **Time to recover** | Or "never recovered" from max drawdown. |
| **Win rate, avg win/loss, payoff, expectancy** | Edge shape, not hype. |
| **Sharpe (approx)** | Return per unit of volatility on the window. |
| **Plain-English verdict** | Honest summary, not encouragement. |

Total return is shown but visually de-emphasized relative to drawdown.

## Look-ahead bar-timing contract

Backtests cheat when bar N's **close/high/low** influence a decision that executes on bar N as if you knew the future.

TapeReplay enforces:

- **Entry on bar N:** `EntryDecisionContext` exposes only prior bars and bar N **open**. No current-bar close/high/low.
- **Entry fill:** pessimistic ask price via cost model.
- **Exits:** intrabar stop/target may use high/low (simulated path). Time exits use bar close at end of bar.

A test strategy that would look amazing with perfect foresight does **not** get that edge in the honest engine.

## Survivorship bias (future)

When multi-ticker backtests arrive, **delisted tickers must be included** or results bias upward. Not built yet. See `docs/scope-and-purpose.md`.

## API endpoints

| Endpoint | Purpose |
|----------|---------|
| `POST /api/backtest/run` | Exploratory single day |
| `POST /api/backtest/commit` | Freeze strategy + in-sample window |
| `POST /api/backtest/evaluate` | Score frozen strategy out-of-sample |

## Design rule

When a choice exists between a result that looks encouraging and one that is honest, TapeReplay chooses honest and makes the assumption explicit in the UI.
