using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Coordinates cache-first market data retrieval.
/// </summary>
/// <remarks>
/// TODO: When multi-ticker backtests ship, include delisted tickers in the universe or results bias upward (survivorship).
/// </remarks>
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

    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<Candle>>> GetMinuteBarsForRangeAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<DateOnly, IReadOnlyList<Candle>>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var bars = await GetMinuteBarsAsync(ticker, date, cancellationToken);
            if (bars.Count > 0)
            {
                result[date] = bars;
            }
        }

        return result;
    }
}
