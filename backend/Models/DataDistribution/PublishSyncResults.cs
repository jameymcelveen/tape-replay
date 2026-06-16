namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Result of a local publish operation.
/// </summary>
public sealed class PublishResult
{
    public required string PublishDirectory { get; init; }

    public int PartitionsExported { get; init; }

    public int PartitionsSkipped { get; init; }

    public required string ManifestPath { get; init; }

    public string? SuggestedSyncCommand { get; init; }
}

/// <summary>
/// Result of a subscriber sync from the CDN.
/// </summary>
public sealed class SyncResult
{
    public int PartitionsDownloaded { get; init; }

    public int PartitionsSkipped { get; init; }

    public int PartitionsFailed { get; init; }

    public bool UsedBootstrap { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}
