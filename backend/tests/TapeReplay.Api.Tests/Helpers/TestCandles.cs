using TapeReplay.Api.Models;
using TapeReplay.Api.Services.ChartBacktest;

namespace TapeReplay.Api.Tests.Helpers;

internal static class TestCandles
{
    public static IReadOnlyList<Candle> RisingBars(string ticker, DateOnly date, int count, decimal startPrice = 100m)
    {
        var bars = new List<Candle>();
        var price = startPrice;
        var start = EasternMarketTime.ToUtc(date, EasternMarketTime.RegularOpen);

        for (var i = 0; i < count; i++)
        {
            var open = price;
            var close = open + 1.5m;
            var high = close + 0.5m;
            var low = open - 0.1m;
            bars.Add(new Candle
            {
                Ticker = ticker,
                DateTime = start.AddMinutes(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 50_000
            });
            price = close;
        }

        return bars;
    }

    public static IReadOnlyList<Candle> CreateMinuteSeries(string ticker, DateOnly date, int count, decimal startPrice = 100m) =>
        RisingBars(ticker, date, count, startPrice);

    /// <summary>
    /// Oracle P&amp;L if a cheater could buy at open and sell at close whenever close &gt; open.
    /// </summary>
    public static decimal OracleLookAheadPnL(IReadOnlyList<Candle> bars, int shares = 10)
    {
        return bars
            .Where(b => b.Close > b.Open)
            .Sum(b => (b.Close - b.Open) * shares);
    }
}
