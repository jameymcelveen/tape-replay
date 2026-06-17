namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Local recording job defined in appsettings.Development.local.json (not committed).
/// </summary>
public sealed class RecordingJobOptions
{
    public const string SectionName = "Recording";

    public bool RunOnStartup { get; set; } = true;

    public List<RecordingJobDefinition> Jobs { get; set; } = [];
}

/// <summary>
/// One targeted recording job: tickers over a date range.
/// </summary>
public sealed class RecordingJobDefinition
{
    public string Label { get; set; } = string.Empty;

    public List<string> Tickers { get; set; } = [];

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }
}
