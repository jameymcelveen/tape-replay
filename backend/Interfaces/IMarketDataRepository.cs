using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Repository for cached market data. Implementation is swappable (SQLite, PostgreSQL).
/// </summary>
public interface IMarketDataRepository
{
    Task<bool> HasDataAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Candle>> GetBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default);

    Task SaveBarsAsync(string ticker, IReadOnlyList<Candle> bars, CancellationToken cancellationToken = default);
}
