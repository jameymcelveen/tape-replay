using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Tests.Helpers;

internal sealed class FixedBarsProvider(IReadOnlyDictionary<DateOnly, IReadOnlyList<Candle>> bars) : IMarketDataProvider
{
    public Task<IReadOnlyList<Candle>> GetMinuteBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        bars.TryGetValue(date, out var dayBars);
        return Task.FromResult<IReadOnlyList<Candle>>(dayBars ?? []);
    }
}

internal sealed class InMemoryMarketDataRepository : IMarketDataRepository
{
    private readonly Dictionary<(string Ticker, DateTime DateTime), Candle> _bars = new();

    public InMemoryMarketDataRepository()
    {
    }

    public InMemoryMarketDataRepository(IReadOnlyDictionary<DateOnly, IReadOnlyList<Candle>> seed)
    {
        foreach (var (date, dayBars) in seed)
        {
            foreach (var bar in dayBars)
            {
                _bars[(bar.Ticker.ToUpperInvariant(), bar.DateTime)] = bar;
            }
        }
    }

    public Task<bool> HasDataAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var has = _bars.Keys.Any(k =>
            k.Ticker == ticker.ToUpperInvariant() && k.DateTime >= start && k.DateTime < end);
        return Task.FromResult(has);
    }

    public Task<bool> IsCoverageDoneAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default) =>
        HasDataAsync(ticker, date, cancellationToken);

    public Task<IReadOnlyList<Candle>> GetBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var rows = _bars
            .Where(k => k.Key.Ticker == ticker.ToUpperInvariant() && k.Key.DateTime >= start && k.Key.DateTime < end)
            .Select(k => k.Value)
            .OrderBy(c => c.DateTime)
            .ToList();
        return Task.FromResult<IReadOnlyList<Candle>>(rows);
    }

    public Task<IReadOnlyList<Candle>> GetBarsInRangeAsync(
        string ticker,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        var rows = _bars
            .Where(k => k.Key.Ticker == ticker.ToUpperInvariant()
                        && k.Key.DateTime >= fromUtc
                        && k.Key.DateTime <= toUtc)
            .Select(k => k.Value)
            .OrderBy(c => c.DateTime)
            .ToList();
        return Task.FromResult<IReadOnlyList<Candle>>(rows);
    }

    public Task SaveBarsAsync(string ticker, IReadOnlyList<Candle> barsToSave, CancellationToken cancellationToken = default) =>
        UpsertMinuteBarsAsync(barsToSave, cancellationToken);

    public Task UpsertMinuteBarsAsync(IReadOnlyList<Candle> bars, CancellationToken cancellationToken = default)
    {
        foreach (var bar in bars)
        {
            _bars[(bar.Ticker.ToUpperInvariant(), bar.DateTime)] = bar;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Candle>> GetMinuteBarsForPartitionAsync(
        string ticker,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = month == 12
            ? new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(year, month + 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var rows = _bars
            .Where(k => k.Key.Ticker == ticker.ToUpperInvariant() && k.Key.DateTime >= start && k.Key.DateTime < end)
            .Select(k => k.Value)
            .OrderBy(c => c.DateTime)
            .ToList();

        return Task.FromResult<IReadOnlyList<Candle>>(rows);
    }

    public Task<IReadOnlyList<string>> GetMinutePartitionKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = _bars.Keys
            .Select(k => PartitionKey.Minute(k.Ticker, k.DateTime.Year, k.DateTime.Month))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    public Task<IReadOnlyList<(int Year, int Month)>> GetDailyPartitionKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = _bars.Keys
            .Select(k => (k.DateTime.Year, k.DateTime.Month))
            .Distinct()
            .OrderBy(k => k.Year)
            .ThenBy(k => k.Month)
            .ToList();
        return Task.FromResult<IReadOnlyList<(int Year, int Month)>>(keys);
    }

    public Task<IReadOnlyList<string>> GetDistinctTickersWithMinuteDataAsync(CancellationToken cancellationToken = default)
    {
        var tickers = _bars.Keys
            .Select(k => k.Ticker)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(tickers);
    }
}
