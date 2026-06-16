using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Ross Cameron style daily high breakout strategy with honest bar-open entry.
/// </summary>
public sealed class DailyHighBreakoutStrategy : IStrategy
{
    public string Name => "Daily High Breakout";

    public bool ShouldEnter(EntryDecisionContext context, StrategyConfig config)
    {
        if (config.EntryTrigger != EntryTriggerType.PriceBreaksAboveDailyHigh)
        {
            return false;
        }

        return context.BarOpen > context.RunningDailyHigh;
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

        for (var i = 0; i < position.PendingTakeProfits.Count; i++)
        {
            if (position.FilledTakeProfitLevels.Contains(i))
            {
                continue;
            }

            var target = position.PendingTakeProfits[i];
            var targetPrice = position.EntryPrice * (1m + target.Percent / 100m);
            if (context.Bar.High >= targetPrice)
            {
                var shares = (int)Math.Floor(position.OriginalShares * target.Weight);
                shares = Math.Min(shares, position.RemainingShares);
                if (shares > 0)
                {
                    return new ExitSignal
                    {
                        Reason = $"take_profit_{i + 1}",
                        SharesToClose = shares,
                        ExitPrice = targetPrice
                    };
                }
            }
        }

        if (context.MarketTime >= config.CloseAllAt && position.RemainingShares > 0)
        {
            return new ExitSignal
            {
                Reason = "close_all_at",
                SharesToClose = position.RemainingShares,
                ExitPrice = context.Bar.Close
            };
        }

        return null;
    }
}
