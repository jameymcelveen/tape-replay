namespace TapeReplay.Api.Models;

/// <summary>
/// Request payload to run a backtest for one ticker and date.
/// </summary>
public sealed class BacktestRequest
{
    public required string Ticker { get; init; }

    public DateOnly Date { get; init; }

    public StrategyConfig? Strategy { get; init; }

    public string? Dsl { get; init; }
}
