using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Train/test harness: freeze strategy on in-sample window, score on out-of-sample only.
/// </summary>
public sealed class BacktestHarness(
    IBacktestEngine backtestEngine,
    MarketDataService marketDataService,
    IStrategyParser parser,
    IBacktestCommitRepository commitRepository) : IBacktestHarness
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<BacktestCommitResponse> CommitAsync(
        BacktestCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(request.InSampleStart, request.InSampleEnd);
        var config = ParseStrategy(request.Strategy, request.Dsl);
        var costs = TradeCostDefaults.Resolve(request.Costs);

        var bars = await marketDataService.GetMinuteBarsForRangeAsync(
            request.Ticker,
            request.InSampleStart,
            request.InSampleEnd,
            cancellationToken);

        var inSample = backtestEngine.RunWindow(
            request.Ticker,
            request.InSampleStart,
            request.InSampleEnd,
            config,
            bars,
            costs,
            SampleLabel.InSample,
            request.StartingCapitalUsd);

        var commitId = Guid.NewGuid();
        var entity = new BacktestCommitEntity
        {
            Id = commitId,
            Ticker = request.Ticker.ToUpperInvariant(),
            InSampleStart = request.InSampleStart,
            InSampleEnd = request.InSampleEnd,
            StrategyJson = JsonSerializer.Serialize(config, JsonOptions),
            InSampleNetReturnPercent = inSample.Metrics.NetReturnPercent,
            CommittedAt = DateTime.UtcNow
        };

        await commitRepository.SaveAsync(entity, cancellationToken);

        return new BacktestCommitResponse
        {
            CommitId = commitId,
            CommittedAt = entity.CommittedAt,
            FrozenStrategy = config,
            InSample = inSample
        };
    }

    public async Task<BacktestEvaluateResponse> EvaluateAsync(
        BacktestEvaluateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(request.OutOfSampleStart, request.OutOfSampleEnd);

        var commit = await commitRepository.GetAsync(request.CommitId, cancellationToken)
            ?? throw new InvalidOperationException($"Commit {request.CommitId} not found.");

        var config = JsonSerializer.Deserialize<StrategyConfig>(commit.StrategyJson, JsonOptions)
            ?? throw new InvalidOperationException("Frozen strategy could not be deserialized.");

        var costs = TradeCostDefaults.Resolve(request.Costs);

        var inSampleBars = await marketDataService.GetMinuteBarsForRangeAsync(
            commit.Ticker,
            commit.InSampleStart,
            commit.InSampleEnd,
            cancellationToken);

        var inSample = backtestEngine.RunWindow(
            commit.Ticker,
            commit.InSampleStart,
            commit.InSampleEnd,
            config,
            inSampleBars,
            costs,
            SampleLabel.InSample,
            request.StartingCapitalUsd);

        var outOfSampleBars = await marketDataService.GetMinuteBarsForRangeAsync(
            commit.Ticker,
            request.OutOfSampleStart,
            request.OutOfSampleEnd,
            cancellationToken);

        var outOfSample = backtestEngine.RunWindow(
            commit.Ticker,
            request.OutOfSampleStart,
            request.OutOfSampleEnd,
            config,
            outOfSampleBars,
            costs,
            SampleLabel.OutOfSample,
            request.StartingCapitalUsd);

        var warning = BuildOverfittingWarning(inSample.Metrics.NetReturnPercent, outOfSample.Metrics.NetReturnPercent);

        return new BacktestEvaluateResponse
        {
            CommitId = commit.Id,
            OutOfSample = outOfSample,
            InSample = inSample,
            OverfittingWarning = warning,
            Verdict = outOfSample.Metrics.Verdict
        };
    }

    private StrategyConfig ParseStrategy(StrategyConfig? strategy, string? dsl)
    {
        return strategy ?? parser.Parse(dsl ?? string.Empty);
    }

    private static void ValidateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException("End date must be on or after start date.");
        }
    }

    private static OverfittingWarning? BuildOverfittingWarning(decimal inSampleReturn, decimal outOfSampleReturn)
    {
        var gap = inSampleReturn - outOfSampleReturn;
        var isDramatic = gap >= 10m || (inSampleReturn > 5m && outOfSampleReturn < 0m && gap >= 5m);

        if (!isDramatic)
        {
            return null;
        }

        return new OverfittingWarning
        {
            InSampleNetReturnPercent = inSampleReturn,
            OutOfSampleNetReturnPercent = outOfSampleReturn,
            ReturnGapPercent = gap,
            Message =
                $"In-sample return ({inSampleReturn:F1}%) is much better than out-of-sample ({outOfSampleReturn:F1}%). " +
                "This is a classic overfitting signal. Out-of-sample is the only result that counts."
        };
    }
}
