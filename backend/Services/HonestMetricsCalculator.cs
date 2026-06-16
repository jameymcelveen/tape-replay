using System.Globalization;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Computes skeptical metrics where drawdown and ruin risk headline over return.
/// </summary>
public sealed class HonestMetricsCalculator : IHonestMetricsCalculator
{
    public HonestMetrics Compute(
        IReadOnlyList<TradeResult> trades,
        IReadOnlyList<EquityPoint> equityCurve,
        decimal startingCapital,
        SampleLabel sampleLabel)
    {
        var gross = trades.Sum(t => t.GrossPnL);
        var net = trades.Sum(t => t.NetPnL);
        var costs = trades.Sum(t => t.TotalCosts);
        var netReturnPercent = startingCapital == 0 ? 0m : net / startingCapital * 100m;

        var (maxDdAbs, maxDdPct) = CalculateMaxDrawdown(equityCurve, startingCapital);
        var (longestStreak, longestDays) = CalculateLongestLosingStreak(trades);
        var (recovered, daysToRecover) = CalculateRecovery(equityCurve, startingCapital);

        var winners = trades.Where(t => t.NetPnL > 0).ToList();
        var losers = trades.Where(t => t.NetPnL < 0).ToList();
        var winRate = trades.Count == 0 ? 0m : (decimal)winners.Count / trades.Count * 100m;
        var avgWin = winners.Count == 0 ? 0m : winners.Average(t => t.NetPnL);
        var avgLoss = losers.Count == 0 ? 0m : losers.Average(t => t.NetPnL);
        var payoff = avgLoss == 0 ? 0m : Math.Abs(avgWin / avgLoss);
        var expectancy = trades.Count == 0 ? 0m : net / trades.Count;
        var sharpe = CalculateSharpe(equityCurve, startingCapital);

        var verdict = BuildVerdict(sampleLabel, netReturnPercent, maxDdPct, longestStreak, longestDays, recovered, net);

        return new HonestMetrics
        {
            GrossTotalPnL = gross,
            NetTotalPnL = net,
            TotalCosts = costs,
            NetReturnPercent = netReturnPercent,
            MaxDrawdownAbsolute = maxDdAbs,
            MaxDrawdownPercent = maxDdPct,
            LongestLosingStreakTrades = longestStreak,
            LongestLosingStreakDays = longestDays,
            DaysToRecoverFromMaxDrawdown = daysToRecover,
            RecoveredFromMaxDrawdown = recovered,
            WinRate = winRate,
            AverageWin = avgWin,
            AverageLoss = avgLoss,
            PayoffRatio = payoff,
            ExpectancyPerTrade = expectancy,
            SharpeRatio = sharpe,
            TradeCount = trades.Count,
            Verdict = verdict
        };
    }

    private static (decimal Absolute, decimal Percent) CalculateMaxDrawdown(
        IReadOnlyList<EquityPoint> equityCurve,
        decimal startingCapital)
    {
        if (equityCurve.Count == 0)
        {
            return (0m, 0m);
        }

        var peak = startingCapital;
        var maxDrawdown = 0m;
        var maxDrawdownPct = 0m;

        foreach (var point in equityCurve)
        {
            peak = Math.Max(peak, point.Equity);
            var drawdown = peak - point.Equity;
            maxDrawdown = Math.Max(maxDrawdown, drawdown);
            if (peak > 0)
            {
                maxDrawdownPct = Math.Max(maxDrawdownPct, drawdown / peak * 100m);
            }
        }

        return (maxDrawdown, maxDrawdownPct);
    }

