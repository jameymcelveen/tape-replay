namespace TapeReplay.Api.Models.ChartBacktest;

/// <summary>
/// Request to compute strategy performance cells for a ticker-day grid.
/// </summary>
public sealed class StrategyHeatmapRequest
{
    /// <summary>Explicit ticker list, or the sentinel values watchlist / all-with-data.</summary>
    public object? Tickers { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public StrategyHeatmapStrategyConfig Strategy { get; set; } = new();

    /// <summary>Display hint only; all metrics are returned and cached.</summary>
    public string Metric { get; set; } = "pnlPct";
}

/// <summary>
/// Strategy configuration shared by the heatmap and chart backtest views.
/// </summary>
public sealed class StrategyHeatmapStrategyConfig
{
    public string Rule { get; set; } = "orb";

    public ReplayRuleParams Params { get; set; } = new();

    public string Scope { get; set; } = "regular";

    public int Shares { get; set; } = 100;
}

/// <summary>
/// Heatmap grid response with one row per ticker.
/// </summary>
public sealed class StrategyHeatmapResponse
{
    public string StrategyConfigHash { get; set; } = string.Empty;

    public IReadOnlyList<DateOnly> TradingDays { get; set; } = [];

    public IReadOnlyList<StrategyHeatmapRow> Rows { get; set; } = [];
}

/// <summary>
/// One ticker row in the heatmap grid.
/// </summary>
public sealed class StrategyHeatmapRow
{
    public string Ticker { get; set; } = string.Empty;

    public IReadOnlyList<StrategyHeatmapDayCell> Days { get; set; } = [];
}

/// <summary>
/// Strategy result for a single ticker and Eastern trading day.
/// </summary>
public sealed class StrategyHeatmapDayCell
{
    public DateOnly Date { get; set; }

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
}

/// <summary>
/// Internal evaluation result for one Eastern session day.
/// </summary>
public sealed class ChartDayEvaluation
{
    public bool HasData { get; init; }

    public bool Traded { get; init; }

    public decimal? PnlPct { get; init; }

    public decimal? CapturePct { get; init; }

    public decimal? PnlDollar { get; init; }

    public DateTime? EntryTime { get; init; }

    public decimal? EntryPrice { get; init; }

    public DateTime? ExitTime { get; init; }

    public decimal? ExitPrice { get; init; }

    public string? ExitReason { get; init; }
}
