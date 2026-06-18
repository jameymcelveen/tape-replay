using TapeReplay.Api.Models;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Repository for cached market data with partition export and upsert support.
/// </summary>
public interface IMarketDataRepository
{
    Task<bool> HasDataAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default);

    Task<bool> IsCoverageDoneAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Candle>> GetBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Candle>> GetBarsInRangeAsync(
        string ticker,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task SaveBarsAsync(string ticker, IReadOnlyList<Candle> bars, CancellationToken cancellationToken = default);

    Task UpsertMinuteBarsAsync(IReadOnlyList<Candle> bars, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Candle>> GetMinuteBarsForPartitionAsync(
        string ticker,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetMinutePartitionKeysAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(int Year, int Month)>> GetDailyPartitionKeysAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetDistinctTickersWithMinuteDataAsync(CancellationToken cancellationToken = default);
}
