using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Services.ChartBacktest;

namespace TapeReplay.Api.Services.ChartBacktest.Rules;

/// <summary>
/// Premarket-high breakout: enter when RTH breaks above the premarket high.
/// </summary>
public sealed class PmhReplayStrategy : IReplayRuleStrategy
{
    public string Rule => "pmh";

    public StrategyTradeResult EvaluateDay(
        IReadOnlyList<EnrichedBar> dayBars,
        ReplayRuleParams parameters,
        int shares)
    {
        var premarket = dayBars.Where(b => b.Session == MarketSession.Premarket).ToList();
        if (premarket.Count == 0)
        {
            return ReplayExitEvaluator.NotTaken("No premarket bars.");
        }

        var level = premarket.Max(b => b.High);
        var regular = dayBars.Where(b => b.Session == MarketSession.Regular).ToList();
        if (regular.Count == 0)
        {
            return ReplayExitEvaluator.NotTaken("No regular-session bars.");
        }

        foreach (var bar in regular)
        {
            if (bar.High < level)
            {
                continue;
            }

            var dayIndex = dayBars.ToList().FindIndex(b => b.UtcTime == bar.UtcTime);
            return ReplayExitEvaluator.CompleteTrade(bar.UtcTime, level, dayIndex, dayBars, parameters, shares);
        }

        return ReplayExitEvaluator.NotTaken("Premarket high was not broken.");
    }
}