    private static (int TradeStreak, int DayStreak) CalculateLongestLosingStreak(IReadOnlyList<TradeResult> trades)
    {
        var longestTrade = 0;
        var currentTrade = 0;
        var longestDay = 0;
        DateOnly? currentStart = null;

        foreach (var trade in trades.OrderBy(t => t.ExitTime))
        {
            if (trade.NetPnL < 0)
            {
                currentTrade++;
                var exitDate = DateOnly.FromDateTime(trade.ExitTime);
                currentStart ??= exitDate;
                longestTrade = Math.Max(longestTrade, currentTrade);
            }
            else
            {
                if (currentStart.HasValue && currentTrade > 0)
                {
                    var exitDate = DateOnly.FromDateTime(trade.ExitTime);
                    var days = exitDate.DayNumber - currentStart.Value.DayNumber + 1;
                    longestDay = Math.Max(longestDay, days);
                }

                currentTrade = 0;
                currentStart = null;
            }
        }

        if (currentStart.HasValue && currentTrade > 0 && trades.Count > 0)
        {
            var lastDate = DateOnly.FromDateTime(trades.Max(t => t.ExitTime));
            longestDay = Math.Max(longestDay, lastDate.DayNumber - currentStart.Value.DayNumber + 1);
        }

        return (longestTrade, longestDay);
    }

    private static (bool Recovered, int? Days) CalculateRecovery(
        IReadOnlyList<EquityPoint> equityCurve,
        decimal startingCapital)
    {
        if (equityCurve.Count < 2)
        {
            return (true, 0);
        }

        var peak = startingCapital;
        var troughEquity = startingCapital;
        var troughDate = equityCurve[0].Date;
        var peakAtTrough = startingCapital;
        var inDrawdown = false;

        foreach (var point in equityCurve)
        {
            if (point.Equity >= peak)
            {
                peak = point.Equity;
                inDrawdown = false;
                continue;
            }

            if (!inDrawdown)
            {
                inDrawdown = true;
                peakAtTrough = peak;
                troughEquity = point.Equity;
                troughDate = point.Date;
            }
            else if (point.Equity < troughEquity)
            {
                troughEquity = point.Equity;
                troughDate = point.Date;
            }
        }

        if (!inDrawdown)
        {
            return (true, 0);
        }

        foreach (var point in equityCurve.Where(p => p.Date > troughDate))
        {
            if (point.Equity >= peakAtTrough)
            {
                return (true, point.Date.DayNumber - troughDate.DayNumber);
            }
        }

        return (false, null);
    }

    private static decimal? CalculateSharpe(IReadOnlyList<EquityPoint> equityCurve, decimal startingCapital)
    {
        if (equityCurve.Count < 3)
        {
            return null;
        }

        var returns = new List<decimal>();
        for (var i = 1; i < equityCurve.Count; i++)
        {
            var prev = equityCurve[i - 1].Equity;
            if (prev == 0)
            {
                continue;
            }

            returns.Add((equityCurve[i].Equity - prev) / prev);
        }

        if (returns.Count < 2)
        {
            return null;
        }

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1);
        if (variance == 0)
        {
            return null;
        }

        var stdDev = (decimal)Math.Sqrt((double)variance);
        return stdDev == 0 ? null : mean / stdDev * (decimal)Math.Sqrt(252);
    }

    private static string BuildVerdict(
        SampleLabel sampleLabel,
        decimal netReturnPercent,
        decimal maxDrawdownPercent,
        int longestLosingStreak,
        int longestLosingDays,
        bool recovered,
        decimal netPnL)
    {
        var label = sampleLabel switch
        {
            SampleLabel.OutOfSample => "Out-of-sample",
            SampleLabel.InSample => "In-sample (suspect, tuning allowed)",
            _ => "Exploratory single day (not evidence)"
        };

        var recovery = recovered ? "recovered from max drawdown" : "never recovered from max drawdown";
        var survival = maxDrawdownPercent >= 30m || netReturnPercent <= -15m || !recovered
            ? "A no-income account would likely not survive this."
            : netReturnPercent < 0
                ? "Negative edge after costs; do not trade this live without new evidence."
                : "Still not proof of edge until out-of-sample confirms it.";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{label}: net {netReturnPercent:F1}% after costs, max drawdown {maxDrawdownPercent:F1}%, longest losing streak {longestLosingStreak} trades ({longestLosingDays} days), {recovery}. {survival}");
    }
}
