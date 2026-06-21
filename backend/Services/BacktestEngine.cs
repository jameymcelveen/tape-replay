using System.Globalization;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Services.ChartBacktest;

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
        var ideal = IdealTradeBenchmark.Compute(bars, config.RegularSessionOnly, config.PositionSizeUsd);
        var capture = IdealTradeBenchmark.ComputeCapturePercent(metrics.NetTotalPnL, ideal, config.PositionSizeUsd);

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
            Metrics = metrics,
            IdealTrade = ideal,
            IdealCapturePct = capture
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

            var dayResult = Run(ticker, date, config, bars, costs, sampleLabel, runningEquity);
            allTrades.AddRange(dayResult.Trades);
            allLogs.AddRange(dayResult.TradeLog.Select(line => $"{date:yyyy-MM-dd} {line}"));

            runningEquity += dayResult.Trades.Sum(t => t.NetPnL);
            equityCurve.Add(new EquityPoint { Date = date, Equity = runningEquity });
            dailyResults.Add(dayResult);
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
        var dailyTradesCompleted = 0;
        var stoppedOutToday = false;
        var firstBreakoutConsumed = false;
        decimal? openingRangeHigh = null;
        var regularSessionBarsInOr = 0;
        var openingRangeComplete = false;

        for (var index = 0; index < orderedBars.Count; index++)
        {
            var bar = orderedBars[index];
            var priorBars = orderedBars.Take(index).ToList();
            var session = MarketSessionClassifier.Classify(bar.DateTime);
            var marketTime = TimeOnly.FromDateTime(MarketSessionClassifier.ToEastern(bar.DateTime));
            var previousDailyHigh = ComputeRunningDailyHigh(priorBars, config.RegularSessionOnly, orderedBars[0].Open);

            var exitContext = new BarContext
            {
                BarIndex = index,
                Bar = bar,
                PriorBars = priorBars,
                RunningDailyHigh = previousDailyHigh,
                MarketTime = marketTime
            };

            ProcessExits(
                openPositions,
                exitContext,
                config,
                costs,
                trades,
                log,
                ref dailyNetPnL,
                ref runningEquity,
                equityCurve,
                ref dailyTradesCompleted,
                ref stoppedOutToday);

            if (session == MarketSession.Regular)
            {
                if (!openingRangeComplete)
                {
                    openingRangeHigh = openingRangeHigh.HasValue
                        ? Math.Max(openingRangeHigh.Value, bar.High)
                        : bar.High;
                    regularSessionBarsInOr++;
                    if (regularSessionBarsInOr >= config.OpeningRangeMinutes)
                    {
                        openingRangeComplete = true;
                    }
                }

                runningDailyHigh = Math.Max(runningDailyHigh, bar.High);
            }

            if (dailyNetPnL <= -config.MaxDailyLossUsd)
            {
                log.Add($"{marketTime:HH:mm} ET: max daily loss reached, halting entries");
                continue;
            }

            var canEnterSession = !config.RegularSessionOnly || session == MarketSession.Regular;
            var canEnterTime = marketTime < config.CloseAllAt;

            if (!canEnterSession || !canEnterTime)
            {
                continue;
            }

            var entryContext = new EntryDecisionContext
            {
                BarIndex = index,
                PriorBars = priorBars,
                RunningDailyHigh = previousDailyHigh,
                BarOpen = bar.Open,
                MarketTime = marketTime,
                DailyTradesCompleted = dailyTradesCompleted,
                StoppedOutToday = stoppedOutToday,
                FirstBreakoutConsumed = firstBreakoutConsumed,
                OpeningRangeHigh = openingRangeHigh,
                OpeningRangeComplete = openingRangeComplete
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

                    if (config.FirstBreakoutOnly)
                    {
                        firstBreakoutConsumed = true;
                    }

                    log.Add($"{marketTime:HH:mm} ET: ENTRY {shares} shares @ {entryFill.FillPrice:F2} (ref {entryReference:F2}, costs {entryFill.TotalCost:F2})");
                }
            }
        }

        foreach (var position in openPositions.ToList())
        {
            var lastBar = orderedBars[^1];
            var marketTime = TimeOnly.FromDateTime(MarketSessionClassifier.ToEastern(lastBar.DateTime));
            var exitContext = new BarContext
            {
                BarIndex = orderedBars.Count - 1,
                Bar = lastBar,
                PriorBars = orderedBars.Take(orderedBars.Count - 1).ToList(),
                RunningDailyHigh = ComputeRunningDailyHigh(
                    orderedBars.Take(orderedBars.Count - 1).ToList(),
                    config.RegularSessionOnly,
                    orderedBars[0].Open),
                MarketTime = marketTime
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

            if (position.RemainingShares == 0)
            {
                dailyTradesCompleted++;
            }
        }

        return (trades, log, equityCurve);
    }

    private static decimal ComputeRunningDailyHigh(
        IReadOnlyList<Candle> priorBars,
        bool regularSessionOnly,
        decimal seedOpen)
    {
        if (priorBars.Count == 0)
        {
            return seedOpen;
        }

        var highs = priorBars
            .Where(bar => !regularSessionOnly || MarketSessionClassifier.Classify(bar.DateTime) == MarketSession.Regular)
            .Select(bar => bar.High);

        return highs.Any() ? highs.Max() : seedOpen;
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
        List<EquityPoint> equityCurve,
        ref int dailyTradesCompleted,
        ref bool stoppedOutToday)
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

                if (signal.Reason == "stop_loss")
                {
                    stoppedOutToday = true;
                }
            }

            if (position.RemainingShares == 0)
            {
                openPositions.Remove(position);
                dailyTradesCompleted++;
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

        var exitEt = TimeOnly.FromDateTime(MarketSessionClassifier.ToEastern(exitTime));
        log.Add($"{exitEt:HH:mm} ET: EXIT {sharesToClose} shares @ {exitFill.FillPrice:F2} ({reason}), gross {grossPnL:F2}, net {netPnL:F2}, costs {totalCosts:F2}");
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

