using System.Formats.Tar;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Builds optional bootstrap tar archives for first-time subscriber seeding.
/// </summary>
public static class BootstrapArchiveBuilder
{
    public static async Task<ManifestBootstrapInfo?> BuildAsync(
        string publishDirectory,
        IReadOnlyList<ManifestPartitionEntry> partitions,
        CancellationToken cancellationToken = default)
    {
        if (partitions.Count == 0)
        {
            return null;
        }

        var bootstrapDir = Path.Combine(publishDirectory, "bootstrap");
        Directory.CreateDirectory(bootstrapDir);

        var tempTar = Path.Combine(bootstrapDir, $"bootstrap_{Guid.NewGuid():N}.tar");
        await using var tarStream = File.Create(tempTar);
        await using (var writer = new TarWriter(tarStream, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var partition in partitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourcePath = Path.Combine(publishDirectory, partition.Filename);
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                await using var entryStream = File.OpenRead(sourcePath);
                var entry = new PaxTarEntry(TarEntryType.RegularFile, partition.Filename)
                {
                    DataStream = entryStream
                };
                await writer.WriteEntryAsync(entry, cancellationToken);
            }
        }

        var sha256 = await ContentHash.ComputeFileSha256HexAsync(tempTar, cancellationToken);
        var finalName = $"bootstrap_{sha256}.tar";
        var finalPath = Path.Combine(bootstrapDir, finalName);
        File.Move(tempTar, finalPath, overwrite: true);

        var fileInfo = new FileInfo(finalPath);
        return new ManifestBootstrapInfo
        {
            Filename = $"bootstrap/{finalName}",
            Sha256 = sha256,
            ByteSize = fileInfo.Length
        };
    }
}
