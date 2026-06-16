using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Tests.Helpers;

internal sealed class InMemoryBacktestCommitRepository : IBacktestCommitRepository
{
    private readonly Dictionary<Guid, BacktestCommitEntity> _store = [];

    public Task<BacktestCommitEntity> SaveAsync(BacktestCommitEntity commit, CancellationToken cancellationToken = default)
    {
        _store[commit.Id] = commit;
        return Task.FromResult(commit);
    }

    public Task<BacktestCommitEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var commit);
        return Task.FromResult(commit);
    }
}
