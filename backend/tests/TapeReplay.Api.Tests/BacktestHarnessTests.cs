using Shouldly;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services;
using TapeReplay.Api.Tests.Helpers;

namespace TapeReplay.Api.Tests;

public sealed class BacktestHarnessTests
{
    [Fact]
    public async Task Commit_freezes_strategy_and_evaluate_scores_out_of_sample()
    {
        var ticker = "TEST";
        var config = new StrategyConfig { Name = "Harness", PositionSizeUsd = 1_000m };
        var bars = new Dictionary<DateOnly, IReadOnlyList<Candle>>();

        for (var d = 0; d < 10; d++)
        {
            var date = new DateOnly(2024, 6, 3).AddDays(d);
            bars[date] = TestCandles.RisingBars(ticker, date, 20, 50m + d);
        }

        var provider = new FixedBarsProvider(bars);
        var repository = new InMemoryMarketDataRepository(bars);
        var marketData = TestMarketDataServiceFactory.Create(repository, provider);
        var engine = new BacktestEngine(new DailyHighBreakoutStrategy(), new TradeCostModel(), new HonestMetricsCalculator());
        var harness = new BacktestHarness(engine, marketData, new StrategyParser(), new InMemoryBacktestCommitRepository());

        var commit = await harness.CommitAsync(new BacktestCommitRequest
        {
            Ticker = ticker,
            InSampleStart = new DateOnly(2024, 6, 3),
            InSampleEnd = new DateOnly(2024, 6, 7),
            Strategy = config
        });

        commit.CommitId.ShouldNotBe(Guid.Empty);
        commit.InSample.SampleLabel.ShouldBe(SampleLabel.InSample);

        var evaluation = await harness.EvaluateAsync(new BacktestEvaluateRequest
        {
            CommitId = commit.CommitId,
            OutOfSampleStart = new DateOnly(2024, 6, 8),
            OutOfSampleEnd = new DateOnly(2024, 6, 12)
        });

        evaluation.OutOfSample.SampleLabel.ShouldBe(SampleLabel.OutOfSample);
        evaluation.Verdict.ShouldNotBeNullOrWhiteSpace();
    }
}
