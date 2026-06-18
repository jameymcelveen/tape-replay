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

        var enriched = EnrichBars(rawBars, includeExtended);
        var chartBars = enriched.Select(ToDto).ToList();
        var evaluation = EvaluateEnrichedDay(enriched, request.Rule, request.Params, request.Shares, includeExtended);

        var strategyBars = includeExtended
            ? enriched
            : enriched.Where(b => b.Session == MarketSession.Regular).ToList();

        var hindsight = PerfectHindsightCalculator.Compute(strategyBars);
        var trade = BuildTradeResult(evaluation);
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

    /// <summary>
    /// Evaluates strategy performance for one Eastern session day from raw minute bars.
    /// </summary>
    public ChartDayEvaluation EvaluateDayBars(
        IReadOnlyList<Candle> rawBars,
        string rule,
        ReplayRuleParams parameters,
        int shares,
        string scope)
    {
        var includeExtended = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase);
        var enriched = EnrichBars(rawBars, includeExtended);

        if (enriched.Count == 0)
        {
            return new ChartDayEvaluation { HasData = false, Traded = false };
        }

        return EvaluateEnrichedDay(enriched, rule, parameters, shares, includeExtended);
    }

    private static List<EnrichedBar> EnrichBars(IReadOnlyList<Candle> rawBars, bool includeExtended)
    {
        return rawBars
            .Select(ToEnrichedBar)
            .Where(b => MarketSessionClassifier.IsInScope(b.Session, b.UtcTime, includeExtended))
            .ToList();
    }

    private ChartDayEvaluation EvaluateEnrichedDay(
        IReadOnlyList<EnrichedBar> enriched,
        string rule,
        ReplayRuleParams parameters,
        int shares,
        bool includeExtended)
    {
        if (enriched.Count == 0)
        {
            return new ChartDayEvaluation { HasData = false, Traded = false };
        }

        var ruleKey = rule.Trim().ToLowerInvariant();
        if (!_strategies.TryGetValue(ruleKey, out var strategy))
        {
            throw new ArgumentException($"Unknown rule '{rule}'. Supported: orb, pmh.");
        }

        var strategyBars = includeExtended
            ? enriched
            : enriched.Where(b => b.Session == MarketSession.Regular).ToList();

        var hindsight = PerfectHindsightCalculator.Compute(strategyBars);
        var trade = strategy.EvaluateDay(enriched.OrderBy(b => b.UtcTime).ToList(), parameters, shares);

        var capturePct = hindsight.ProfPerShare is > 0 && trade.PnlPerShare is not null
            ? trade.PnlPerShare / hindsight.ProfPerShare * 100m
            : (decimal?)null;

        return new ChartDayEvaluation
        {
            HasData = true,
            Traded = trade.Taken,
            PnlPct = trade.Pct,
            CapturePct = capturePct,
            PnlDollar = trade.Pnl,
            EntryTime = trade.EntryTime,
            EntryPrice = trade.EntryPrice,
            ExitTime = trade.ExitTime,
            ExitPrice = trade.ExitPrice,
            ExitReason = trade.ExitReason
        };
    }

    private static StrategyTradeResult BuildTradeResult(ChartDayEvaluation evaluation) => new()
    {
        Taken = evaluation.Traded,
        Pct = evaluation.PnlPct,
        Pnl = evaluation.PnlDollar,
        EntryTime = evaluation.EntryTime,
        EntryPrice = evaluation.EntryPrice,
        ExitTime = evaluation.ExitTime,
        ExitPrice = evaluation.ExitPrice,
        ExitReason = evaluation.ExitReason,
        PnlPerShare = evaluation.EntryPrice is not null && evaluation.ExitPrice is not null
            ? evaluation.ExitPrice - evaluation.EntryPrice
            : null
    };

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
