using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Imports verified parquet partitions into SQLite and updates coverage provenance.
/// </summary>
public sealed class PartitionImportService(
    IMarketDataRepository marketDataRepository,
    IMarketDailyRepository marketDailyRepository,
    ICoverageRepository coverageRepository,
    IDataPartitionStateRepository partitionStateRepository,
    ILogger<PartitionImportService> logger)
{
    public async Task ImportMinutePartitionAsync(
        string partitionKey,
        string parquetPath,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var (ticker, year, month) = PartitionKey.ParseMinute(partitionKey);
        var bars = await ParquetMinutePartitionCodec.ReadAsync(parquetPath, cancellationToken);
        if (bars.Count == 0)
        {
            logger.LogWarning("Minute partition {Key} contained no rows.", partitionKey);
            return;
        }

        await marketDataRepository.UpsertMinuteBarsAsync(bars, cancellationToken);
        await marketDailyRepository.UpsertDailyFromMinuteBarsAsync(bars, cancellationToken);

        var dates = bars
            .Select(b => DateOnly.FromDateTime(b.DateTime))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        foreach (var date in dates)
        {
            await coverageRepository.MarkMinuteDoneAsync(ticker, date, CoverageProvenance.Published, cancellationToken);
            await coverageRepository.MarkDailyDoneAsync(date, CoverageProvenance.Published, cancellationToken);
        }

        await partitionStateRepository.SetImportedHashAsync(PartitionKind.Minute, partitionKey, sha256, cancellationToken);
        logger.LogInformation("Imported minute partition {Key} ({Rows} rows).", partitionKey, bars.Count);
    }

    public async Task ImportDailyPartitionAsync(
        string partitionKey,
        string parquetPath,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var (year, month) = PartitionKey.ParseDaily(partitionKey);
        var bars = await ParquetDailyPartitionCodec.ReadAsync(parquetPath, cancellationToken);
        if (bars.Count == 0)
        {
            logger.LogWarning("Daily partition {Key} contained no rows.", partitionKey);
            return;
        }

        await marketDailyRepository.UpsertDailyBarsAsync(bars, cancellationToken);

        foreach (var date in bars.Select(b => b.Date).Distinct())
        {
            await coverageRepository.MarkDailyDoneAsync(date, CoverageProvenance.Published, cancellationToken);
        }

        foreach (var tickerGroup in bars.GroupBy(b => b.Ticker))
        {
            foreach (var date in tickerGroup.Select(b => b.Date).Distinct())
            {
                await coverageRepository.MarkMinuteDoneAsync(tickerGroup.Key, date, CoverageProvenance.Published, cancellationToken);
            }
        }

        await partitionStateRepository.SetImportedHashAsync(PartitionKind.Daily, partitionKey, sha256, cancellationToken);
        logger.LogInformation("Imported daily partition {Key} ({Rows} rows, {Year}-{Month}).", partitionKey, bars.Count, year, month);
    }
}
