using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Queues and runs recording jobs from local config on startup.
/// </summary>
public sealed class RecordingStartupService(
    IOptions<RecordingJobOptions> recordingOptions,
    IOptions<DataDistributionOptions> distributionOptions,
    ICoverageRepository coverageRepository,
    MarketDataScraperService scraper,
    ILogger<RecordingStartupService> logger)
{
    public async Task RunConfiguredJobsAsync(CancellationToken cancellationToken = default)
    {
        var config = recordingOptions.Value;
        if (!config.RunOnStartup || config.Jobs.Count == 0)
        {
            return;
        }

        if (!distributionOptions.Value.IsScraperEnabled())
        {
            logger.LogInformation("Recording jobs skipped: scraper disabled for role {Role}.", distributionOptions.Value.Role);
            return;
        }

        var queued = 0;
        foreach (var job in config.Jobs)
        {
            if (job.Tickers.Count == 0 || job.DateTo < job.DateFrom)
            {
                logger.LogWarning("Skipping invalid recording job {Label}.", job.Label);
                continue;
            }

            logger.LogInformation(
                "Queueing job {Label}: {TickerCount} tickers {From} to {To}",
                job.Label,
                job.Tickers.Count,
                job.DateFrom,
                job.DateTo);

            foreach (var ticker in job.Tickers.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                for (var date = job.DateFrom; date <= job.DateTo; date = date.AddDays(1))
                {
                    if (!TradingCalendar.IsTradingDay(date))
                    {
                        await coverageRepository.MarkMinuteSkippedAsync(ticker, date, cancellationToken);
                        continue;
                    }

                    if (await coverageRepository.IsMinuteDoneAsync(ticker, date, cancellationToken))
                    {
                        continue;
                    }

                    await coverageRepository.EnsureMinutePendingAsync(ticker, date, cancellationToken);
                    queued++;
                }
            }
        }

        if (queued == 0)
        {
            logger.LogInformation("No new cells to record from configured jobs.");
            return;
        }

        logger.LogInformation("Recording {Queued} pending cells from configured jobs...", queued);
        var totalRecorded = 0;
        var rounds = 0;
        const int maxRounds = 500;

        while (rounds < maxRounds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recorded = await scraper.ScrapePendingAsync(20, cancellationToken);
            rounds++;
            totalRecorded += recorded;
            if (recorded == 0)
            {
                break;
            }
        }

        logger.LogInformation(
            "Configured recording jobs finished: {Recorded} cells recorded in {Rounds} scrape rounds.",
            totalRecorded,
            rounds);
    }
}
