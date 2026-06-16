namespace TapeReplay.Api.Models;

/// <summary>
/// Skeptical performance metrics where drawdown and ruin risk headline over return.
/// </summary>
public sealed class HonestMetrics
{
    public decimal GrossTotalPnL { get; init; }

    public decimal NetTotalPnL { get; init; }

    public decimal TotalCosts { get; init; }

    public decimal NetReturnPercent { get; init; }

    public decimal MaxDrawdownAbsolute { get; init; }

    public decimal MaxDrawdownPercent { get; init; }

    public int LongestLosingStreakTrades { get; init; }

    public int LongestLosingStreakDays { get; init; }

    public int? DaysToRecoverFromMaxDrawdown { get; init; }

    public bool RecoveredFromMaxDrawdown { get; init; }

    public decimal WinRate { get; init; }

    public decimal AverageWin { get; init; }

    public decimal AverageLoss { get; init; }

    public decimal PayoffRatio { get; init; }

    public decimal ExpectancyPerTrade { get; init; }

    public decimal? SharpeRatio { get; init; }

    public int TradeCount { get; init; }

    public required string Verdict { get; init; }
}
