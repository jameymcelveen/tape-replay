namespace TapeReplay.Api.Models.ChartBacktest;

/// <summary>
/// Request to run a chart backtest over stored minute bars.
/// </summary>
public sealed class ChartBacktestRequest
{
    public string Ticker { get; set; } = string.Empty;

    public DateTime From { get; set; }

    public DateTime To { get; set; }

    /// <summary>regular = RTH only; all = include pre and post.</summary>
    public string Scope { get; set; } = "regular";

    /// <summary>orb or pmh.</summary>
    public string Rule { get; set; } = "orb";

    public ReplayRuleParams Params { get; set; } = new();

    public int Shares { get; set; } = 100;
}

/// <summary>
/// Strategy parameters shared by ORB and PMH rules.
/// </summary>
public sealed class ReplayRuleParams
{
    public int OrMinutes { get; set; } = 5;

    public decimal StopPct { get; set; } = 5;

    public decimal TargetPct { get; set; } = 10;
}

/// <summary>
/// Full chart backtest response including bars, trade, hindsight, and summary.
/// </summary>
public sealed class ChartBacktestResponse
{
    public IReadOnlyList<ChartBarDto> Bars { get; set; } = [];

    public StrategyTradeResult Trade { get; set; } = new();

    public HindsightResult Hindsight { get; set; } = new();

    public ChartBacktestSummary Summary { get; set; } = new();
}

/// <summary>
/// One minute bar for chart rendering.
/// </summary>
public sealed class ChartBarDto
{
    public DateTime T { get; set; }

    public string EtTime { get; set; } = string.Empty;

    public decimal O { get; set; }

    public decimal H { get; set; }

    public decimal L { get; set; }

    public decimal C { get; set; }

    public long V { get; set; }

    public string Session { get; set; } = string.Empty;
}

/// <summary>
/// Strategy trade outcome for the selected rule.
/// </summary>
public sealed class StrategyTradeResult
{
    public bool Taken { get; set; }

    public string? Reason { get; set; }

    public DateTime? EntryTime { get; set; }

    public decimal? EntryPrice { get; set; }

    public DateTime? ExitTime { get; set; }

    public decimal? ExitPrice { get; set; }

    public string? ExitReason { get; set; }

    public decimal? PnlPerShare { get; set; }

    public decimal? Pnl { get; set; }

    public decimal? Pct { get; set; }
}

/// <summary>
/// Perfect-hindsight best long trade over the scoped bars.
/// </summary>
public sealed class HindsightResult
{
    public DateTime? BuyTime { get; set; }

    public decimal? BuyPrice { get; set; }

    public DateTime? SellTime { get; set; }

    public decimal? SellPrice { get; set; }

    public decimal? ProfPerShare { get; set; }

    public decimal? Pct { get; set; }
}

/// <summary>
/// Strategy performance relative to the hindsight ceiling.
/// </summary>
public sealed class ChartBacktestSummary
{
    public decimal? CapturePct { get; set; }
}

/// <summary>
/// Enriched bar used internally for strategy evaluation.
/// </summary>
public sealed class EnrichedBar
{
    public required DateTime UtcTime { get; init; }

    public required decimal Open { get; init; }

    public required decimal High { get; init; }

    public required decimal Low { get; init; }

    public required decimal Close { get; init; }

    public required long Volume { get; init; }

    public required MarketSession Session { get; init; }

    public required DateOnly EasternDate { get; init; }
}
