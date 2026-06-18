using TapeReplay.Api.Models.ChartBacktest;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Cache for per-ticker-day strategy evaluation results.
/// </summary>
public interface IStrategyResultRepository
{
    Task<IReadOnlyDictionary<DateOnly, StrategyResultEntity>> GetCachedAsync(
        string ticker,
        DateOnly from,
        DateOnly to,
        string strategyConfigHash,
        CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<StrategyResultEntity> results, CancellationToken cancellationToken = default);
}
