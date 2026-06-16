namespace TapeReplay.Api.Models;

/// <summary>
/// Executable strategy configuration produced from the DSL or UI.
/// </summary>
public sealed class StrategyConfig
{
    public string Name { get; set; } = "Daily High Breakout";

    public EntryTriggerType EntryTrigger { get; set; } = EntryTriggerType.PriceBreaksAboveDailyHigh;

    public decimal PositionSizeUsd { get; set; } = 1000m;

    public decimal StopLossPercent { get; set; } = 1m;

    public IReadOnlyList<TakeProfitTarget> TakeProfitTargets { get; set; } = [];

    public TimeOnly CloseAllAt { get; set; } = new(14, 0);

    public decimal MaxDailyLossUsd { get; set; } = 500m;

    public int MaxConcurrentTrades { get; set; } = 3;
}

/// <summary>
/// Supported entry trigger types for MVP strategies.
/// </summary>
public enum EntryTriggerType
{
    PriceBreaksAboveDailyHigh
}

/// <summary>
/// A partial or full take-profit exit level.
/// </summary>
public sealed class TakeProfitTarget
{
    public decimal Percent { get; set; }

    public decimal Weight { get; set; }
}
