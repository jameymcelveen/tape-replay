using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Services;

/// <summary>
/// Coordinates cache-first market data retrieval with coverage grid and role-aware scraping.
/// </summary>
/// <remarks>
/// TODO: When multi-ticker backtests ship, include delisted tickers in the universe or results bias upward (survivorship).
/// </remarks>
public sealed class MarketDataService(
    IMarketDataRepository repository,
    IMarketDataProvider provider,
    ICoverageRepository coverageRepository,
    IOptions<DataDistributionOptions> distributionOptions,
    MarketDataScraperService scraper,
    DataSubscriberService subscriber,
    ILogger<MarketDataService> logger)
{
    public async Task<IReadOnlyList<Models.Candle>> GetMinuteBarsAsync(
        string ticker,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);

        var normalizedTicker = ticker.ToUpperInvariant();
        var config = distributionOptions.Value;

        if (await coverageRepository.IsMinuteDoneAsync(normalizedTicker, date, cancellationToken))
        {
            logger.LogInformation("Loading covered bars for {Ticker} on {Date}", normalizedTicker, date);
            return await repository.GetBarsAsync(normalizedTicker, date, cancellationToken);
        }

        if (await repository.HasDataAsync(normalizedTicker, date, cancellationToken))
        {
            logger.LogInformation("Loading cached bars for {Ticker} on {Date}", normalizedTicker, date);
            return await repository.GetBarsAsync(normalizedTicker, date, cancellationToken);
        }

        if (config.CanSubscribe() && !string.IsNullOrWhiteSpace(config.ManifestUrl))
        {
            logger.LogInformation("Attempting CDN sync before fetch for {Ticker} on {Date}", normalizedTicker, date);
            await subscriber.SyncAsync(allowBootstrap: false, cancellationToken);

            if (await coverageRepository.IsMinuteDoneAsync(normalizedTicker, date, cancellationToken)
                || await repository.HasDataAsync(normalizedTicker, date, cancellationToken))
            {
                return await repository.GetBarsAsync(normalizedTicker, date, cancellationToken);
            }
        }

        if (!config.IsScraperEnabled())
        {
            logger.LogWarning(
                "No data for {Ticker} on {Date} and scraper is disabled (role {Role}).",
                normalizedTicker,
                date,
                config.Role);
            return [];
        }

        await coverageRepository.EnsureMinutePendingAsync(normalizedTicker, date, cancellationToken);
        await scraper.ScrapePendingAsync(1, cancellationToken);

        if (await coverageRepository.IsMinuteDoneAsync(normalizedTicker, date, cancellationToken)
            || await repository.HasDataAsync(normalizedTicker, date, cancellationToken))
        {
            return await repository.GetBarsAsync(normalizedTicker, date, cancellationToken);
        }

        logger.LogInformation("Fetching bars from provider for {Ticker} on {Date}", normalizedTicker, date);
        var bars = await provider.GetMinuteBarsAsync(normalizedTicker, date, cancellationToken);

        if (bars.Count > 0)
        {
            await repository.SaveBarsAsync(normalizedTicker, bars, cancellationToken);
            await coverageRepository.MarkMinuteDoneAsync(normalizedTicker, date, CoverageProvenance.Api, cancellationToken);
        }

        return bars;
    }

    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<Models.Candle>>> GetMinuteBarsForRangeAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<DateOnly, IReadOnlyList<Models.Candle>>();

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
