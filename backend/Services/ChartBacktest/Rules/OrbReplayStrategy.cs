using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Services.ChartBacktest;

namespace TapeReplay.Api.Services.ChartBacktest.Rules;

/// <summary>
/// Opening-range breakout: enter when price breaks above the first N minutes of RTH.
/// </summary>
public sealed class OrbReplayStrategy : IReplayRuleStrategy
{
    public string Rule => "orb";

    public StrategyTradeResult EvaluateDay(
        IReadOnlyList<EnrichedBar> dayBars,
        ReplayRuleParams parameters,
        int shares)
    {
        var regular = dayBars.Where(b => b.Session == MarketSession.Regular).ToList();
        if (regular.Count == 0)
        {
            return ReplayExitEvaluator.NotTaken("No regular-session bars.");
        }

        var orMinutes = Math.Max(1, parameters.OrMinutes);
        if (regular.Count <= orMinutes)
        {
            return ReplayExitEvaluator.NotTaken("Not enough bars to form the opening range.");
        }

        var openingRange = regular.Take(orMinutes).ToList();
        var level = openingRange.Max(b => b.High);

        for (var i = orMinutes; i < regular.Count; i++)
        {
            var bar = regular[i];
            if (bar.High < level)
            {
                continue;
            }

            var dayIndex = dayBars.ToList().FindIndex(b => b.UtcTime == bar.UtcTime);
            return ReplayExitEvaluator.CompleteTrade(bar.UtcTime, level, dayIndex, dayBars, parameters, shares);
        }

        return ReplayExitEvaluator.NotTaken("Opening range level was not broken.");
    }
}
