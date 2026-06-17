using System.Globalization;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.ChartBacktest;

namespace TapeReplay.Api.Services.ChartBacktest;

/// <summary>
/// Loads stored minute bars and runs chart replay rules with a hindsight benchmark.
/// </summary>
public sealed class ChartBacktestService(
    MarketDataService marketDataService,
    IEnumerable<IReplayRuleStrategy> strategies)
{
    private readonly IReadOnlyDictionary<string, IReplayRuleStrategy> _strategies =
        strategies.ToDictionary(s => s.Rule, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runs a chart backtest for the requested ticker and UTC time range.
    /// </summary>
    public async Task<ChartBacktestResponse> RunAsync(
        ChartBacktestRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticker = request.Ticker.ToUpperInvariant();
        var includeExtended = string.Equals(request.Scope, "all", StringComparison.OrdinalIgnoreCase);

        var rawBars = await marketDataService.GetStoredMinuteBarsInRangeAsync(
            ticker,
            request.From,
            request.To,
            cancellationToken);

        var enriched = rawBars
            .Select(ToEnrichedBar)
            .Where(b => MarketSessionClassifier.IsInScope(b.Session, b.UtcTime, includeExtended))
            .ToList();

        var chartBars = enriched.Select(ToDto).ToList();

        var strategyBars = includeExtended
            ? enriched
            : enriched.Where(b => b.Session == MarketSession.Regular).ToList();

        var hindsight = PerfectHindsightCalculator.Compute(strategyBars);

        var rule = request.Rule.Trim().ToLowerInvariant();
        if (!_strategies.TryGetValue(rule, out var strategy))
        {
            throw new ArgumentException($"Unknown rule '{request.Rule}'. Supported: orb, pmh.");
        }

        var trade = EvaluateFirstTrade(strategy, enriched, request.Params, request.Shares);

        var capturePct = hindsight.ProfPerShare is > 0 && trade.PnlPerShare is not null
            ? trade.PnlPerShare / hindsight.ProfPerShare * 100m
            : (decimal?)null;

        return new ChartBacktestResponse
        {
            Bars = chartBars,
            Trade = trade,
            Hindsight = hindsight,
            Summary = new ChartBacktestSummary { CapturePct = capturePct }
        };
    }

    private static StrategyTradeResult EvaluateFirstTrade(
        IReplayRuleStrategy strategy,
        IReadOnlyList<EnrichedBar> bars,
        ReplayRuleParams parameters,
        int shares)
    {
        foreach (var dayGroup in bars.GroupBy(b => b.EasternDate).OrderBy(g => g.Key))
        {
            var dayBars = dayGroup.OrderBy(b => b.UtcTime).ToList();
            var result = strategy.EvaluateDay(dayBars, parameters, shares);
            if (result.Taken)
            {
                return result;
            }
        }

        var lastAttempt = bars
            .GroupBy(b => b.EasternDate)
            .OrderBy(g => g.Key)
            .Select(g => strategy.EvaluateDay(g.OrderBy(b => b.UtcTime).ToList(), parameters, shares))
            .LastOrDefault();

        return lastAttempt ?? ReplayExitEvaluator.NotTaken("No bars in range.");
    }

    private static EnrichedBar ToEnrichedBar(Candle candle)
    {
        var session = MarketSessionClassifier.Classify(candle.DateTime);
        return new EnrichedBar
        {
            UtcTime = candle.DateTime.Kind == DateTimeKind.Utc
                ? candle.DateTime
                : DateTime.SpecifyKind(candle.DateTime, DateTimeKind.Utc),
            Open = candle.Open,
            High = candle.High,
            Low = candle.Low,
            Close = candle.Close,
            Volume = candle.Volume,
            Session = session,
            EasternDate = MarketSessionClassifier.GetEasternDate(candle.DateTime)
        };
    }

    private static ChartBarDto ToDto(EnrichedBar bar)
    {
        var et = MarketSessionClassifier.ToEastern(bar.UtcTime);
        return new ChartBarDto
        {
            T = bar.UtcTime,
            EtTime = et.ToString("HH:mm", CultureInfo.InvariantCulture),
            O = bar.Open,
            H = bar.High,
            L = bar.Low,
            C = bar.Close,
            V = bar.Volume,
            Session = bar.Session.ToString().ToLowerInvariant()
        };
    }
}
