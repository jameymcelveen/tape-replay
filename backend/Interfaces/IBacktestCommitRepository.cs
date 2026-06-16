using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Persists frozen strategy commits for out-of-sample scoring.
/// </summary>
public interface IBacktestCommitRepository
{
    Task<BacktestCommitEntity> SaveAsync(BacktestCommitEntity commit, CancellationToken cancellationToken = default);

    Task<BacktestCommitEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
