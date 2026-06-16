namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Persisted daily OHLCV bar for a single ticker.
/// </summary>
public sealed class MarketDailyEntity
{
    public long Id { get; set; }

    public required string Ticker { get; set; }

    public DateOnly Date { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public long Volume { get; set; }
}
