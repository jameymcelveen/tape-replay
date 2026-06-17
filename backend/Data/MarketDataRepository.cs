using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.DataDistribution;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Data;

/// <summary>
/// SQLite-backed market data repository using EF Core.
/// </summary>
public sealed class MarketDataRepository(AppDbContext dbContext) : IMarketDataRepository
{
    public async Task<bool> HasDataAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (await IsCoverageDoneAsync(ticker, date, cancellationToken))
        {
            return true;
        }

        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);

        return await dbContext.MarketData
            .AsNoTracking()
            .AnyAsync(
                m => m.Ticker == ticker.ToUpperInvariant()
                     && m.DateTime >= start
                     && m.DateTime < end,
                cancellationToken);
    }

    public Task<bool> IsCoverageDoneAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        var normalized = ticker.ToUpperInvariant();
        return dbContext.TickerMinuteCoverage
            .AsNoTracking()
            .AnyAsync(
                c => c.Ticker == normalized && c.Date == date && c.Status == CoverageStatus.Done,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Candle>> GetBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        var normalizedTicker = ticker.ToUpperInvariant();
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);

        var rows = await dbContext.MarketData
            .AsNoTracking()
            .Where(m => m.Ticker == normalizedTicker && m.DateTime >= start && m.DateTime < end)
            .OrderBy(m => m.DateTime)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToCandle).ToList();
    }

    public async Task<IReadOnlyList<Candle>> GetBarsInRangeAsync(
        string ticker,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedTicker = ticker.ToUpperInvariant();
        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);

        if (to < from)
        {
            (from, to) = (to, from);
        }

        var rows = await dbContext.MarketData
            .AsNoTracking()
            .Where(m => m.Ticker == normalizedTicker && m.DateTime >= from && m.DateTime <= to)
            .OrderBy(m => m.DateTime)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToCandle).ToList();
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public async Task SaveBarsAsync(string ticker, IReadOnlyList<Candle> bars, CancellationToken cancellationToken = default)
    {
        if (bars.Count == 0)
        {
            return;
        }

        await UpsertMinuteBarsAsync(bars, cancellationToken);
    }

    public async Task UpsertMinuteBarsAsync(IReadOnlyList<Candle> bars, CancellationToken cancellationToken = default)
    {
        if (bars.Count == 0)
        {
            return;
        }

        foreach (var group in bars.GroupBy(b => b.Ticker.ToUpperInvariant()))
        {
            var ticker = group.Key;
            var timestamps = group.Select(b => b.DateTime).Distinct().ToList();
            var existing = await dbContext.MarketData
                .Where(m => m.Ticker == ticker && timestamps.Contains(m.DateTime))
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
            {
                dbContext.MarketData.RemoveRange(existing);
            }

            var entities = group.Select(bar => new MarketDataEntity
            {
                Ticker = ticker,
                DateTime = bar.DateTime,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume
            });

            await dbContext.MarketData.AddRangeAsync(entities, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Candle>> GetMinuteBarsForPartitionAsync(
        string ticker,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var normalizedTicker = ticker.ToUpperInvariant();
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = month == 12
            ? new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(year, month + 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var rows = await dbContext.MarketData
            .AsNoTracking()
            .Where(m => m.Ticker == normalizedTicker && m.DateTime >= start && m.DateTime < end)
            .OrderBy(m => m.DateTime)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToCandle).ToList();
    }

    public async Task<IReadOnlyList<string>> GetMinutePartitionKeysAsync(CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.MarketData
            .AsNoTracking()
            .Select(m => new { m.Ticker, m.DateTime })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => PartitionKey.Minute(r.Ticker, r.DateTime.Year, r.DateTime.Month))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<(int Year, int Month)>> GetDailyPartitionKeysAsync(CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.MarketDaily
            .AsNoTracking()
            .Select(d => new { d.Date })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => (r.Date.Year, r.Date.Month))
            .Distinct()
            .OrderBy(k => k.Year)
            .ThenBy(k => k.Month)
            .ToList();
    }

    private static Candle MapToCandle(MarketDataEntity entity) => new()
    {
        Ticker = entity.Ticker,
        DateTime = entity.DateTime,
        Open = entity.Open,
        High = entity.High,
        Low = entity.Low,
        Close = entity.Close,
        Volume = entity.Volume
    };
}
