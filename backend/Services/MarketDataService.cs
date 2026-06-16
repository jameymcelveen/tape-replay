using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Coordinates cache-first market data retrieval.
/// </summary>
public sealed class MarketDataService(
    IMarketDataRepository repository,
    IMarketDataProvider provider,
    ILogger<MarketDataService> logger)
{
    public async Task<IReadOnlyList<Candle>> GetMinuteBarsAsync(
        string ticker,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);

        var normalizedTicker = ticker.ToUpperInvariant();

        if (await repository.HasDataAsync(normalizedTicker, date, cancellationToken))
        {
            logger.LogInformation("Loading cached bars for {Ticker} on {Date}", normalizedTicker, date);
            return await repository.GetBarsAsync(normalizedTicker, date, cancellationToken);
        }

        logger.LogInformation("Fetching bars from provider for {Ticker} on {Date}", normalizedTicker, date);
        var bars = await provider.GetMinuteBarsAsync(normalizedTicker, date, cancellationToken);

        if (bars.Count > 0)
        {
            await repository.SaveBarsAsync(normalizedTicker, bars, cancellationToken);
        }

        return bars;
    }
}
