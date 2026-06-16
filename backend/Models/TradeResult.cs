namespace TapeReplay.Api.Models;

/// <summary>
/// A completed round-trip trade leg from the backtest engine.
/// </summary>
public sealed class TradeResult
{
    public DateTime EntryTime { get; init; }

    public DateTime ExitTime { get; init; }

    public decimal EntryPrice { get; init; }

    public decimal ExitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal GrossPnL { get; init; }

    public decimal TotalCosts { get; init; }

    public decimal NetPnL { get; init; }

    public string ExitReason { get; init; } = string.Empty;

    /// <summary>Backward-compatible alias for net P&amp;L.</summary>
    public decimal PnL => NetPnL;
}
