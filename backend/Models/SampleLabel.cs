namespace TapeReplay.Api.Models;

/// <summary>
/// Labels which sample a backtest result belongs to for honest reporting.
/// </summary>
public enum SampleLabel
{
    Exploratory,
    InSample,
    OutOfSample
}
