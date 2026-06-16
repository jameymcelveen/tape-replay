namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Tracks a content-addressed partition imported from the CDN.
/// </summary>
public sealed class DataPartitionImportEntity
{
    public long Id { get; set; }

    public PartitionKind Kind { get; set; }

    public required string PartitionKey { get; set; }

    public required string Sha256 { get; set; }

    public DateTime ImportedAt { get; set; }
}
