using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Controllers;

/// <summary>
/// Market data CDN publish, subscribe, scraper, and coverage grid endpoints.
/// </summary>
[ApiController]
[Route("api/data")]
public sealed class DataDistributionController(
    IOptions<DataDistributionOptions> options,
    DataPublisherService publisher,
    DataSubscriberService subscriber,
    MarketDataScraperService scraper,
    ICoverageRepository coverageRepository) : ControllerBase
{
    /// <summary>Returns the configured data distribution role and URLs.</summary>
    [HttpGet("config")]
    public ActionResult<object> GetConfig()
    {
        var config = options.Value;
        return Ok(new
        {
            role = config.Role.ToString(),
            manifestUrl = config.ManifestUrl,
            cdnBaseUrl = config.CdnBaseUrl,
            publishDirectory = config.PublishDirectory,
            scraperEnabled = config.IsScraperEnabled(),
            syncOnLaunch = config.SyncOnLaunch
        });
    }

    /// <summary>Publishes changed partitions to the local publish directory.</summary>
    [HttpPost("publish")]
    public async Task<ActionResult<PublishResult>> Publish(CancellationToken cancellationToken)
    {
        var result = await publisher.PublishAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Syncs partitions from the CDN manifest into SQLite.</summary>
    [HttpPost("sync")]
    public async Task<ActionResult<SyncResult>> Sync(CancellationToken cancellationToken)
    {
        var result = await subscriber.SyncAsync(cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>Queues pending minute coverage cells for one or more tickers over a date range.</summary>
    [HttpPost("queue-minute")]
    public async Task<ActionResult<object>> QueueMinute(
        [FromBody] QueueMinuteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Tickers.Count == 0)
        {
            return BadRequest(new { error = "At least one ticker is required." });
        }

        if (request.DateTo < request.DateFrom)
        {
            return BadRequest(new { error = "dateTo must be on or after dateFrom." });
        }

        var queued = 0;
        foreach (var ticker in request.Tickers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            for (var date = request.DateFrom; date <= request.DateTo; date = date.AddDays(1))
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

        return Ok(new { queued, tickers = request.Tickers.Count, dateFrom = request.DateFrom, dateTo = request.DateTo });
    }

    /// <summary>Runs scrape batches until no pending cells remain or maxRounds is hit.</summary>
    [HttpPost("record")]
    public async Task<ActionResult<object>> Record(
        [FromQuery] int batchSize = 20,
        [FromQuery] int maxRounds = 500,
        CancellationToken cancellationToken = default)
    {
        var totalRecorded = 0;
        var rounds = 0;

        while (rounds < maxRounds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recorded = await scraper.ScrapePendingAsync(batchSize, cancellationToken);
            rounds++;
            totalRecorded += recorded;

            if (recorded == 0)
            {
                break;
            }
        }

        return Ok(new { totalRecorded, rounds, completed = rounds < maxRounds || totalRecorded == 0 });
    }

    /// <summary>Runs the scraper for pending coverage cells.</summary>
    [HttpPost("scrape")]
    public async Task<ActionResult<object>> Scrape([FromQuery] int batchSize = 20, CancellationToken cancellationToken = default)
    {
        var recorded = await scraper.ScrapePendingAsync(batchSize, cancellationToken);
        return Ok(new { recorded });
    }

    /// <summary>Returns minute coverage cells for the status grid.</summary>
    [HttpGet("coverage/minute")]
    public async Task<ActionResult<IReadOnlyList<TickerMinuteCoverageEntity>>> GetMinuteCoverage(
        [FromQuery] string? ticker,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        var rows = await coverageRepository.GetMinuteCoverageAsync(ticker, startDate, endDate, cancellationToken);
        return Ok(rows);
    }

    /// <summary>Returns daily coverage cells for the status grid.</summary>
    [HttpGet("coverage/daily")]
    public async Task<ActionResult<IReadOnlyList<MarketDailyCoverageEntity>>> GetDailyCoverage(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        var rows = await coverageRepository.GetDailyCoverageAsync(startDate, endDate, cancellationToken);
        return Ok(rows);
    }
}
