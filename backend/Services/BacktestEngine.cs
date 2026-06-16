using System.Globalization;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Minute-by-minute backtest state machine.
/// </summary>
public sealed class BacktestEngine(IStrategy strategy) : IBacktestEngine
{
    public BacktestResult Run(string ticker, DateOnly date, StrategyConfig config, IReadOnlyList<Candle> bars)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);
        ArgumentNullException.ThrowIfNull(config);

        if (bars.Count == 0)
        {
            return EmptyResult(ticker, date, config.Name);
        }

        var orderedBars = bars.OrderBy(b => b.DateTime).ToList();
        var trades = new List<TradeResult>();
        var log = new List<string>();
        var openPositions = new List<OpenPosition>();
        var dailyPnL = 0m;
        var equityCurve = new List<decimal> { 0m };
        var runningDailyHigh = orderedBars[0].High;
        var tradeCounter = 0;

        for (var index = 0; index < orderedBars.Count; index++)
        {
            var bar = orderedBars[index];
            var marketTime = TimeOnly.FromDateTime(bar.DateTime);
            var previousDailyHigh = runningDailyHigh;

            var context = new BarContext
            {
                Bar = bar,
                RunningDailyHigh = previousDailyHigh,
                MarketTime = marketTime
            };

            ProcessExits(openPositions, context, config, trades, log, ref dailyPnL, equityCurve);

            if (dailyPnL <= -config.MaxDailyLossUsd)
            {
                log.Add($"{bar.DateTime:HH:mm}: max daily loss reached, halting entries");
                runningDailyHigh = Math.Max(runningDailyHigh, bar.High);
                continue;
            }

            if (openPositions.Count < config.MaxConcurrentTrades
                && strategy.ShouldEnter(context, config))
            {
                var entryPrice = Math.Max(bar.Close, previousDailyHigh + 0.01m);
                var shares = (int)Math.Floor(config.PositionSizeUsd / entryPrice);
                if (shares > 0)
                {
                    tradeCounter++;
                    var stopLossPrice = entryPrice * (1m - config.StopLossPercent / 100m);
                    openPositions.Add(new OpenPosition
                    {
                        Id = $"trade-{tradeCounter}",
                        EntryTime = bar.DateTime,
                        EntryPrice = entryPrice,
                        OriginalShares = shares,
                        RemainingShares = shares,
                        StopLossPrice = stopLossPrice,
                        PendingTakeProfits = config.TakeProfitTargets.ToList()
                    });

                    log.Add($"{bar.DateTime:HH:mm}: ENTRY {shares} shares @ {entryPrice:F2} (broke daily high {previousDailyHigh:F2})");
                }
            }

            runningDailyHigh = Math.Max(runningDailyHigh, bar.High);
        }

        foreach (var position in openPositions.ToList())
        {
            var lastBar = orderedBars[^1];
            ClosePosition(
                position,
                lastBar.DateTime,
                lastBar.Close,
                position.RemainingShares,
                "session_end",
                trades,
                log,
                ref dailyPnL,
                equityCurve);
        }

        var totalPnL = trades.Sum(t => t.PnL);
        var winners = trades.Count(t => t.PnL > 0);
        var winRate = trades.Count == 0 ? 0m : (decimal)winners / trades.Count * 100m;
        var maxDrawdown = CalculateMaxDrawdown(equityCurve);

        return new BacktestResult
        {
            Ticker = ticker.ToUpperInvariant(),
            Date = date,
            StrategyName = config.Name,
            Trades = trades,
            TotalPnL = totalPnL,
            WinRate = winRate,
            MaxDrawdown = maxDrawdown,
            TradeLog = log
        };
    }

    private void ProcessExits(
        List<OpenPosition> openPositions,
        BarContext context,
        StrategyConfig config,
        List<TradeResult> trades,
        List<string> log,
        ref decimal dailyPnL,
        List<decimal> equityCurve)
    {
        foreach (var position in openPositions.ToList())
        {
            while (position.RemainingShares > 0)
            {
                var signal = strategy.EvaluateExit(position, context, config);
                if (signal is null)
                {
                    break;
                }

                ClosePosition(
                    position,
                    context.Bar.DateTime,
                    signal.ExitPrice,
                    signal.SharesToClose,
                    signal.Reason,
                    trades,
                    log,
                    ref dailyPnL,
                    equityCurve);

                if (signal.Reason.StartsWith("take_profit_", StringComparison.Ordinal))
                {
                    var level = int.Parse(signal.Reason["take_profit_".Length..], CultureInfo.InvariantCulture) - 1;
                    position.FilledTakeProfitLevels.Add(level);
                }
            }

            if (position.RemainingShares == 0)
            {
                openPositions.Remove(position);
            }
        }
    }

    private static void ClosePosition(
        OpenPosition position,
        DateTime exitTime,
        decimal exitPrice,
        int sharesToClose,
        string reason,
        List<TradeResult> trades,
        List<string> log,
        ref decimal dailyPnL,
        List<decimal> equityCurve)
    {
        if (sharesToClose <= 0)
        {
            return;
        }

        var pnl = (exitPrice - position.EntryPrice) * sharesToClose;
        dailyPnL += pnl;
        equityCurve.Add(equityCurve[^1] + pnl);

        trades.Add(new TradeResult
        {
            EntryTime = position.EntryTime,
            ExitTime = exitTime,
            EntryPrice = position.EntryPrice,
            ExitPrice = exitPrice,
            Quantity = sharesToClose,
            PnL = pnl,
            ExitReason = reason
        });

        position.RemainingShares -= sharesToClose;

        log.Add($"{exitTime:HH:mm}: EXIT {sharesToClose} shares @ {exitPrice:F2} ({reason}), P&L {pnl:F2}");
    }

    private static decimal CalculateMaxDrawdown(IReadOnlyList<decimal> equityCurve)
    {
        var peak = equityCurve[0];
        var maxDrawdown = 0m;

        foreach (var value in equityCurve)
        {
            peak = Math.Max(peak, value);
            var drawdown = peak - value;
            maxDrawdown = Math.Max(maxDrawdown, drawdown);
        }

        return maxDrawdown;
    }

    private static BacktestResult EmptyResult(string ticker, DateOnly date, string strategyName) => new()
    {
        Ticker = ticker.ToUpperInvariant(),
        Date = date,
        StrategyName = strategyName,
        Trades = [],
        TotalPnL = 0m,
        WinRate = 0m,
        MaxDrawdown = 0m,
        TradeLog = ["No market data available for the selected date."]
    };
}
