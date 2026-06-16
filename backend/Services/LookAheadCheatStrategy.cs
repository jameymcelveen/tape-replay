using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Deliberately dishonest strategy for look-ahead audit tests. Uses bar close on bar N to enter on bar N,
/// which a naive engine would reward; the honest engine must not expose close to entry decisions.
/// </summary>
public sealed class LookAheadCheatStrategy : IStrategy
{
    public string Name => "Look-Ahead Cheat (test only)";

    public bool ShouldEnter(EntryDecisionContext context, StrategyConfig config)
    {
        if (context.PriorBars.Count == 0)
        {
            return false;
        }

        var lastBar = context.PriorBars[^1];
        return lastBar.Close > lastBar.Open && lastBar.Close > context.RunningDailyHigh;
    }

    public ExitSignal? EvaluateExit(OpenPosition position, BarContext context, StrategyConfig config)
    {
        if (context.Bar.Low <= position.StopLossPrice)
        {
            return new ExitSignal
            {
                Reason = "stop_loss",
                SharesToClose = position.RemainingShares,
                ExitPrice = position.StopLossPrice
            };
        }

        return null;
    }
}
