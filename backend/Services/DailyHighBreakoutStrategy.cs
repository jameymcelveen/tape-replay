using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Ross Cameron style breakout strategy with opening-range and daily-high triggers.
/// </summary>
public sealed class DailyHighBreakoutStrategy : IStrategy
{
    public string Name => "Opening Range Breakout";

    public bool ShouldEnter(EntryDecisionContext context, StrategyConfig config)
    {
        if (!IsWithinEntryWindow(context.MarketTime, config))
        {
            return false;
        }

        if (context.DailyTradesCompleted >= config.MaxTradesPerDay)
        {
            return false;
        }

        if (config.NoReentryAfterStop && context.StoppedOutToday)
        {
            return false;
        }

        if (config.FirstBreakoutOnly && context.FirstBreakoutConsumed)
        {
            return false;
        }

        return config.EntryTrigger switch
        {
            EntryTriggerType.OpeningRangeHighBreak => ShouldEnterOpeningRangeBreak(context, config),
            EntryTriggerType.PriceBreaksAboveDailyHigh => context.BarOpen > context.RunningDailyHigh,
            _ => false
        };
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

    private static bool ShouldEnterOpeningRangeBreak(EntryDecisionContext context, StrategyConfig config)
    {
        if (!context.OpeningRangeComplete || context.OpeningRangeHigh is null)
        {
            return false;
        }

        return context.BarOpen > context.OpeningRangeHigh.Value;
    }

    private static bool IsWithinEntryWindow(TimeOnly marketTime, StrategyConfig config) =>
        marketTime >= config.EntryWindowStart && marketTime <= config.EntryWindowEnd;
}
