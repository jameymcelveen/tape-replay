using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Pluggable strategy contract for evaluating entry and exit signals.
/// </summary>
public interface IStrategy
{
    string Name { get; }

    /// <summary>
    /// Uses only prior bars and the current bar open (bar-timing contract).
    /// </summary>
    bool ShouldEnter(EntryDecisionContext context, StrategyConfig config);

    /// <summary>
    /// Intrabar exits may use the current bar high/low; time exits use bar close at end of bar.
    /// </summary>
    ExitSignal? EvaluateExit(OpenPosition position, BarContext context, StrategyConfig config);
}

/// <summary>
/// Restricted context for entry decisions on bar N: prior bars and bar N open only.
/// </summary>
/// <remarks>
/// Bar-timing contract: no high, low, or close from bar N may influence entry signals.
/// </remarks>
public sealed class EntryDecisionContext
{
    public int BarIndex { get; init; }

    public IReadOnlyList<Candle> PriorBars { get; init; } = [];

    public decimal RunningDailyHigh { get; init; }

    public decimal BarOpen { get; init; }

    public TimeOnly MarketTime { get; init; }
}

public sealed class BarContext
{
    public int BarIndex { get; init; }

    public required Candle Bar { get; init; }

    public IReadOnlyList<Candle> PriorBars { get; init; } = [];

    public decimal RunningDailyHigh { get; init; }

    public TimeOnly MarketTime { get; init; }
}

/// <summary>
/// An open position tracked by the backtest engine.
/// </summary>
public sealed class OpenPosition
{
    public required string Id { get; init; }

    public DateTime EntryTime { get; init; }

    public decimal EntryPrice { get; init; }

    public decimal EntryReferencePrice { get; init; }

    public decimal EntryCostsTotal { get; init; }

    public int RemainingShares { get; set; }

    public int OriginalShares { get; init; }

    public decimal StopLossPrice { get; init; }

    public IReadOnlyList<TakeProfitTarget> PendingTakeProfits { get; set; } = [];

    public HashSet<int> FilledTakeProfitLevels { get; } = [];
}

/// <summary>
/// Exit instruction from a strategy evaluation.
/// </summary>
public sealed class ExitSignal
{
    public required string Reason { get; init; }

    public int SharesToClose { get; init; }

    public decimal ExitPrice { get; init; }
}
