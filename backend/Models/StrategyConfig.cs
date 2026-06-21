namespace TapeReplay.Api.Models;

/// <summary>
/// Executable strategy configuration produced from the DSL or UI.
/// </summary>
public sealed class StrategyConfig
{
    public string Name { get; set; } = "Opening Range Breakout";

    public EntryTriggerType EntryTrigger { get; set; } = EntryTriggerType.OpeningRangeHighBreak;

    /// <summary>Minutes after the regular open used to form the opening range high.</summary>
    public int OpeningRangeMinutes { get; set; } = 5;

    /// <summary>Earliest Eastern time to allow new entries.</summary>
    public TimeOnly EntryWindowStart { get; set; } = new(9, 35);

    /// <summary>Latest Eastern time to allow new entries.</summary>
    public TimeOnly EntryWindowEnd { get; set; } = new(10, 30);

    public decimal PositionSizeUsd { get; set; } = 1000m;

    public decimal StopLossPercent { get; set; } = 1.5m;

    public IReadOnlyList<TakeProfitTarget> TakeProfitTargets { get; set; } = [];

    public TimeOnly CloseAllAt { get; set; } = new(12, 0);

    public decimal MaxDailyLossUsd { get; set; } = 300m;

    public int MaxConcurrentTrades { get; set; } = 1;

    /// <summary>Maximum completed round-trips allowed per ticker per day.</summary>
    public int MaxTradesPerDay { get; set; } = 1;

    /// <summary>When true, no new entries after a stop-loss exit on the same day.</summary>
    public bool NoReentryAfterStop { get; set; } = true;

    /// <summary>When true, ignore pre/post market bars for entries and daily-high tracking.</summary>
    public bool RegularSessionOnly { get; set; } = true;

    /// <summary>When true, allow only the first qualifying breakout signal per day.</summary>
    public bool FirstBreakoutOnly { get; set; } = true;
}

/// <summary>
/// Supported entry trigger types for MVP strategies.
/// </summary>
public enum EntryTriggerType
{
    PriceBreaksAboveDailyHigh,
    OpeningRangeHighBreak
}

/// <summary>
/// A partial or full take-profit exit level.
/// </summary>
public sealed class TakeProfitTarget
{
    public decimal Percent { get; set; }

    public decimal Weight { get; set; }
}
