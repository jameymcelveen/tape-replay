using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Computes skeptical headline metrics from trade and equity data.
/// </summary>
public interface IHonestMetricsCalculator
{
    HonestMetrics Compute(
        IReadOnlyList<TradeResult> trades,
        IReadOnlyList<EquityPoint> equityCurve,
        decimal startingCapital,
        SampleLabel sampleLabel);
}

/// <summary>
/// A point on the cumulative equity curve with calendar date.
/// </summary>
public sealed class EquityPoint
{
    public DateOnly Date { get; init; }

    public decimal Equity { get; init; }
}
