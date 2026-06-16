using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Whole-market daily OHLCV persistence.
/// </summary>
public interface IMarketDailyRepository
{
    Task<IReadOnlyList<DailyBar>> GetDailyBarsForPartitionAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task UpsertDailyBarsAsync(IReadOnlyList<DailyBar> bars, CancellationToken cancellationToken = default);

    Task UpsertDailyFromMinuteBarsAsync(IReadOnlyList<Models.Candle> minuteBars, CancellationToken cancellationToken = default);
}
