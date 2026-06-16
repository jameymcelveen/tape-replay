namespace TapeReplay.Api.Models;

/// <summary>
/// Persisted frozen strategy commit for out-of-sample evaluation.
/// </summary>
public sealed class BacktestCommitEntity
{
    public Guid Id { get; set; }

    public required string Ticker { get; set; }

    public DateOnly InSampleStart { get; set; }

    public DateOnly InSampleEnd { get; set; }

    public required string StrategyJson { get; set; }

    public decimal InSampleNetReturnPercent { get; set; }

    public DateTime CommittedAt { get; set; }
}
