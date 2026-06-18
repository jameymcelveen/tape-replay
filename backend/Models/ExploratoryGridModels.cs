namespace TapeReplay.Api.Models;

/// <summary>
/// Request to run exploratory backtests across tickers and trading days.
/// </summary>
public sealed class ExploratoryGridRequest
{
    public IReadOnlyList<string> Tickers { get; set; } = [];

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public StrategyConfig? Strategy { get; set; }

    public string? Dsl { get; set; }

    public TradeCostConfig? Costs { get; set; }

    public decimal StartingCapitalUsd { get; set; } = 25_000m;
}

/// <summary>
/// Exploratory calendar grid with per-day net-after-costs cells.
/// </summary>
public sealed class ExploratoryGridResponse
{
    public IReadOnlyList<DateOnly> TradingDays { get; set; } = [];

    public IReadOnlyList<ExploratoryGridRow> Rows { get; set; } = [];

    public ExploratoryGridTotals Totals { get; set; } = new();
}

/// <summary>
/// One ticker row in the exploratory grid.
/// </summary>
public sealed class ExploratoryGridRow
{
    public string Ticker { get; set; } = string.Empty;

    public IReadOnlyList<ExploratoryGridDayCell> Days { get; set; } = [];
}

/// <summary>
/// Net-after-costs result for one ticker and trading day.
/// </summary>
public sealed class ExploratoryGridDayCell
{
    public DateOnly Date { get; set; }

    public bool HasData { get; set; }

    public bool Traded { get; set; }

    public decimal NetTotalPnL { get; set; }

    public decimal GrossTotalPnL { get; set; }

    public decimal TotalCosts { get; set; }

    public int TradeCount { get; set; }
}

/// <summary>
/// Honest aggregate metrics for the visible exploratory window.
/// </summary>
public sealed class ExploratoryGridTotals
{
    public decimal NetTotalPnL { get; set; }

    public decimal WinRateDays { get; set; }

    public decimal MaxDrawdownAbsolute { get; set; }

    public int LongestLosingStreakDays { get; set; }

    public int TradingDaysWithData { get; set; }

    public int TradingDaysWithTrades { get; set; }
}
