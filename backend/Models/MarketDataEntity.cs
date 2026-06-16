namespace TapeReplay.Api.Models;

/// <summary>
/// Persisted market data row for EF Core.
/// </summary>
public sealed class MarketDataEntity
{
    public long Id { get; set; }

    public required string Ticker { get; set; }

    public DateTime DateTime { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public long Volume { get; set; }
}
