using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.ChartBacktest;

namespace TapeReplay.Api.Data;

/// <summary>
/// SQLite cache for strategy heatmap cells.
/// </summary>
public sealed class StrategyResultRepository(AppDbContext dbContext) : IStrategyResultRepository
{
    public async Task<IReadOnlyDictionary<DateOnly, StrategyResultEntity>> GetCachedAsync(
        string ticker,
        DateOnly from,
        DateOnly to,
        string strategyConfigHash,
        CancellationToken cancellationToken = default)
    {
        var normalized = ticker.ToUpperInvariant();
        var rows = await dbContext.StrategyResults
            .AsNoTracking()
            .Where(r =>
                r.Ticker == normalized
                && r.StrategyConfigHash == strategyConfigHash
                && r.Date >= from
                && r.Date <= to)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Date);
    }

    public async Task SaveAsync(IReadOnlyList<StrategyResultEntity> results, CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
        {
            return;
        }

        foreach (var result in results)
        {
            result.Ticker = result.Ticker.ToUpperInvariant();
            var existing = await dbContext.StrategyResults
                .FirstOrDefaultAsync(
                    r => r.Ticker == result.Ticker
                         && r.Date == result.Date
                         && r.StrategyConfigHash == result.StrategyConfigHash,
                    cancellationToken);

            if (existing is null)
            {
                dbContext.StrategyResults.Add(result);
            }
            else
            {
                existing.HasData = result.HasData;
                existing.Traded = result.Traded;
                existing.PnlPct = result.PnlPct;
                existing.CapturePct = result.CapturePct;
                existing.PnlDollar = result.PnlDollar;
                existing.EntryTime = result.EntryTime;
                existing.EntryPrice = result.EntryPrice;
                existing.ExitTime = result.ExitTime;
                existing.ExitPrice = result.ExitPrice;
                existing.ExitReason = result.ExitReason;
                existing.ComputedAt = result.ComputedAt;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
