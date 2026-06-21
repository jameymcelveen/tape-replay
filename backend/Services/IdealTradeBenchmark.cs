using TapeReplay.Api.Models;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Services.ChartBacktest;

namespace TapeReplay.Api.Services;

/// <summary>
/// Computes perfect-hindsight buy/sell benchmarks for honest capture comparison.
/// </summary>
public static class IdealTradeBenchmark
{
    /// <summary>
    /// Finds the best possible long pair over scoped bars and optional share sizing.
    /// </summary>
    public static HindsightResult Compute(
        IReadOnlyList<Candle> bars,
        bool regularSessionOnly,
        decimal positionSizeUsd)
    {
        var enriched = ToEnrichedBars(bars, regularSessionOnly);
        var ideal = PerfectHindsightCalculator.Compute(enriched);
        if (ideal.BuyPrice is null or <= 0m || ideal.ProfPerShare is null or <= 0m)
        {
            return ideal;
        }

        var shares = (int)Math.Floor(positionSizeUsd / ideal.BuyPrice.Value);
        if (shares <= 0)
        {
            return ideal;
        }

        return ideal;
    }

    /// <summary>
    /// Strategy net P&amp;L as a percent of ideal gross P&amp;L on the same share count.
    /// </summary>
    public static decimal? ComputeCapturePercent(
        decimal netTotalPnL,
        HindsightResult ideal,
        decimal positionSizeUsd)
    {
        if (ideal.BuyPrice is null or <= 0m || ideal.ProfPerShare is null or <= 0m)
        {
            return null;
        }

        var shares = (int)Math.Floor(positionSizeUsd / ideal.BuyPrice.Value);
        if (shares <= 0)
        {
            return null;
        }

        var idealGross = ideal.ProfPerShare.Value * shares;
        if (idealGross <= 0m)
        {
            return null;
        }

        return netTotalPnL / idealGross * 100m;
    }

    private static List<EnrichedBar> ToEnrichedBars(IReadOnlyList<Candle> bars, bool regularSessionOnly)
    {
        var result = new List<EnrichedBar>(bars.Count);
        foreach (var bar in bars.OrderBy(b => b.DateTime))
        {
            var session = MarketSessionClassifier.Classify(bar.DateTime);
            if (regularSessionOnly && session != MarketSession.Regular)
            {
                continue;
            }

            result.Add(new EnrichedBar
            {
                UtcTime = bar.DateTime.Kind == DateTimeKind.Utc
                    ? bar.DateTime
                    : DateTime.SpecifyKind(bar.DateTime, DateTimeKind.Utc),
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume,
                Session = session,
                EasternDate = MarketSessionClassifier.GetEasternDate(bar.DateTime)
            });
        }

        return result;
    }
}
