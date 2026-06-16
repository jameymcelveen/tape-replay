using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Data;

/// <summary>
/// SQLite-backed market data repository using EF Core.
/// </summary>
public sealed class MarketDataRepository(AppDbContext dbContext) : IMarketDataRepository
{
    public async Task<bool> HasDataAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
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

    public async Task SaveBarsAsync(string ticker, IReadOnlyList<Candle> bars, CancellationToken cancellationToken = default)
    {
        if (bars.Count == 0)
        {
            return;
        }

        var normalizedTicker = ticker.ToUpperInvariant();
        var entities = bars.Select(bar => new MarketDataEntity
        {
            Ticker = normalizedTicker,
            DateTime = bar.DateTime,
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = bar.Volume
        });

        await dbContext.MarketData.AddRangeAsync(entities, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
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
