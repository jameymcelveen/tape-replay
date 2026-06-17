using TapeReplay.Api.Models.ChartBacktest;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Pluggable intraday replay rule (ORB, PMH, etc.) for chart backtests.
/// </summary>
public interface IReplayRuleStrategy
{
    string Rule { get; }

    StrategyTradeResult EvaluateDay(
        IReadOnlyList<EnrichedBar> dayBars,
        ReplayRuleParams parameters,
        int shares);
}
