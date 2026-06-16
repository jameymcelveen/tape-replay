namespace TapeReplay.Api.Models;

/// <summary>
/// Request to freeze a strategy against an in-sample window.
/// </summary>
public sealed class BacktestCommitRequest
{
    public required string Ticker { get; init; }

    public DateOnly InSampleStart { get; init; }

    public DateOnly InSampleEnd { get; init; }

    public StrategyConfig? Strategy { get; init; }

    public string? Dsl { get; init; }

    public TradeCostConfig? Costs { get; init; }

    public decimal StartingCapitalUsd { get; init; } = 25_000m;
}

/// <summary>
/// Response after committing a frozen strategy.
/// </summary>
public sealed class BacktestCommitResponse
{
    public Guid CommitId { get; init; }

    public DateTime CommittedAt { get; init; }

    public required StrategyConfig FrozenStrategy { get; init; }

    public required BacktestWindowResult InSample { get; init; }
}

/// <summary>
/// Request to score a committed strategy on unseen dates.
/// </summary>
public sealed class BacktestEvaluateRequest
{
    public Guid CommitId { get; init; }

    public DateOnly OutOfSampleStart { get; init; }

    public DateOnly OutOfSampleEnd { get; init; }

    public TradeCostConfig? Costs { get; init; }

    public decimal StartingCapitalUsd { get; init; } = 25_000m;
}

/// <summary>
/// Out-of-sample evaluation with overfitting warning when in-sample looked too good.
/// </summary>
public sealed class BacktestEvaluateResponse
{
    public Guid CommitId { get; init; }

    public required BacktestWindowResult OutOfSample { get; init; }

    public required BacktestWindowResult InSample { get; init; }

    public OverfittingWarning? OverfittingWarning { get; init; }

    public required string Verdict { get; init; }
}

/// <summary>
/// Warning when in-sample performance dramatically exceeds out-of-sample.
/// </summary>
public sealed class OverfittingWarning
{
    public required string Message { get; init; }

    public decimal InSampleNetReturnPercent { get; init; }

    public decimal OutOfSampleNetReturnPercent { get; init; }

    public decimal ReturnGapPercent { get; init; }
}
