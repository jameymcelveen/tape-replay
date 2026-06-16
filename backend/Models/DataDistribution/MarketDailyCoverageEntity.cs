namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Whole-market daily bar coverage for a single trading day.
/// </summary>
public sealed class MarketDailyCoverageEntity
{
    public DateOnly Date { get; set; }

    public CoverageStatus Status { get; set; }

    public CoverageProvenance Provenance { get; set; }

    public DateTime UpdatedAt { get; set; }
}
