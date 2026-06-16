namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// In-memory daily OHLCV bar.
/// </summary>
public sealed class DailyBar
{
    public required string Ticker { get; init; }

    public DateOnly Date { get; init; }

    public decimal Open { get; init; }

    public decimal High { get; init; }

    public decimal Low { get; init; }

    public decimal Close { get; init; }

    public long Volume { get; init; }
}
