namespace TapeReplay.Api.Models;

/// <summary>
/// Polygon.io API configuration including rate-limit guardrails.
/// </summary>
public sealed class PolygonOptions
{
    public const string SectionName = "Polygon";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Maximum API calls allowed per rolling 60-second window.</summary>
    public int MaxCallsPerMinute { get; set; } = 5;

    /// <summary>Seconds to wait after a 429 before the next request.</summary>
    public int BackoffSecondsOn429 { get; set; } = 60;
}
