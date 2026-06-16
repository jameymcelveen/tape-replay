namespace TapeReplay.Api.Models;

/// <summary>
/// Aggregated output from a single-day backtest run.
/// </summary>
public sealed class BacktestResult
{
    public required string Ticker { get; init; }

    public DateOnly Date { get; init; }

    public required string StrategyName { get; init; }

    public IReadOnlyList<TradeResult> Trades { get; init; } = [];

    public decimal TotalPnL { get; init; }

    public decimal WinRate { get; init; }

    public decimal MaxDrawdown { get; init; }

    public IReadOnlyList<string> TradeLog { get; init; } = [];
}
