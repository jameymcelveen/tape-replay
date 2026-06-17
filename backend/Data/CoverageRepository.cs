using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Data;

/// <summary>
/// EF Core implementation of the coverage grid.
/// </summary>
public sealed class CoverageRepository(AppDbContext dbContext) : ICoverageRepository
{
    public Task<bool> IsMinuteDoneAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        var normalized = ticker.ToUpperInvariant();
        return dbContext.TickerMinuteCoverage
            .AsNoTracking()
            .AnyAsync(
                c => c.Ticker == normalized && c.Date == date && c.Status == CoverageStatus.Done,
                cancellationToken);
    }

    public Task<bool> IsDailyDoneAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return dbContext.MarketDailyCoverage
            .AsNoTracking()
            .AnyAsync(c => c.Date == date && c.Status == CoverageStatus.Done, cancellationToken);
    }

    public async Task EnsureMinutePendingAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        var normalized = ticker.ToUpperInvariant();
        var existing = await dbContext.TickerMinuteCoverage
            .FirstOrDefaultAsync(c => c.Ticker == normalized && c.Date == date, cancellationToken);

        if (existing is null)
        {
            dbContext.TickerMinuteCoverage.Add(new TickerMinuteCoverageEntity
            {
                Ticker = normalized,
                Date = date,
                Status = CoverageStatus.Pending,
                Provenance = CoverageProvenance.Api,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<TickerMinuteCoverageEntity>> GetPendingMinuteCellsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.TickerMinuteCoverage
            .AsNoTracking()
            .Where(c => c.Status == CoverageStatus.Pending)
            .OrderBy(c => c.Date)
            .ThenBy(c => c.Ticker)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkMinuteDoneAsync(
        string ticker,
        DateOnly date,
        CoverageProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        var normalized = ticker.ToUpperInvariant();
        var cell = await dbContext.TickerMinuteCoverage
            .FirstOrDefaultAsync(c => c.Ticker == normalized && c.Date == date, cancellationToken);

        if (cell is null)
        {
            dbContext.TickerMinuteCoverage.Add(new TickerMinuteCoverageEntity
            {
                Ticker = normalized,
                Date = date,
                Status = CoverageStatus.Done,
                Provenance = provenance,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            cell.Status = CoverageStatus.Done;
            cell.Provenance = provenance;
            cell.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkMinuteSkippedAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        var normalized = ticker.ToUpperInvariant();
        var cell = await dbContext.TickerMinuteCoverage
            .FirstOrDefaultAsync(c => c.Ticker == normalized && c.Date == date, cancellationToken);

        if (cell is null)
        {
            dbContext.TickerMinuteCoverage.Add(new TickerMinuteCoverageEntity
            {
                Ticker = normalized,
                Date = date,
                Status = CoverageStatus.Skipped,
                Provenance = CoverageProvenance.Api,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            cell.Status = CoverageStatus.Skipped;
            cell.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkDailyDoneAsync(
        DateOnly date,
        CoverageProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        var cell = await dbContext.MarketDailyCoverage
            .FirstOrDefaultAsync(c => c.Date == date, cancellationToken);

        if (cell is null)
        {
            dbContext.MarketDailyCoverage.Add(new MarketDailyCoverageEntity
            {
                Date = date,
                Status = CoverageStatus.Done,
                Provenance = provenance,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            cell.Status = CoverageStatus.Done;
            cell.Provenance = provenance;
            cell.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkMinuteRangeDoneFromBarsAsync(
        IReadOnlyList<string> tickers,
        DateOnly startDate,
        DateOnly endDate,
        CoverageProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        foreach (var ticker in tickers.Distinct(StringComparer.Ordinal))
        {
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                await MarkMinuteDoneAsync(ticker, date, provenance, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<TickerMinuteCoverageEntity>> GetMinuteCoverageAsync(
        string? ticker,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TickerMinuteCoverage.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(ticker))
        {
            query = query.Where(c => c.Ticker == ticker.ToUpperInvariant());
        }

        if (startDate.HasValue)
        {
            query = query.Where(c => c.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(c => c.Date <= endDate.Value);
        }

        return await query.OrderBy(c => c.Date).ThenBy(c => c.Ticker).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketDailyCoverageEntity>> GetDailyCoverageAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MarketDailyCoverage.AsNoTracking().AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(c => c.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(c => c.Date <= endDate.Value);
        }

        return await query.OrderBy(c => c.Date).ToListAsync(cancellationToken);
    }
}
