using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Data;

/// <summary>
/// Tracks imported and published partition content hashes.
/// </summary>
public sealed class DataPartitionStateRepository(AppDbContext dbContext) : IDataPartitionStateRepository
{
    public async Task<string?> GetImportedHashAsync(
        PartitionKind kind,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DataPartitionImports
            .AsNoTracking()
            .Where(p => p.Kind == kind && p.PartitionKey == partitionKey)
            .Select(p => p.Sha256)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetImportedHashAsync(
        PartitionKind kind,
        string partitionKey,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.DataPartitionImports
            .FirstOrDefaultAsync(p => p.Kind == kind && p.PartitionKey == partitionKey, cancellationToken);

        if (row is null)
        {
            dbContext.DataPartitionImports.Add(new DataPartitionImportEntity
            {
                Kind = kind,
                PartitionKey = partitionKey,
                Sha256 = sha256,
                ImportedAt = DateTime.UtcNow
            });
        }
        else
        {
            row.Sha256 = sha256;
            row.ImportedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetPublishedHashAsync(
        PartitionKind kind,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DataPublishLogs
            .AsNoTracking()
            .Where(p => p.Kind == kind && p.PartitionKey == partitionKey)
            .Select(p => p.Sha256)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetPublishedHashAsync(
        PartitionKind kind,
        string partitionKey,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.DataPublishLogs
            .FirstOrDefaultAsync(p => p.Kind == kind && p.PartitionKey == partitionKey, cancellationToken);

        if (row is null)
        {
            dbContext.DataPublishLogs.Add(new DataPublishLogEntity
            {
                Kind = kind,
                PartitionKey = partitionKey,
                Sha256 = sha256,
                PublishedAt = DateTime.UtcNow
            });
        }
        else
        {
            row.Sha256 = sha256;
            row.PublishedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllImportedHashesAsync(
        PartitionKind kind,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.DataPartitionImports
            .AsNoTracking()
            .Where(p => p.Kind == kind)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.PartitionKey, r => r.Sha256, StringComparer.Ordinal);
    }
}
