namespace TapeReplay.Api.Models;

/// <summary>
/// A single OHLCV market bar.
/// </summary>
public sealed class Candle
{
    public required string Ticker { get; init; }

    public DateTime DateTime { get; init; }

    public decimal Open { get; init; }

    public decimal High { get; init; }

    public decimal Low { get; init; }

    public decimal Close { get; init; }

    public long Volume { get; init; }
}
