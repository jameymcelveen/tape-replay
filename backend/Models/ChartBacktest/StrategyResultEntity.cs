namespace TapeReplay.Api.Models.ChartBacktest;

/// <summary>
/// Cached per-ticker-day strategy evaluation keyed by strategy configuration hash.
/// </summary>
public sealed class StrategyResultEntity
{
    public string Ticker { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public string StrategyConfigHash { get; set; } = string.Empty;

    public bool HasData { get; set; }

    public bool Traded { get; set; }

    public decimal? PnlPct { get; set; }

    public decimal? CapturePct { get; set; }

    public decimal? PnlDollar { get; set; }

    public DateTime? EntryTime { get; set; }

    public decimal? EntryPrice { get; set; }

    public DateTime? ExitTime { get; set; }

    public decimal? ExitPrice { get; set; }

    public string? ExitReason { get; set; }

    public DateTime ComputedAt { get; set; }
}
