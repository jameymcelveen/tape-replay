using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Exports changed partitions to a local publish directory and writes manifest.json.
/// Upload to CDN is external (no credentials in app).
/// </summary>
public sealed class DataPublisherService(
    IOptions<DataDistributionOptions> options,
    IMarketDataRepository marketDataRepository,
    IMarketDailyRepository marketDailyRepository,
    IDataPartitionStateRepository partitionStateRepository,
    ILogger<DataPublisherService> logger)
{
    public async Task<PublishResult> PublishAsync(CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.CanPublish())
        {
            throw new InvalidOperationException($"Role {config.Role} cannot publish.");
        }

        var publishRoot = ResolvePublishDirectory(config.PublishDirectory);
        Directory.CreateDirectory(publishRoot);

        var exported = 0;
        var skipped = 0;
        var manifestPartitions = new List<ManifestPartitionEntry>();

        foreach (var key in await marketDataRepository.GetMinutePartitionKeysAsync(cancellationToken))
        {
            var (ticker, year, month) = PartitionKey.ParseMinute(key);
            var bars = await marketDataRepository.GetMinuteBarsForPartitionAsync(ticker, year, month, cancellationToken);
            if (bars.Count == 0)
            {
                continue;
            }

            var entry = await ExportPartitionAsync(
                publishRoot,
                PartitionKind.Minute,
                key,
                bars,
                async path => await ParquetMinutePartitionCodec.WriteAsync(path, bars, cancellationToken),
                bars.Count,
                BuildMinuteCoverage(ticker, year, month, bars),
                cancellationToken);

            if (entry.WasExported)
            {
                exported++;
            }
            else
            {
                skipped++;
            }

            manifestPartitions.Add(entry.Descriptor);
        }

        foreach (var (year, month) in await marketDataRepository.GetDailyPartitionKeysAsync(cancellationToken))
        {
            var key = PartitionKey.Daily(year, month);
            var bars = await marketDailyRepository.GetDailyBarsForPartitionAsync(year, month, cancellationToken);
            if (bars.Count == 0)
            {
                continue;
            }

            var entry = await ExportPartitionAsync(
                publishRoot,
                PartitionKind.Daily,
                key,
                bars,
                async path => await ParquetDailyPartitionCodec.WriteAsync(path, bars, cancellationToken),
                bars.Count,
                BuildDailyCoverage(bars),
                cancellationToken);

            if (entry.WasExported)
            {
                exported++;
            }
            else
            {
                skipped++;
            }

            manifestPartitions.Add(entry.Descriptor);
        }

        ManifestBootstrapInfo? bootstrap = null;
        if (config.IncludeBootstrapArchive && manifestPartitions.Count > 0)
        {
            bootstrap = await BootstrapArchiveBuilder.BuildAsync(publishRoot, manifestPartitions, cancellationToken);
        }

        var manifest = new DataManifest
        {
            DatasetVersion = config.DatasetVersion,
            SchemaVersion = config.SchemaVersion,
            GeneratedAt = DateTime.UtcNow,
            Partitions = manifestPartitions.OrderBy(p => p.Kind).ThenBy(p => p.Key).ToList(),
            Bootstrap = bootstrap
        };

        var manifestPath = Path.Combine(publishRoot, "manifest.json");
        await DataManifestSerializer.WriteToFileAsync(manifest, manifestPath, cancellationToken);

        var syncCommand = BuildSuggestedSyncCommand(publishRoot, config);
        logger.LogInformation(
            "Publish complete: {Exported} exported, {Skipped} unchanged. Directory: {Dir}",
            exported,
            skipped,
            publishRoot);

        return new PublishResult
        {
            PublishDirectory = publishRoot,
            PartitionsExported = exported,
            PartitionsSkipped = skipped,
            ManifestPath = manifestPath,
            SuggestedSyncCommand = syncCommand
        };
    }

    private async Task<(ManifestPartitionEntry Descriptor, bool WasExported)> ExportPartitionAsync<T>(
        string publishRoot,
        PartitionKind kind,
        string partitionKey,
        T _,
        Func<string, Task> writeParquet,
        long rowCount,
        PartitionCoverageInfo covers,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(publishRoot, $".tmp_{Guid.NewGuid():N}.parquet");
        await writeParquet(tempPath);
        var sha256 = await ContentHash.ComputeFileSha256HexAsync(tempPath, cancellationToken);
        var filename = $"{sha256}.parquet";
        var finalPath = Path.Combine(publishRoot, filename);

        var previousHash = await partitionStateRepository.GetPublishedHashAsync(kind, partitionKey, cancellationToken);
        var wasExported = !string.Equals(previousHash, sha256, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(finalPath);

        if (wasExported)
        {
            File.Move(tempPath, finalPath, overwrite: true);
            await partitionStateRepository.SetPublishedHashAsync(kind, partitionKey, sha256, cancellationToken);
        }
        else
        {
            File.Delete(tempPath);
        }

        var fileInfo = new FileInfo(finalPath);
        var descriptor = new ManifestPartitionEntry
        {
            Kind = PartitionKey.KindToManifestValue(kind),
            Key = partitionKey,
            Filename = filename,
            Sha256 = sha256,
            RowCount = rowCount,
            ByteSize = fileInfo.Length,
            Covers = covers
        };

        return (descriptor, wasExported);
    }

    private static PartitionCoverageInfo BuildMinuteCoverage(string ticker, int year, int month, IReadOnlyList<Candle> bars)
    {
        var dates = bars.Select(b => DateOnly.FromDateTime(b.DateTime)).Distinct().OrderBy(d => d).ToList();
        return new PartitionCoverageInfo
        {
            Tickers = [ticker],
            StartDate = dates.FirstOrDefault(),
            EndDate = dates.LastOrDefault()
        };
    }

    private static PartitionCoverageInfo BuildDailyCoverage(IReadOnlyList<DailyBar> bars)
    {
        var tickers = bars.Select(b => b.Ticker).Distinct(StringComparer.Ordinal).OrderBy(t => t).ToList();
        var dates = bars.Select(b => b.Date).Distinct().OrderBy(d => d).ToList();
        return new PartitionCoverageInfo
        {
            Tickers = tickers,
            StartDate = dates.FirstOrDefault(),
            EndDate = dates.LastOrDefault()
        };
    }

    private static string ResolvePublishDirectory(string configured)
    {
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), configured);
    }

    private static string BuildSuggestedSyncCommand(string publishRoot, DataDistributionOptions config)
    {
        if (!string.IsNullOrWhiteSpace(config.CdnBaseUrl) && config.CdnBaseUrl.Contains("surge.sh", StringComparison.OrdinalIgnoreCase))
        {
            var domain = config.CdnBaseUrl
                .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');
            return $"cd \"{publishRoot}\" && surge . {domain}";
        }

        return $"rsync -av \"{publishRoot}/\" your-cdn-host:/data/";
    }
}
