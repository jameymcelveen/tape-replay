using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Coverage grid for minute and daily market data cells.
/// </summary>
public interface ICoverageRepository
{
    Task<bool> IsMinuteDoneAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default);

    Task<bool> IsDailyDoneAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task EnsureMinutePendingAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TickerMinuteCoverageEntity>> GetPendingMinuteCellsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task MarkMinuteDoneAsync(
        string ticker,
        DateOnly date,
        CoverageProvenance provenance,
        CancellationToken cancellationToken = default);

    Task MarkDailyDoneAsync(
        DateOnly date,
        CoverageProvenance provenance,
        CancellationToken cancellationToken = default);

    Task MarkMinuteRangeDoneFromBarsAsync(
        IReadOnlyList<string> tickers,
        DateOnly startDate,
        DateOnly endDate,
        CoverageProvenance provenance,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TickerMinuteCoverageEntity>> GetMinuteCoverageAsync(
        string? ticker,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarketDailyCoverageEntity>> GetDailyCoverageAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default);
}
