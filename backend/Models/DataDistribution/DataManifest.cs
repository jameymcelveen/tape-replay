namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Coverage metadata embedded in a manifest partition entry.
/// </summary>
public sealed class PartitionCoverageInfo
{
    public IReadOnlyList<string> Tickers { get; init; } = [];

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }
}

/// <summary>
/// One content-addressed partition in the CDN manifest.
/// </summary>
public sealed class ManifestPartitionEntry
{
    public required string Kind { get; init; }

    public required string Key { get; init; }

    public required string Filename { get; init; }

    public required string Sha256 { get; init; }

    public long RowCount { get; init; }

    public long ByteSize { get; init; }

    public PartitionCoverageInfo? Covers { get; init; }
}

/// <summary>
/// Optional bootstrap archive for first-time subscriber seeding.
/// </summary>
public sealed class ManifestBootstrapInfo
{
    public required string Filename { get; init; }

    public required string Sha256 { get; init; }

    public long ByteSize { get; init; }
}

/// <summary>
/// Static JSON manifest hosted on the data CDN.
/// </summary>
public sealed class DataManifest
{
    public required string DatasetVersion { get; init; }

    public required string SchemaVersion { get; init; }

    public required DateTime GeneratedAt { get; init; }

    public IReadOnlyList<ManifestPartitionEntry> Partitions { get; init; } = [];

    public ManifestBootstrapInfo? Bootstrap { get; init; }

    /// <summary>Optional manifest signature hook for future publisher verification.</summary>
    public string? Signature { get; init; }
}
