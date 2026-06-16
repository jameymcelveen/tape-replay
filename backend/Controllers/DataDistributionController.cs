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
