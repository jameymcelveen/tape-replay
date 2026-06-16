namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Last published content hash per partition key on the publisher machine.
/// </summary>
public sealed class DataPublishLogEntity
{
    public long Id { get; set; }

    public PartitionKind Kind { get; set; }

    public required string PartitionKey { get; set; }

    public required string Sha256 { get; set; }

    public DateTime PublishedAt { get; set; }
}
