using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Tracks imported and published content-addressed partition hashes.
/// </summary>
public interface IDataPartitionStateRepository
{
    Task<string?> GetImportedHashAsync(PartitionKind kind, string partitionKey, CancellationToken cancellationToken = default);

    Task SetImportedHashAsync(
        PartitionKind kind,
        string partitionKey,
        string sha256,
        CancellationToken cancellationToken = default);

    Task<string?> GetPublishedHashAsync(PartitionKind kind, string partitionKey, CancellationToken cancellationToken = default);

    Task SetPublishedHashAsync(
        PartitionKind kind,
        string partitionKey,
        string sha256,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetAllImportedHashesAsync(
        PartitionKind kind,
        CancellationToken cancellationToken = default);
}
