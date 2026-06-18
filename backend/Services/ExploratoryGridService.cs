using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Services;

/// <summary>
/// Runs exploratory single-day backtests across a ticker and date grid.
/// </summary>
public sealed class ExploratoryGridService(
    IBacktestEngine backtestEngine,
    MarketDataService marketDataService,
    IStrategyParser parser)
{
    /// <summary>
    /// Computes net-after-costs cells for each ticker and trading day in range.
    /// </summary>
    public async Task<ExploratoryGridResponse> RunAsync(
        ExploratoryGridRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.To < request.From)
        {
            throw new ArgumentException("'to' must be on or after 'from'.");
        }

        if (request.Tickers.Count == 0)
        {
            throw new ArgumentException("At least one ticker is required.");
        }

        var config = request.Strategy ?? parser.Parse(request.Dsl ?? string.Empty);
        var costs = TradeCostDefaults.Resolve(request.Costs);
        var tradingDays = EnumerateTradingDays(request.From, request.To);
        var rows = new List<ExploratoryGridRow>();

        foreach (var ticker in request.Tickers.Select(t => t.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal))
        {
            var dayCells = new List<ExploratoryGridDayCell>();
            foreach (var day in tradingDays)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bars = await marketDataService.GetMinuteBarsAsync(ticker, day, cancellationToken);
                if (bars.Count == 0)
                {
                    dayCells.Add(new ExploratoryGridDayCell
                    {
                        Date = day,
                        HasData = false,
                        Traded = false
                    });
                    continue;
                }

                var result = backtestEngine.Run(
                    ticker,
                    day,
                    config,
                    bars,
                    costs,
                    SampleLabel.Exploratory,
                    request.StartingCapitalUsd);

                dayCells.Add(new ExploratoryGridDayCell
                {
                    Date = day,
                    HasData = true,
                    Traded = result.Trades.Count > 0,
                    NetTotalPnL = result.NetTotalPnL,
                    GrossTotalPnL = result.GrossTotalPnL,
                    TotalCosts = result.TotalCosts,
                    TradeCount = result.Trades.Count
                });
            }

            rows.Add(new ExploratoryGridRow { Ticker = ticker, Days = dayCells });
        }

        return new ExploratoryGridResponse
        {
            TradingDays = tradingDays,
            Rows = rows,
            Totals = ComputeTotals(rows)
        };
    }

    private static ExploratoryGridTotals ComputeTotals(IReadOnlyList<ExploratoryGridRow> rows)
    {
        var dailyNet = new SortedDictionary<DateOnly, decimal>();
        var tradedDays = 0;
        var winningDays = 0;
        var daysWithData = 0;

        foreach (var row in rows)
        {
            foreach (var cell in row.Days)
            {
                if (!cell.HasData)
                {
                    continue;
                }

                daysWithData++;
                dailyNet[cell.Date] = dailyNet.GetValueOrDefault(cell.Date) + cell.NetTotalPnL;

                if (cell.Traded)
                {
                    tradedDays++;
                    if (cell.NetTotalPnL > 0m)
                    {
                        winningDays++;
                    }
                }
            }
        }

        var equity = 0m;
        var peak = 0m;
        var maxDrawdown = 0m;
        var longestLosing = 0;
        var currentLosing = 0;

        foreach (var (_, net) in dailyNet)
        {
            equity += net;
            peak = Math.Max(peak, equity);
            maxDrawdown = Math.Max(maxDrawdown, peak - equity);

            if (net < 0m)
            {
                currentLosing++;
                longestLosing = Math.Max(longestLosing, currentLosing);
            }
            else
            {
                currentLosing = 0;
            }
        }

        var netTotal = dailyNet.Values.Sum();
        var winRate = tradedDays > 0 ? (decimal)winningDays / tradedDays * 100m : 0m;

        return new ExploratoryGridTotals
        {
            NetTotalPnL = netTotal,
            WinRateDays = winRate,
            MaxDrawdownAbsolute = maxDrawdown,
            LongestLosingStreakDays = longestLosing,
            TradingDaysWithData = daysWithData,
            TradingDaysWithTrades = tradedDays
        };
    }

    private static IReadOnlyList<DateOnly> EnumerateTradingDays(DateOnly from, DateOnly to)
    {
        var days = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (TradingCalendar.IsTradingDay(date))
            {
                days.Add(date);
            }
        }

        return days;
    }
}
