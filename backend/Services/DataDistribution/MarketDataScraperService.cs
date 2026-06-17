using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Records pending coverage cells via the market data provider (Polygon or mock).
/// </summary>
public sealed class MarketDataScraperService(
    IOptions<DataDistributionOptions> options,
    ICoverageRepository coverageRepository,
    IMarketDataRepository marketDataRepository,
    IMarketDailyRepository marketDailyRepository,
    IMarketDataProvider provider,
    ILogger<MarketDataScraperService> logger)
{
    public async Task<int> ScrapePendingAsync(int batchSize = 20, CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsScraperEnabled())
        {
            logger.LogInformation("Scraper is disabled for role {Role}.", options.Value.Role);
            return 0;
        }

        var pending = await coverageRepository.GetPendingMinuteCellsAsync(batchSize, cancellationToken);
        var recorded = 0;

        foreach (var cell in pending)
        {
            if (await coverageRepository.IsMinuteDoneAsync(cell.Ticker, cell.Date, cancellationToken))
            {
                continue;
            }
            var bars = await provider.GetMinuteBarsAsync(cell.Ticker, cell.Date, cancellationToken);

            if (bars.Count == 0)
            {
                await coverageRepository.MarkMinuteSkippedAsync(cell.Ticker, cell.Date, cancellationToken);
                logger.LogInformation(
                    "Skipped {Ticker} {Date}: no bars returned from provider.",
                    cell.Ticker,
                    cell.Date);
                continue;
            }

            await marketDataRepository.UpsertMinuteBarsAsync(bars, cancellationToken);
            await marketDailyRepository.UpsertDailyFromMinuteBarsAsync(bars, cancellationToken);
            await coverageRepository.MarkMinuteDoneAsync(cell.Ticker, cell.Date, CoverageProvenance.Api, cancellationToken);
            await coverageRepository.MarkDailyDoneAsync(cell.Date, CoverageProvenance.Api, cancellationToken);
            recorded++;
        }

        return recorded;
    }
}
