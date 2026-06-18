using Shouldly;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Services.ChartBacktest;

namespace TapeReplay.Api.Tests;

public sealed class StrategyHeatmapTests
{
    [Fact]
    public void ConfigHasher_SameConfig_ProducesStableHash()
    {
        var config = new StrategyHeatmapStrategyConfig
        {
            Rule = "ORB",
            Scope = "all",
            Shares = 100,
            Params = new ReplayRuleParams { OrMinutes = 5, StopPct = 5, TargetPct = 10 }
        };

        var first = StrategyConfigHasher.Compute(config);
        var second = StrategyConfigHasher.Compute(config);

        first.ShouldBe(second);
        first.Length.ShouldBe(64);
    }

    [Fact]
    public void ConfigHasher_DifferentShares_ProducesDifferentHash()
    {
        var baseConfig = new StrategyHeatmapStrategyConfig
        {
            Rule = "orb",
            Scope = "regular",
            Shares = 100,
            Params = new ReplayRuleParams()
        };

        var other = new StrategyHeatmapStrategyConfig
        {
            Rule = "orb",
            Scope = "regular",
            Shares = 200,
            Params = new ReplayRuleParams()
        };

        StrategyConfigHasher.Compute(baseConfig).ShouldNotBe(StrategyConfigHasher.Compute(other));
    }

    [Fact]
    public void EvaluateDayBars_NoBars_ReturnsNoData()
    {
        var service = new ChartBacktestService(
            null!,
            [new Services.ChartBacktest.Rules.OrbReplayStrategy(), new Services.ChartBacktest.Rules.PmhReplayStrategy()]);

        var result = service.EvaluateDayBars([], "orb", new ReplayRuleParams(), 100, "regular");

        result.HasData.ShouldBeFalse();
        result.Traded.ShouldBeFalse();
    }
}
