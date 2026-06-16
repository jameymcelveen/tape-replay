namespace TapeReplay.Api.Models;

/// <summary>
/// A completed round-trip trade from the backtest engine.
/// </summary>
public sealed class TradeResult
{
    public DateTime EntryTime { get; init; }

    public DateTime ExitTime { get; init; }

    public decimal EntryPrice { get; init; }

    public decimal ExitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal PnL { get; init; }

    public string ExitReason { get; init; } = string.Empty;
}
