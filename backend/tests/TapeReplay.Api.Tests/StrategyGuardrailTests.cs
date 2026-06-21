using Shouldly;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services;
using TapeReplay.Api.Services.ChartBacktest;
using TapeReplay.Api.Tests.Helpers;

namespace TapeReplay.Api.Tests;

public sealed class StrategyGuardrailTests
{
    [Fact]
    public void Opening_range_break_enters_once_after_orb_forms()
    {
        var date = new DateOnly(2026, 6, 16);
        var bars = BuildOrbBreakoutDay(date, openingHigh: 10m, breakoutOpen: 10.5m);
        var engine = CreateEngine();
        var config = DefaultConfig();
        config.EntryWindowStart = new TimeOnly(9, 30);

        var result = engine.Run("TEST", date, config, bars, new TradeCostConfig());

        result.Trades.Count.ShouldBeGreaterThan(0);
        result.Trades.Count.ShouldBeLessThanOrEqualTo(2);
        result.IdealTrade?.BuyTime.ShouldNotBeNull();
        result.IdealTrade?.SellTime.ShouldNotBeNull();
    }

    [Fact]
    public void Max_trades_per_day_blocks_reentry_churn()
    {
        var date = new DateOnly(2026, 6, 16);
        var bars = BuildChurnDay(date);
        var engine = CreateEngine();
        var config = DefaultConfig();
        config.MaxTradesPerDay = 1;
        config.EntryTrigger = EntryTriggerType.PriceBreaksAboveDailyHigh;

        var result = engine.Run("TEST", date, config, bars, new TradeCostConfig());

        result.Trades.Count.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Strategy_parser_round_trips_new_entry_rules()
    {
        var parser = new StrategyParser();
        var config = DefaultConfig();

        var dsl = parser.Generate(config);
        var parsed = parser.Parse(dsl);

        parsed.EntryTrigger.ShouldBe(EntryTriggerType.OpeningRangeHighBreak);
        parsed.OpeningRangeMinutes.ShouldBe(2);
        parsed.EntryWindowStart.ShouldBe(new TimeOnly(9, 35));
        parsed.MaxTradesPerDay.ShouldBe(1);
        parsed.NoReentryAfterStop.ShouldBeTrue();
        parsed.RegularSessionOnly.ShouldBeTrue();
    }

    private static BacktestEngine CreateEngine() =>
        new(new DailyHighBreakoutStrategy(), new TradeCostModel(), new HonestMetricsCalculator());

    private static StrategyConfig DefaultConfig() => new()
    {
        Name = "Test ORB",
        EntryTrigger = EntryTriggerType.OpeningRangeHighBreak,
        OpeningRangeMinutes = 2,
        EntryWindowStart = new TimeOnly(9, 35),
        EntryWindowEnd = new TimeOnly(10, 30),
        PositionSizeUsd = 1_000m,
        StopLossPercent = 2m,
        TakeProfitTargets =
        [
            new TakeProfitTarget { Percent = 3m, Weight = 1m }
        ],
        CloseAllAt = new TimeOnly(12, 0),
        MaxDailyLossUsd = 500m,
        MaxConcurrentTrades = 1,
        MaxTradesPerDay = 1,
        NoReentryAfterStop = true,
        RegularSessionOnly = true,
        FirstBreakoutOnly = true
    };

    private static List<Candle> BuildOrbBreakoutDay(DateOnly date, decimal openingHigh, decimal breakoutOpen)
    {
        var bars = new List<Candle>();
        var start = EasternMarketTime.ToUtc(date, EasternMarketTime.RegularOpen);

        bars.Add(MakeBar(start, 0, openingHigh - 0.2m, openingHigh, openingHigh - 0.3m));
        bars.Add(MakeBar(start, 1, openingHigh - 0.1m, openingHigh, openingHigh - 0.2m));
        bars.Add(MakeBar(start, 2, breakoutOpen, breakoutOpen + 0.5m, breakoutOpen - 0.1m));
        bars.Add(MakeBar(start, 3, breakoutOpen + 0.2m, breakoutOpen + 1m, breakoutOpen + 0.1m));

        return bars;
    }

    private static List<Candle> BuildChurnDay(DateOnly date)
    {
        var bars = new List<Candle>();
        var start = EasternMarketTime.ToUtc(date, EasternMarketTime.RegularOpen);
        var price = 10m;

        for (var i = 0; i < 20; i++)
        {
            var open = price;
            var high = open + (i % 2 == 0 ? 0.2m : 0.05m);
            var low = open - 0.15m;
            var close = open + (i % 2 == 0 ? -0.1m : 0.1m);
            bars.Add(MakeBar(start, i, open, high, low, close));
            price = close;
        }

        return bars;
    }

    private static Candle MakeBar(
        DateTime sessionStart,
        int minuteOffset,
        decimal open,
        decimal high,
        decimal low,
        decimal? close = null) => new()
        {
            Ticker = "TEST",
            DateTime = sessionStart.AddMinutes(minuteOffset),
            Open = open,
            High = high,
            Low = low,
            Close = close ?? open,
            Volume = 10_000
        };
}
