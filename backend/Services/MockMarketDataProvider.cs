using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Synthetic minute bars for local development without a Polygon API key.
/// </summary>
public sealed class MockMarketDataProvider : IMarketDataProvider
{
    public Task<IReadOnlyList<Candle>> GetMinuteBarsAsync(
        string ticker,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var normalizedTicker = ticker.ToUpperInvariant();
        var bars = new List<Candle>();
        var start = date.ToDateTime(new TimeOnly(9, 30), DateTimeKind.Utc);
        var price = 50m;
        var runningHigh = price;

        for (var minute = 0; minute < 390; minute++)
        {
            var timestamp = start.AddMinutes(minute);
            var drift = (decimal)(Random.Shared.NextDouble() * 0.3 - 0.05);
            var open = price;
            var close = Math.Max(1m, open + drift);
            var high = Math.Max(open, close) + (decimal)Random.Shared.NextDouble() * 0.15m;

            if (minute == 45)
            {
                high = runningHigh + 0.75m;
                close = runningHigh + 0.50m;
            }

            var low = Math.Min(open, close) - (decimal)Random.Shared.NextDouble() * 0.10m;
            runningHigh = Math.Max(runningHigh, high);

            bars.Add(new Candle
            {
                Ticker = normalizedTicker,
                DateTime = timestamp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = Random.Shared.Next(10_000, 250_000)
            });

            price = close;
        }

        return Task.FromResult<IReadOnlyList<Candle>>(bars);
    }
}
