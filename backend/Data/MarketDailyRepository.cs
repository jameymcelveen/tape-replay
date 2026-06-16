using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Data;

/// <summary>
/// SQLite-backed daily bar repository.
/// </summary>
public sealed class MarketDailyRepository(AppDbContext dbContext) : IMarketDailyRepository
{
    public async Task<IReadOnlyList<DailyBar>> GetDailyBarsForPartitionAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, month, 1);
        var end = month == 12 ? new DateOnly(year + 1, 1, 1) : new DateOnly(year, month + 1, 1);

        var rows = await dbContext.MarketDaily
            .AsNoTracking()
            .Where(d => d.Date >= start && d.Date < end)
            .OrderBy(d => d.Ticker)
            .ThenBy(d => d.Date)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task UpsertDailyBarsAsync(IReadOnlyList<DailyBar> bars, CancellationToken cancellationToken = default)
    {
        if (bars.Count == 0)
        {
            return;
        }

        foreach (var group in bars.GroupBy(b => (b.Ticker.ToUpperInvariant(), b.Date)))
        {
            var (ticker, date) = group.Key;
            var bar = group.Last();

            var existing = await dbContext.MarketDaily
                .FirstOrDefaultAsync(d => d.Ticker == ticker && d.Date == date, cancellationToken);

            if (existing is null)
            {
                dbContext.MarketDaily.Add(new MarketDailyEntity
                {
                    Ticker = ticker,
                    Date = date,
                    Open = bar.Open,
                    High = bar.High,
                    Low = bar.Low,
                    Close = bar.Close,
                    Volume = bar.Volume
                });
            }
            else
            {
                existing.Open = bar.Open;
                existing.High = bar.High;
                existing.Low = bar.Low;
                existing.Close = bar.Close;
                existing.Volume = bar.Volume;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertDailyFromMinuteBarsAsync(IReadOnlyList<Candle> minuteBars, CancellationToken cancellationToken = default)
    {
        var daily = minuteBars
            .GroupBy(b => (b.Ticker.ToUpperInvariant(), DateOnly.FromDateTime(b.DateTime)))
            .Select(g =>
            {
                var ordered = g.OrderBy(c => c.DateTime).ToList();
                return new DailyBar
                {
                    Ticker = g.Key.Item1,
                    Date = g.Key.Item2,
                    Open = ordered[0].Open,
                    High = ordered.Max(c => c.High),
                    Low = ordered.Min(c => c.Low),
                    Close = ordered[^1].Close,
                    Volume = ordered.Sum(c => c.Volume)
                };
            })
            .ToList();

        await UpsertDailyBarsAsync(daily, cancellationToken);
    }

    private static DailyBar Map(MarketDailyEntity entity) => new()
    {
        Ticker = entity.Ticker,
        Date = entity.Date,
        Open = entity.Open,
        High = entity.High,
        Low = entity.Low,
        Close = entity.Close,
        Volume = entity.Volume
    };
}
