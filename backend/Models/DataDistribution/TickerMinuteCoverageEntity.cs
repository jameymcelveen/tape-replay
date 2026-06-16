namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Per-ticker per-day minute bar coverage cell.
/// </summary>
public sealed class TickerMinuteCoverageEntity
{
    public required string Ticker { get; set; }

    public DateOnly Date { get; set; }

    public CoverageStatus Status { get; set; }

    public CoverageProvenance Provenance { get; set; }

    public DateTime UpdatedAt { get; set; }
}
