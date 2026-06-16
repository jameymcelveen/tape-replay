using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Data;

/// <summary>
/// SQLite-backed repository for frozen strategy commits.
/// </summary>
public sealed class BacktestCommitRepository(AppDbContext dbContext) : IBacktestCommitRepository
{
    public async Task<BacktestCommitEntity> SaveAsync(BacktestCommitEntity commit, CancellationToken cancellationToken = default)
    {
        dbContext.BacktestCommits.Add(commit);
        await dbContext.SaveChangesAsync(cancellationToken);
        return commit;
    }

    public async Task<BacktestCommitEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.BacktestCommits
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }
}
