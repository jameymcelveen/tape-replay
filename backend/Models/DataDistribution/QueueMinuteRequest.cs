namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Request to queue minute-bar coverage cells for scraping.
/// </summary>
public sealed class QueueMinuteRequest
{
    public required IReadOnlyList<string> Tickers { get; init; }

    public required DateOnly DateFrom { get; init; }

    public required DateOnly DateTo { get; init; }
}
