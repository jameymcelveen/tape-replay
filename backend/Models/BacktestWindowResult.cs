namespace TapeReplay.Api.Models;

/// <summary>
/// Aggregated backtest across a date range with honest labeling.
/// </summary>
public sealed class BacktestWindowResult
{
    public required string Ticker { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public SampleLabel SampleLabel { get; init; }

    public required string StrategyName { get; init; }

    public IReadOnlyList<TradeResult> Trades { get; init; } = [];

    public IReadOnlyList<BacktestResult> DailyResults { get; init; } = [];

    public required HonestMetrics Metrics { get; init; }

    public IReadOnlyList<string> TradeLog { get; init; } = [];
}
