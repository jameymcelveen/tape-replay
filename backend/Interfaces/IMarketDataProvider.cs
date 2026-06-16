using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Abstraction for external market data providers (Polygon, future vendors).
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// Fetches one-minute OHLCV bars for a ticker on a trading day.
    /// </summary>
    Task<IReadOnlyList<Candle>> GetMinuteBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default);
}
