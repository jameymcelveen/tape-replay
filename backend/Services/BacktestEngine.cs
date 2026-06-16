using System.Globalization;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Minute-by-minute backtest state machine with bar-timing contract and mandatory costs.
/// </summary>
/// <remarks>
/// Bar-timing contract: entry on bar N uses EntryDecisionContext (prior bars + bar N open only).
/// Entry fills at pessimistic ask via <see cref="ITradeCostModel"/>. Exits use intrabar high/low
/// for stop/target simulation; time exits use bar close at end of bar.
/// </remarks>
public sealed class BacktestEngine(
    IStrategy strategy,
    ITradeCostModel costModel,
    IHonestMetricsCalculator metricsCalculator) : IBacktestEngine
{
    public BacktestResult Run(
        string ticker,
        DateOnly date,
        StrategyConfig config,
        IReadOnlyList<Candle> bars,
        TradeCostConfig costs,
        SampleLabel sampleLabel = SampleLabel.Exploratory,
        decimal startingCapitalUsd = 25_000m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(costs);

        if (bars.Count == 0)
        {
            return EmptyResult(ticker, date, config.Name, sampleLabel, startingCapitalUsd);
        }

        var (trades, log, equityCurve) = ReplayDay(bars, config, costs, startingCapitalUsd);
        var metrics = metricsCalculator.Compute(trades, equityCurve, startingCapitalUsd, sampleLabel);

        return new BacktestResult
        {
            Ticker = ticker.ToUpperInvariant(),
            Date = date,
            SampleLabel = sampleLabel,
            StrategyName = config.Name,
            Trades = trades,
            GrossTotalPnL = metrics.GrossTotalPnL,
            NetTotalPnL = metrics.NetTotalPnL,
            TotalCosts = metrics.TotalCosts,
            WinRate = metrics.WinRate,
            MaxDrawdown = metrics.MaxDrawdownAbsolute,
            TradeLog = log,
            Metrics = metrics
        };
    }

    public BacktestWindowResult RunWindow(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        StrategyConfig config,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<Candle>> barsByDate,
        TradeCostConfig costs,
        SampleLabel sampleLabel,
        decimal startingCapitalUsd = 25_000m)
    {
        var allTrades = new List<TradeResult>();
        var allLogs = new List<string>();
        var dailyResults = new List<BacktestResult>();
        var equityCurve = new List<EquityPoint>();
        var runningEquity = startingCapitalUsd;

        equityCurve.Add(new EquityPoint { Date = startDate, Equity = runningEquity });

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (!barsByDate.TryGetValue(date, out var bars) || bars.Count == 0)
            {
                continue;
            }

            var (dayTrades, dayLog, _) = ReplayDay(bars, config, costs, runningEquity);
            allTrades.AddRange(dayTrades);
            allLogs.AddRange(dayLog.Select(line => $"{date:yyyy-MM-dd} {line}"));

            runningEquity += dayTrades.Sum(t => t.NetPnL);
            equityCurve.Add(new EquityPoint { Date = date, Equity = runningEquity });

            var dayMetrics = metricsCalculator.Compute(dayTrades, [
                new EquityPoint { Date = date, Equity = runningEquity }
            ], startingCapitalUsd, sampleLabel);

            dailyResults.Add(new BacktestResult
            {
                Ticker = ticker.ToUpperInvariant(),
                Date = date,
                SampleLabel = sampleLabel,
                StrategyName = config.Name,
                Trades = dayTrades,
                GrossTotalPnL = dayMetrics.GrossTotalPnL,
                NetTotalPnL = dayMetrics.NetTotalPnL,
                TotalCosts = dayMetrics.TotalCosts,
                WinRate = dayMetrics.WinRate,
                MaxDrawdown = dayMetrics.MaxDrawdownAbsolute,
                TradeLog = dayLog,
                Metrics = dayMetrics
            });
        }

        var windowMetrics = metricsCalculator.Compute(allTrades, equityCurve, startingCapitalUsd, sampleLabel);

        return new BacktestWindowResult
        {
            Ticker = ticker.ToUpperInvariant(),
            StartDate = startDate,
            EndDate = endDate,
            SampleLabel = sampleLabel,
            StrategyName = config.Name,
            Trades = allTrades,
            DailyResults = dailyResults,
            Metrics = windowMetrics,
            TradeLog = allLogs
        };
    }

    private (List<TradeResult> Trades, List<string> Log, List<EquityPoint> EquityCurve) ReplayDay(
        IReadOnlyList<Candle> bars,
        StrategyConfig config,
        TradeCostConfig costs,
        decimal startingCapitalUsd)
    {
        var orderedBars = bars.OrderBy(b => b.DateTime).ToList();
        var trades = new List<TradeResult>();
        var log = new List<string>();
        var openPositions = new List<OpenPosition>();
        var dailyNetPnL = 0m;
        var equityCurve = new List<EquityPoint>
        {
            new() { Date = DateOnly.FromDateTime(orderedBars[0].DateTime), Equity = startingCapitalUsd }
        };
        var runningEquity = startingCapitalUsd;
        var runningDailyHigh = orderedBars[0].Open;
        var tradeCounter = 0;

        for (var index = 0; index < orderedBars.Count; index++)
        {
            var bar = orderedBars[index];
            var priorBars = orderedBars.Take(index).ToList();
            var marketTime = TimeOnly.FromDateTime(bar.DateTime);
            var previousDailyHigh = index == 0
                ? orderedBars[0].Open
                : priorBars.Max(b => b.High);

            var exitContext = new BarContext
            {
                BarIndex = index,
                Bar = bar,
                PriorBars = priorBars,
                RunningDailyHigh = previousDailyHigh,
                MarketTime = marketTime
            };

            ProcessExits(openPositions, exitContext, config, costs, trades, log, ref dailyNetPnL, ref runningEquity, equityCurve);

            if (dailyNetPnL <= -config.MaxDailyLossUsd)
            {
                log.Add($"{bar.DateTime:HH:mm}: max daily loss reached, halting entries");
                runningDailyHigh = Math.Max(runningDailyHigh, bar.High);
                continue;
            }

            var entryContext = new EntryDecisionContext
            {
                BarIndex = index,
                PriorBars = priorBars,
                RunningDailyHigh = previousDailyHigh,
                BarOpen = bar.Open,
                MarketTime = marketTime
            };

            if (openPositions.Count < config.MaxConcurrentTrades
                && strategy.ShouldEnter(entryContext, config))
            {
                var entryReference = bar.Open;
                var shares = (int)Math.Floor(config.PositionSizeUsd / entryReference);
                if (shares > 0)
                {
                    var entryFill = costModel.CalculateEntry(entryReference, shares, costs);
                    tradeCounter++;
                    var stopLossPrice = entryFill.FillPrice * (1m - config.StopLossPercent / 100m);
                    openPositions.Add(new OpenPosition
                    {
                        Id = $"trade-{tradeCounter}",
                        EntryTime = bar.DateTime,
                        EntryPrice = entryFill.FillPrice,
                        EntryReferencePrice = entryReference,
                        EntryCostsTotal = entryFill.TotalCost,
                        OriginalShares = shares,
                        RemainingShares = shares,
                        StopLossPrice = stopLossPrice,
                        PendingTakeProfits = config.TakeProfitTargets.ToList()
                    });

                    log.Add($"{bar.DateTime:HH:mm}: ENTRY {shares} shares @ {entryFill.FillPrice:F2} (ref {entryReference:F2}, costs {entryFill.TotalCost:F2})");
                }
            }

            runningDailyHigh = Math.Max(runningDailyHigh, bar.High);
        }

        foreach (var position in openPositions.ToList())
        {
            var lastBar = orderedBars[^1];
            var exitContext = new BarContext
            {
                BarIndex = orderedBars.Count - 1,
                Bar = lastBar,
                PriorBars = orderedBars.Take(orderedBars.Count - 1).ToList(),
                RunningDailyHigh = orderedBars.Take(orderedBars.Count - 1).DefaultIfEmpty(lastBar).Max(b => b.High),
                MarketTime = TimeOnly.FromDateTime(lastBar.DateTime)
            };

            ClosePosition(
                position,
                lastBar.DateTime,
                lastBar.Close,
                position.RemainingShares,
                "session_end",
                costs,
                trades,
                log,
                ref dailyNetPnL,
                ref runningEquity,
                equityCurve);
        }

        return (trades, log, equityCurve);
    }

    private void ProcessExits(
        List<OpenPosition> openPositions,
        BarContext context,
        StrategyConfig config,
        TradeCostConfig costs,
        List<TradeResult> trades,
        List<string> log,
        ref decimal dailyNetPnL,
        ref decimal runningEquity,
        List<EquityPoint> equityCurve)
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
                    costs,
                    trades,
                    log,
                    ref dailyNetPnL,
                    ref runningEquity,
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

    private void ClosePosition(
        OpenPosition position,
        DateTime exitTime,
        decimal exitReferencePrice,
        int sharesToClose,
        string reason,
        TradeCostConfig costs,
        List<TradeResult> trades,
        List<string> log,
        ref decimal dailyNetPnL,
        ref decimal runningEquity,
        List<EquityPoint> equityCurve)
    {
        if (sharesToClose <= 0)
        {
            return;
        }

        var exitFill = costModel.CalculateExit(exitReferencePrice, sharesToClose, costs);
        var entryCostShare = position.OriginalShares == 0
            ? 0m
            : position.EntryCostsTotal * sharesToClose / position.OriginalShares;
        var totalCosts = entryCostShare + exitFill.TotalCost;
        var grossPnL = (exitReferencePrice - position.EntryReferencePrice) * sharesToClose;
        var netPnL = grossPnL - totalCosts;

        dailyNetPnL += netPnL;
        runningEquity += netPnL;
        equityCurve.Add(new EquityPoint { Date = DateOnly.FromDateTime(exitTime), Equity = runningEquity });

        trades.Add(new TradeResult
        {
            EntryTime = position.EntryTime,
            ExitTime = exitTime,
            EntryPrice = position.EntryPrice,
            ExitPrice = exitFill.FillPrice,
            Quantity = sharesToClose,
            GrossPnL = grossPnL,
            TotalCosts = totalCosts,
            NetPnL = netPnL,
            ExitReason = reason
        });

        position.RemainingShares -= sharesToClose;

        log.Add($"{exitTime:HH:mm}: EXIT {sharesToClose} shares @ {exitFill.FillPrice:F2} ({reason}), gross {grossPnL:F2}, net {netPnL:F2}, costs {totalCosts:F2}");
    }

    private BacktestResult EmptyResult(
        string ticker,
        DateOnly date,
        string strategyName,
        SampleLabel sampleLabel,
        decimal startingCapitalUsd)
    {
        var metrics = metricsCalculator.Compute([], [], startingCapitalUsd, sampleLabel);
        return new BacktestResult
        {
            Ticker = ticker.ToUpperInvariant(),
            Date = date,
            SampleLabel = sampleLabel,
            StrategyName = strategyName,
            Trades = [],
            GrossTotalPnL = 0m,
            NetTotalPnL = 0m,
            TotalCosts = 0m,
            WinRate = 0m,
            MaxDrawdown = 0m,
            TradeLog = ["No market data available for the selected date."],
            Metrics = metrics
        };
    }
}
