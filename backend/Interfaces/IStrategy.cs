using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Pluggable strategy contract for evaluating entry and exit signals.
/// </summary>
public interface IStrategy
{
    string Name { get; }

    bool ShouldEnter(BarContext context, StrategyConfig config);

    ExitSignal? EvaluateExit(OpenPosition position, BarContext context, StrategyConfig config);
}

/// <summary>
/// Per-bar market context passed to strategy evaluation.
/// </summary>
public sealed class BarContext
{
    public required Candle Bar { get; init; }

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
