using System.Formats.Tar;
using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Fetches manifest.json and downloads only new or changed content-addressed partitions.
/// </summary>
public sealed class DataSubscriberService(
    IOptions<DataDistributionOptions> options,
    IDataPartitionStateRepository partitionStateRepository,
    PartitionImportService partitionImportService,
    IHttpClientFactory httpClientFactory,
    ILogger<DataSubscriberService> logger)
{
    private const int MaxDownloadAttempts = 2;

    public async Task<SyncResult> SyncAsync(bool allowBootstrap = true, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.CanSubscribe())
        {
            throw new InvalidOperationException($"Role {config.Role} cannot subscribe.");
        }

        if (string.IsNullOrWhiteSpace(config.ManifestUrl))
        {
            return new SyncResult { Errors = ["DataDistribution:ManifestUrl is not configured."] };
        }

        var client = httpClientFactory.CreateClient("DataCdn");
        DataManifest manifest;

        try
        {
            await using var manifestStream = await client.GetStreamAsync(config.ManifestUrl, cancellationToken);
            manifest = await DataManifestSerializer.ReadAsync(manifestStream, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch data manifest from {Url}", config.ManifestUrl);
            return new SyncResult { Errors = [$"Manifest fetch failed: {ex.Message}"] };
        }

        var cdnBase = (config.CdnBaseUrl ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(cdnBase))
        {
            cdnBase = new Uri(config.ManifestUrl).GetLeftPart(UriPartial.Authority);
        }

        var pending = await GetPendingPartitionsAsync(manifest, cancellationToken);
        var downloaded = 0;
        var skipped = manifest.Partitions.Count - pending.Count;
        var failed = 0;
        var errors = new List<string>();
        var usedBootstrap = false;

        if (allowBootstrap && pending.Count > 5 && manifest.Bootstrap is not null)
        {
            var bootstrapOk = await TryBootstrapImportAsync(manifest, cdnBase, client, cancellationToken);
            if (bootstrapOk)
            {
                usedBootstrap = true;
                pending = await GetPendingPartitionsAsync(manifest, cancellationToken);
            }
        }

        foreach (var partition in pending)
        {
            var success = await DownloadAndImportAsync(partition, cdnBase, client, cancellationToken);
            if (success)
            {
                downloaded++;
            }
            else
            {
                failed++;
                errors.Add($"Failed to import partition {partition.Key} ({partition.Filename}).");
            }
        }

        return new SyncResult
        {
            PartitionsDownloaded = downloaded,
            PartitionsSkipped = skipped,
            PartitionsFailed = failed,
            UsedBootstrap = usedBootstrap,
            Errors = errors
        };
    }

    private async Task<List<ManifestPartitionEntry>> GetPendingPartitionsAsync(
        DataManifest manifest,
        CancellationToken cancellationToken)
    {
        var pending = new List<ManifestPartitionEntry>();

        foreach (var partition in manifest.Partitions)
        {
            var kind = PartitionKey.KindFromManifestValue(partition.Kind);
            var imported = await partitionStateRepository.GetImportedHashAsync(kind, partition.Key, cancellationToken);
            if (string.Equals(imported, partition.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pending.Add(partition);
        }

        return pending;
    }

    private async Task<bool> TryBootstrapImportAsync(
        DataManifest manifest,
        string cdnBase,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var bootstrap = manifest.Bootstrap!;
        var tempDir = Path.Combine(Path.GetTempPath(), $"tapereplay-bootstrap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var archivePath = Path.Combine(tempDir, "bootstrap.tar");
            var url = $"{cdnBase}/{bootstrap.Filename.TrimStart('/')}";
            await DownloadFileWithRetryAsync(client, url, archivePath, bootstrap.Sha256, cancellationToken);

            var extractDir = Path.Combine(tempDir, "extract");
            Directory.CreateDirectory(extractDir);
            await using var archiveStream = File.OpenRead(archivePath);
            await using var reader = new TarReader(archiveStream);

            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.EntryType is not TarEntryType.RegularFile || entry.DataStream is null)
                {
                    continue;
                }

                var target = Path.Combine(extractDir, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var outStream = File.Create(target);
                await entry.DataStream.CopyToAsync(outStream, cancellationToken);
            }

            foreach (var partition in manifest.Partitions)
            {
                var localFile = Path.Combine(extractDir, partition.Filename);
                if (!File.Exists(localFile))
                {
                    continue;
                }

                await ImportPartitionFileAsync(partition, localFile, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bootstrap import failed; falling back to per-partition downloads.");
            return false;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private async Task<bool> DownloadAndImportAsync(
        ManifestPartitionEntry partition,
        string cdnBase,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tapereplay-partition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var localPath = Path.Combine(tempDir, partition.Filename);
            var url = $"{cdnBase}/{partition.Filename}";
            await DownloadFileWithRetryAsync(client, url, localPath, partition.Sha256, cancellationToken);
            await ImportPartitionFileAsync(partition, localPath, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Partition download/import failed for {Key}", partition.Key);
            return false;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private async Task DownloadFileWithRetryAsync(
        HttpClient client,
        string url,
        string localPath,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
        {
            try
            {
                await using var stream = await client.GetStreamAsync(url, cancellationToken);
                await using var file = File.Create(localPath);
                await stream.CopyToAsync(file, cancellationToken);
                await ContentHash.VerifyFileSha256HexAsync(localPath, expectedSha256, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }

                logger.LogWarning(ex, "Download attempt {Attempt} failed for {Url}", attempt, url);
            }
        }

        throw lastError ?? new InvalidOperationException($"Download failed for {url}");
    }

    private Task ImportPartitionFileAsync(
        ManifestPartitionEntry partition,
        string localPath,
        CancellationToken cancellationToken)
    {
        var kind = PartitionKey.KindFromManifestValue(partition.Kind);
        return kind switch
        {
            PartitionKind.Minute => partitionImportService.ImportMinutePartitionAsync(
                partition.Key,
                localPath,
                partition.Sha256,
                cancellationToken),
            PartitionKind.Daily => partitionImportService.ImportDailyPartitionAsync(
                partition.Key,
                localPath,
                partition.Sha256,
                cancellationToken),
            _ => throw new InvalidDataException($"Unknown partition kind: {partition.Kind}")
        };
    }
}
