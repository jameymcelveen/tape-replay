using TapeReplay.Api.Models.ChartBacktest;

namespace TapeReplay.Api.Services.ChartBacktest;

/// <summary>
/// Computes the best possible long trade (buy low, sell high) over a bar series.
/// </summary>
public static class PerfectHindsightCalculator
{
    /// <summary>
    /// Finds the maximum profit long trade with buy strictly before sell.
    /// </summary>
    public static HindsightResult Compute(IReadOnlyList<EnrichedBar> bars)
    {
        if (bars.Count < 2)
        {
            return new HindsightResult();
        }

        var minLow = bars[0].Low;
        var minIndex = 0;
        decimal bestProfit = 0;
        int bestBuyIndex = 0;
        int bestSellIndex = 0;

        for (var i = 1; i < bars.Count; i++)
        {
            var profit = bars[i].High - minLow;
            if (profit > bestProfit && i > minIndex)
            {
                bestProfit = profit;
                bestBuyIndex = minIndex;
                bestSellIndex = i;
            }

            if (bars[i].Low < minLow)
            {
                minLow = bars[i].Low;
                minIndex = i;
            }
        }

        if (bestProfit <= 0)
        {
            return new HindsightResult();
        }

        var buyBar = bars[bestBuyIndex];
        var sellBar = bars[bestSellIndex];

        return new HindsightResult
        {
            BuyTime = buyBar.UtcTime,
            BuyPrice = buyBar.Low,
            SellTime = sellBar.UtcTime,
            SellPrice = sellBar.High,
            ProfPerShare = bestProfit,
            Pct = buyBar.Low == 0 ? 0 : bestProfit / buyBar.Low * 100m
        };
    }
}
