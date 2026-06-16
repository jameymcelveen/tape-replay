using Shouldly;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services;
using TapeReplay.Api.Tests.Helpers;

namespace TapeReplay.Api.Tests;

public sealed class LookAheadAuditTests
{
    [Fact]
    public void EntryDecisionContext_does_not_expose_current_bar_close()
    {
        var properties = typeof(EntryDecisionContext)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        properties.ShouldNotContain("Close");
        properties.ShouldNotContain("High");
        properties.ShouldNotContain("Low");
        properties.ShouldContain("BarOpen");
    }

    [Fact]
    public void Honest_engine_does_not_capture_oracle_look_ahead_edge()
    {
        var bars = TestCandles.RisingBars("TEST", new DateOnly(2024, 6, 3), 30);
        var oracleGross = TestCandles.OracleLookAheadPnL(bars, shares: 20);

        var engine = new BacktestEngine(
            new DailyHighBreakoutStrategy(),
            new TradeCostModel(),
            new HonestMetricsCalculator());

        var config = new StrategyConfig
        {
            Name = "Test",
            PositionSizeUsd = 2_000m,
            MaxConcurrentTrades = 10
        };

        var result = engine.Run("TEST", new DateOnly(2024, 6, 3), config, bars, new TradeCostConfig());
        result.NetTotalPnL.ShouldBeLessThan(oracleGross * 0.5m);
    }
}
