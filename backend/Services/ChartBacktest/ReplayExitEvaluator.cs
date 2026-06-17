using TapeReplay.Api.Models.ChartBacktest;

namespace TapeReplay.Api.Services.ChartBacktest;

/// <summary>
/// Shared stop, target, and session-close exit logic for replay rules.
/// </summary>
public static class ReplayExitEvaluator
{
    /// <summary>
    /// Simulates exits from the entry bar forward through regular-session bars for the day.
    /// </summary>
    public static StrategyTradeResult CompleteTrade(
        DateTime entryTime,
        decimal entryPrice,
        int entryIndex,
        IReadOnlyList<EnrichedBar> dayBars,
        ReplayRuleParams parameters,
        int shares)
    {
        var stopPrice = entryPrice * (1m - parameters.StopPct / 100m);
        var targetPrice = entryPrice * (1m + parameters.TargetPct / 100m);

        for (var i = entryIndex; i < dayBars.Count; i++)
        {
            var bar = dayBars[i];
            if (bar.Session != MarketSession.Regular)
            {
                continue;
            }

            var stopHit = bar.Low <= stopPrice;
            var targetHit = bar.High >= targetPrice;

            if (stopHit && targetHit)
            {
                return BuildResult(entryTime, entryPrice, bar.UtcTime, stopPrice, "stop", shares);
            }

            if (stopHit)
            {
                return BuildResult(entryTime, entryPrice, bar.UtcTime, stopPrice, "stop", shares);
            }

            if (targetHit)
            {
                return BuildResult(entryTime, entryPrice, bar.UtcTime, targetPrice, "target", shares);
            }
        }

        var lastRegular = dayBars.LastOrDefault(b => b.Session == MarketSession.Regular);
        if (lastRegular is null)
        {
            return NotTaken("No regular-session bars after entry.");
        }

        return BuildResult(entryTime, entryPrice, lastRegular.UtcTime, lastRegular.Close, "close", shares);
    }

    public static StrategyTradeResult NotTaken(string reason) => new()
    {
        Taken = false,
        Reason = reason
    };

    private static StrategyTradeResult BuildResult(
        DateTime entryTime,
        decimal entryPrice,
        DateTime exitTime,
        decimal exitPrice,
        string exitReason,
        int shares)
    {
        var pnlPerShare = exitPrice - entryPrice;
        return new StrategyTradeResult
        {
            Taken = true,
            EntryTime = entryTime,
            EntryPrice = entryPrice,
            ExitTime = exitTime,
            ExitPrice = exitPrice,
            ExitReason = exitReason,
            PnlPerShare = pnlPerShare,
            Pnl = pnlPerShare * shares,
            Pct = entryPrice == 0 ? 0 : pnlPerShare / entryPrice * 100m
        };
    }
}
