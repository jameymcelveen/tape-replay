using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Train/test harness: commit in-sample, evaluate out-of-sample.
/// </summary>
public interface IBacktestHarness
{
    Task<BacktestCommitResponse> CommitAsync(BacktestCommitRequest request, CancellationToken cancellationToken = default);

    Task<BacktestEvaluateResponse> EvaluateAsync(BacktestEvaluateRequest request, CancellationToken cancellationToken = default);
}
